using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Esquecidos.Nucleo.Comandos;

namespace Esquecidos.Nucleo.Roteiro
{
    /// <summary>Liga um tipo de comando ao nome dele nos arquivos de roteiro.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class RoteiroAttribute : Attribute
    {
        public string Nome { get; }
        public string Ajuda { get; }

        public RoteiroAttribute(string nome, string ajuda = "")
        {
            Nome = nome;
            Ajuda = ajuda;
        }
    }

    public sealed class LinhaDeRoteiro
    {
        public string Comando;
        public string[] Args;
        public int Numero;
        public string Arquivo;

        public override string ToString() => $"{Arquivo}:{Numero} {Comando} {string.Join(" ", Args)}";
    }

    public sealed class Roteiro
    {
        public string Nome;
        public List<LinhaDeRoteiro> Linhas = new List<LinhaDeRoteiro>();
        public Dictionary<string, int> Rotulos = new Dictionary<string, int>();
    }

    /// <summary>
    /// Le os arquivos de roteiro: "@nome" abre um roteiro, cada linha e um
    /// comando seguido de argumentos, com aspas para texto que tem espaco.
    /// E o papel do .sce no Pal3: a historia escrita como sequencia de comandos.
    /// </summary>
    public static class LeitorDeRoteiro
    {
        public static List<Roteiro> Ler(string conteudo, string arquivo)
        {
            var lista = new List<Roteiro>();
            Roteiro atual = null;
            string[] linhas = conteudo.Replace("\r\n", "\n").Split('\n');
            for (int n = 0; n < linhas.Length; n++)
            {
                string bruta = linhas[n].Trim();
                if (bruta.Length == 0 || bruta[0] == '#') continue;
                if (bruta[0] == '@')
                {
                    atual = new Roteiro { Nome = bruta.Substring(1).Trim() };
                    lista.Add(atual);
                    continue;
                }
                if (atual == null) throw new FormatException($"{arquivo}:{n + 1} comando fora de roteiro (falta '@nome')");
                List<string> partes = Tokenizar(bruta, arquivo, n + 1);
                var linha = new LinhaDeRoteiro { Comando = partes[0], Args = partes.Skip(1).ToArray(), Numero = n + 1, Arquivo = arquivo };
                if (!Idioma.EmPortugues)
                    foreach (int i in Traduziveis(linha)) linha.Args[i] = Idioma.T(linha.Args[i]);
                if (linha.Comando == "rotulo")
                {
                    if (linha.Args.Length != 1) throw new FormatException($"{arquivo}:{n + 1} 'rotulo' precisa de um nome");
                    atual.Rotulos[linha.Args[0]] = atual.Linhas.Count;
                }
                atual.Linhas.Add(linha);
            }
            return lista;
        }

        /// <summary>
        /// Quais argumentos da linha sao texto para o jogador ler: a fala, a
        /// narracao, as opcoes da escolha, a legenda, a dica, e o nome de quem
        /// fala quando nao e um id de personagem ("Sua voz", "Dra. Vasco").
        /// </summary>
        public static IEnumerable<int> Traduziveis(LinhaDeRoteiro l)
        {
            string[] a = l.Args;
            switch (l.Comando)
            {
                case "fala":
                    if (a.Length > 0 && (a[0].Contains(" ") || a[0].Any(char.IsUpper))) yield return 0;
                    if (a.Length > 1) yield return 1;
                    break;
                case "narra":
                case "legenda":
                case "dica":
                    if (a.Length > 0) yield return 0;
                    break;
                case "escolha":
                    for (int i = 0; i + 1 < a.Length; i++)
                        if (a[i + 1] == "->" && a[i] != "->") yield return i;
                    break;
            }
        }

        public static List<string> Tokenizar(string linha, string arquivo = "?", int numero = 0)
        {
            var partes = new List<string>();
            var sb = new StringBuilder();
            bool aspas = false, temToken = false;
            for (int i = 0; i < linha.Length; i++)
            {
                char c = linha[i];
                if (aspas)
                {
                    if (c == '\\' && i + 1 < linha.Length && linha[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else if (c == '"') aspas = false;
                    else sb.Append(c);
                    continue;
                }
                if (c == '"')
                {
                    aspas = true;
                    temToken = true;
                    continue;
                }
                if (char.IsWhiteSpace(c))
                {
                    if (temToken) partes.Add(sb.ToString());
                    sb.Clear();
                    temToken = false;
                    continue;
                }
                if (c == '#' && !temToken) break;
                sb.Append(c);
                temToken = true;
            }
            if (aspas) throw new FormatException($"{arquivo}:{numero} aspas sem fechar");
            if (temToken) partes.Add(sb.ToString());
            if (partes.Count == 0) throw new FormatException($"{arquivo}:{numero} linha vazia");
            return partes;
        }
    }

    /// <summary>
    /// Transforma "da_item bandagem 3" num objeto DarItem("bandagem", 3) olhando
    /// o construtor do tipo marcado com [Roteiro("da_item")]. Novo comando e so
    /// declarar o tipo; ninguem precisa mexer no leitor.
    /// </summary>
    public sealed class FabricaDeComandos
    {
        private readonly Dictionary<string, Type> _tipos = new Dictionary<string, Type>();

        public IReadOnlyDictionary<string, Type> Tipos => _tipos;

        public void RegistrarDe(Assembly assembly)
        {
            foreach (Type t in assembly.GetTypes())
            {
                var attr = t.GetCustomAttribute<RoteiroAttribute>();
                if (attr == null) continue;
                if (!typeof(IComando).IsAssignableFrom(t)) throw new InvalidOperationException($"{t.Name} tem [Roteiro] mas nao e IComando");
                if (_tipos.TryGetValue(attr.Nome, out Type ja) && ja != t)
                    throw new InvalidOperationException($"comando de roteiro '{attr.Nome}' declarado duas vezes: {ja.Name} e {t.Name}");
                _tipos[attr.Nome] = t;
            }
        }

        public bool Conhece(string nome) => _tipos.ContainsKey(nome);

        public IComando Criar(LinhaDeRoteiro linha)
        {
            if (!_tipos.TryGetValue(linha.Comando, out Type tipo))
                throw new FormatException($"{linha.Arquivo}:{linha.Numero} comando desconhecido '{linha.Comando}'");
            ConstructorInfo ctor = tipo.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            ParameterInfo[] ps = ctor.GetParameters();
            int obrigatorios = ps.Count(p => !p.HasDefaultValue);
            if (linha.Args.Length < obrigatorios || linha.Args.Length > ps.Length)
                throw new FormatException($"{linha.Arquivo}:{linha.Numero} '{linha.Comando}' espera {obrigatorios}..{ps.Length} argumentos, veio {linha.Args.Length}");
            var valores = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (i >= linha.Args.Length)
                {
                    valores[i] = ps[i].DefaultValue;
                    continue;
                }
                valores[i] = Converter(linha.Args[i], ps[i].ParameterType, linha, ps[i].Name);
            }
            return (IComando)ctor.Invoke(valores);
        }

        private static object Converter(string texto, Type tipo, LinhaDeRoteiro linha, string nome)
        {
            if (tipo == typeof(string)) return texto;
            if (tipo == typeof(int) && int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return i;
            if (tipo == typeof(float) && float.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return f;
            if (tipo == typeof(bool))
            {
                if (texto == "1" || texto == "sim") return true;
                if (texto == "0" || texto == "nao") return false;
            }
            throw new FormatException($"{linha.Arquivo}:{linha.Numero} '{linha.Comando}': '{texto}' nao serve como {nome} ({tipo.Name})");
        }
    }
}
