using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Esquecidos.Nucleo;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Roteiro;
using Xunit;
using Xunit.Abstractions;

namespace Esquecidos.Testes
{
    /// <summary>
    /// Toda frase que o jogador le precisa existir em ingles. O teste junta as
    /// chaves de tres lugares (catalogo, roteiros e as chamadas T/F no codigo),
    /// compara com Resources/Traducoes/en e escreve as que faltam num arquivo.
    /// </summary>
    [CollectionDefinition("Idioma", DisableParallelization = true)]
    public class ColecaoIdioma
    {
    }

    [Collection("Idioma")]
    public class TraducaoTestes
    {
        private readonly ITestOutputHelper _saida;

        public TraducaoTestes(ITestOutputHelper saida)
        {
            _saida = saida;
        }

        /// <summary>Textos montados em tempo de execucao a partir de listas fixas (T(lado), T(motivo)...).</summary>
        private static readonly string[] Dinamicos =
        {
            "leste", "oeste", "norte", "sul",
            "atordoado", "recarregando", "fôlego insuficiente", "arma errada", "morto",
            "curto", "raspado", "longo", "trança", "coque", "rabo", "cacheado", "bagunçado", "careca",
            "Baixa", "Média", "Alta",
        };

        private static string Desescapar(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char c = s[++i];
                    sb.Append(c == 'n' ? '\n' : c == 't' ? '\t' : c);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public static Dictionary<string, string> Chaves()
        {
            // chave -> de onde veio (o primeiro lugar)
            var chaves = new Dictionary<string, string>(StringComparer.Ordinal);
            void Add(string s, string origem)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                if (!chaves.ContainsKey(s)) chaves[s] = origem;
            }

            Catalogo c = Apoio.Catalogo();
            c.PassarTextos(s =>
            {
                Add(s, "dados");
                return s;
            });
            foreach (string afixo in File.ReadAllLines(Path.Combine(Apoio.Raiz, "Assets", "Resources", "Dados", "afixos.txt")).Where(l => l.StartsWith("nome=")))
                foreach (string n in afixo.Substring(5).Split('|')) Add(n.Trim(), "dados");

            foreach ((string nome, string conteudo) in Apoio.Arquivos("Roteiros"))
                foreach (Roteiro r in LeitorDeRoteiro.Ler(conteudo, nome))
                    foreach (LinhaDeRoteiro l in r.Linhas)
                        foreach (int i in LeitorDeRoteiro.Traduziveis(l))
                            Add(l.Args[i], "roteiro:" + nome);

            var chamada = new Regex(@"(?<![A-Za-z0-9_])(?:Idioma\.)?[TF]\(""((?:[^""\\]|\\.)*)""");
            foreach (string arquivo in Directory.GetFiles(Path.Combine(Apoio.Raiz, "Assets", "Scripts"), "*.cs", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (arquivo.Contains("Teste")) continue;
                string texto = File.ReadAllText(arquivo);
                foreach (Match m in chamada.Matches(texto)) Add(Desescapar(m.Groups[1].Value), "codigo:" + Path.GetFileName(arquivo));
            }
            foreach (string d in Dinamicos) Add(d, "dinamico");
            return chaves;
        }

        private static List<KeyValuePair<string, string>> Traducoes(string idioma)
        {
            string pasta = Path.Combine(Apoio.Raiz, "Assets", "Resources", "Traducoes", idioma);
            var pares = new List<KeyValuePair<string, string>>();
            if (!Directory.Exists(pasta)) return pares;
            foreach (string f in Directory.GetFiles(pasta, "*.txt").OrderBy(x => x, StringComparer.Ordinal))
                pares.AddRange(Idioma.Ler(File.ReadAllText(f)));
            return pares;
        }

        private static readonly Regex Marcador = new Regex(@"\{(\d+)(:[^}]*)?\}");

        [Fact]
        public void TodoTextoTemTraducaoEmIngles()
        {
            Dictionary<string, string> chaves = Chaves();
            var tabela = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> kv in Traducoes("en")) tabela[kv.Key] = kv.Value;
            List<string> faltando = chaves.Keys.Where(k => !tabela.ContainsKey(k)).ToList();

            // As que faltam vao para um arquivo no formato da traducao, agrupadas pela origem.
            var sb = new StringBuilder();
            foreach (IGrouping<string, string> g in faltando.GroupBy(k => chaves[k].Split(':')[0]))
            {
                sb.Append("# ").Append(g.Key).Append('\n');
                foreach (string k in g) sb.Append(Idioma.Escapar(k)).Append("\n= \n\n");
            }
            string saida = Path.Combine(AppContext.BaseDirectory, "faltando_en.txt");
            File.WriteAllText(saida, sb.ToString());
            _saida.WriteLine($"{chaves.Count} textos, {faltando.Count} sem traducao (lista em {saida})");
            Assert.True(faltando.Count == 0, $"{faltando.Count} textos sem traducao; veja {saida}. Exemplos: {string.Join(" | ", faltando.Take(8))}");
        }

        [Fact]
        public void TraducaoTemOsMesmosMarcadoresDoOriginal()
        {
            var problemas = new List<string>();
            foreach (KeyValuePair<string, string> kv in Traducoes("en"))
            {
                var a = Marcador.Matches(kv.Key).Select(m => m.Value).OrderBy(x => x).ToList();
                var b = Marcador.Matches(kv.Value).Select(m => m.Value).OrderBy(x => x).ToList();
                if (!a.SequenceEqual(b)) problemas.Add($"'{kv.Key}' -> '{kv.Value}'");
                if (kv.Value.Trim().Length == 0) problemas.Add($"'{kv.Key}' sem texto em ingles");
                if (kv.Key.Count(ch => ch == '<') != kv.Value.Count(ch => ch == '<')) problemas.Add($"'{kv.Key}': etiquetas <b>/<size> diferentes");
            }
            Assert.True(problemas.Count == 0, string.Join("\n", problemas.Take(30)));
        }

        [Fact]
        public void JogoInteiroEmInglesNaoDeixaTextoParaTras()
        {
            // Uma partida curta em ingles: o que o nucleo pedir e nao tiver traducao aparece aqui.
            var arquivos = Traducoes("en");
            Idioma.Definir(Idioma.Ingles, new[] { string.Join("\n\n", arquivos.Select(kv => Idioma.Escapar(kv.Key) + "\n= " + Idioma.Escapar(kv.Value))) });
            try
            {
                Catalogo c = Apoio.Catalogo();
                c.Traduzir();
                var p = new Esquecidos.Nucleo.Partidas.Partida(c, () => Apoio.Arquivos("Roteiros"));
                p.NovaPartida("corredor", "Test", null, 7);
                Apoio.PularCenas(p);
                p.EntrarNoMapa("setor1", "patio");
                Apoio.PularCenas(p);
                Apoio.Rodar(p, 5);
                Assert.True(Idioma.Faltando.Count == 0, "pedidos sem traducao: " + string.Join(" | ", Idioma.Faltando.Take(10)));
                Assert.DoesNotContain("Pátio", c.Setor("setor1").Nome + c.Item("faca_cozinha").Nome);
            }
            finally
            {
                Idioma.Definir(Idioma.Portugues);
            }
        }
    }
}
