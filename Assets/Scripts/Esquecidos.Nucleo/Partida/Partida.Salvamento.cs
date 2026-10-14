using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Progresso;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Partidas
{
    /// <summary>
    /// O jogo salvo e um arquivo no mesmo formato dos dados ([secao] e
    /// chave=valor), como o Flare faz: da para abrir num editor de texto e
    /// entender. Nada de binario, nada de biblioteca de JSON.
    /// </summary>
    public sealed partial class Partida
    {
        public const int VersaoDoSalvo = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string Salvar()
        {
            var sb = new StringBuilder();
            void L(string k, object v) => sb.Append(k).Append('=').Append(Convert.ToString(v, Inv)).Append('\n');

            sb.Append("# Os Esquecidos — jogo salvo\n[jogo]\n");
            L("versao", VersaoDoSalvo);
            L("semente", SementeDoMundo);
            L("dia", Ciclo.Dia);
            L("hora", Ciclo.Hora.ToString("0.###", Inv));
            L("tempo", TempoDeJogo.ToString("0.#", Inv));
            L("mapa", Mapa?.Id ?? MapaInicial);
            if (Jogador != null)
            {
                L("x", Jogador.Pos.X.ToString("0.##", Inv));
                L("z", Jogador.Pos.Y.ToString("0.##", Inv));
                L("vida", Jogador.Vida.ToString("0.#", Inv));
                L("folego", Jogador.Energia.ToString("0.#", Inv));
            }
            if (CompanheiroNpc != null) L("companheiro", CompanheiroNpc);

            sb.Append("\n[personagem]\n");
            Personagem h = Heroi;
            L("nome", h.Nome);
            L("classe", h.Classe);
            L("nivel", h.Nivel);
            L("xp", h.Xp);
            L("sucata", h.Sucata);
            L("pontos_atributo", h.PontosDeAtributo);
            L("pontos_poder", h.PontosDePoder);
            L("atributos", string.Join(",", h.Atributos));
            L("barra", string.Join(",", h.Barra.Select(b => b ?? "-")));
            L("aparencia", AparenciaParaTexto(h.Aparencia));
            foreach (KeyValuePair<string, int> p in h.Poderes) L("poder", $"{p.Key},{p.Value}");

            for (int i = 0; i < h.Equipado.Length; i++)
                if (h.Equipado[i] != null) EscreverItem(sb, "equipado:" + ((SlotEquip)i), h.Equipado[i]);
            for (int i = 0; i < h.Mochila.Tamanho; i++)
                if (h.Mochila.Espacos[i] != null) EscreverItem(sb, "mochila:" + i, h.Mochila.Espacos[i]);
            for (int i = 0; i < Guarda.Tamanho; i++)
                if (Guarda.Espacos[i] != null) EscreverItem(sb, "guarda:" + i, Guarda.Espacos[i]);

            sb.Append("\n[historia]\n");
            foreach (string s in Diario.Status.OrderBy(x => x, StringComparer.Ordinal)) L("status", s);
            foreach (string id in Diario.Ordem)
            {
                EstadoDeMissao m = Diario.Missoes[id];
                L("missao", $"{m.Id},{m.Etapa},{m.Contador},{(m.Concluida ? 1 : 0)}");
            }
            foreach (KeyValuePair<string, bool> o in EstadosDeObjetos) L("objeto", $"{o.Key},{(o.Value ? 1 : 0)}");
            foreach (string r in RegistrosLidos) L("registro", r);
            foreach (KeyValuePair<string, int> a in Afinidade) L("afinidade", $"{a.Key},{a.Value}");
            foreach (KeyValuePair<string, int> c in Contadores) L("contador", $"{c.Key},{c.Value}");
            foreach (KeyValuePair<string, HashSet<int>> e in Explorado)
            {
                // So o dia atual importa para o minimapa; os outros so contam o que ja foi entregue.
                L("explorado", $"{e.Key},{string.Join(" ", e.Value.OrderBy(x => x))}");
            }
            foreach (KeyValuePair<string, int> e in Entregue) L("entregue", $"{e.Key},{e.Value}");
            return sb.ToString();
        }

        private static void EscreverItem(StringBuilder sb, string onde, Item item)
        {
            sb.Append("\n[item]\n");
            sb.Append("onde=").Append(onde).Append('\n');
            sb.Append("base=").Append(item.Base).Append('\n');
            if (item.Raridade != Raridade.Comum) sb.Append("raridade=").Append((int)item.Raridade).Append('\n');
            sb.Append("nivel=").Append(item.Nivel).Append('\n');
            if (item.Quantidade != 1) sb.Append("qtd=").Append(item.Quantidade).Append('\n');
            foreach (AfixoRolado a in item.Afixos)
                sb.Append("afixo=").Append(a.Afixo).Append(',').Append(a.Valor.ToString("0.###", Inv)).Append('\n');
        }

        private static string AparenciaParaTexto(Aparencia a)
        {
            return string.Format(Inv, "pele:{0};cabelo:{1};penteado:{2};roupa:{3};calca:{4};botas:{5};altura:{6:0.##};porte:{7:0.##}{8}",
                a.Pele, a.Cabelo, a.Penteado, a.Roupa, a.Calca, a.Botas, a.Altura, a.Porte,
                string.IsNullOrEmpty(a.Acessorio) ? "" : ";acessorio:" + a.Acessorio);
        }

        /// <summary>
        /// Monta uma partida a partir do texto salvo e entra no mapa onde o
        /// jogador estava. Item de base que nao existe mais (dados mudaram)
        /// some em vez de derrubar a carga.
        /// </summary>
        public void Carregar(string texto)
        {
            List<Secao> secoes = LeitorIni.Ler(texto, "salvo");
            Secao jogo = secoes.First(s => s.Nome == "jogo");
            Secao pers = secoes.First(s => s.Nome == "personagem");
            if (jogo.Inteiro("versao") > VersaoDoSalvo) throw new FormatException("jogo salvo de uma versão mais nova");

            SementeDoMundo = jogo.Inteiro("semente", 1);
            Rng = new Random(SementeDoMundo ^ 0x5eed ^ jogo.Inteiro("dia"));
            Ciclo.Definir(jogo.Inteiro("dia", 1), jogo.Real("hora", 8));
            TempoDeJogo = jogo.Real("tempo");
            CompanheiroNpc = jogo.Texto("companheiro");

            var h = new Personagem
            {
                Nome = pers.Texto("nome", "Novato"),
                Classe = pers.TextoObrigatorio("classe"),
                Nivel = pers.Inteiro("nivel", 1),
                Xp = pers.Inteiro("xp"),
                Sucata = pers.Inteiro("sucata"),
                PontosDeAtributo = pers.Inteiro("pontos_atributo"),
                PontosDePoder = pers.Inteiro("pontos_poder"),
                Aparencia = Aparencia.Ler(pers.Texto("aparencia")),
            };
            string[] at = pers.Lista("atributos");
            for (int i = 0; i < 4 && i < at.Length; i++) h.Atributos[i] = pers.LerInteiro(at[i], "atributos");
            string[] barra = pers.Lista("barra");
            for (int i = 0; i < h.Barra.Length && i < barra.Length; i++) h.Barra[i] = barra[i] == "-" || barra[i].Length == 0 ? null : barra[i];
            foreach (string p in pers.Todos("poder"))
            {
                string[] partes = Secao.Partir(p);
                if (Catalogo.Poderes.ContainsKey(partes[0])) h.Poderes[partes[0]] = pers.LerInteiro(partes[1], "poder");
            }
            Heroi = h;
            Array.Clear(Guarda.Espacos, 0, Guarda.Tamanho);

            foreach (Secao s in secoes.Where(x => x.Nome == "item"))
            {
                string baseId = s.Texto("base");
                if (baseId == null || !Catalogo.Itens.ContainsKey(baseId)) continue;
                var item = new Item(baseId, s.Inteiro("qtd", 1))
                {
                    Raridade = (Raridade)s.Inteiro("raridade"),
                    Nivel = s.Inteiro("nivel", 1),
                };
                foreach (string a in s.Todos("afixo"))
                {
                    string[] p = Secao.Partir(a);
                    if (Catalogo.Afixos.ContainsKey(p[0])) item.Afixos.Add(new AfixoRolado { Afixo = p[0], Valor = s.LerReal(p[1], "afixo") });
                }
                string[] onde = s.Texto("onde", "mochila:0").Split(':');
                int n;
                switch (onde[0])
                {
                    case "equipado":
                        if (Enum.TryParse(onde[1], out SlotEquip slot)) h.Equipado[(int)slot] = item;
                        break;
                    case "guarda":
                        n = int.Parse(onde[1], Inv);
                        if (n < Guarda.Tamanho && Guarda.Espacos[n] == null) Guarda.Espacos[n] = item;
                        else Guarda.Adicionar(item, Catalogo);
                        break;
                    default:
                        n = int.Parse(onde[1], Inv);
                        if (n < h.Mochila.Tamanho && h.Mochila.Espacos[n] == null) h.Mochila.Espacos[n] = item;
                        else h.Mochila.Adicionar(item, Catalogo);
                        break;
                }
            }

            Diario.Status.Clear();
            Diario.Missoes.Clear();
            Diario.Ordem.Clear();
            EstadosDeObjetos.Clear();
            RegistrosLidos.Clear();
            Afinidade.Clear();
            Contadores.Clear();
            Explorado.Clear();
            Entregue.Clear();
            Secao hist = secoes.FirstOrDefault(s => s.Nome == "historia");
            if (hist != null)
            {
                foreach (string s in hist.Todos("status")) Diario.Status.Add(s);
                foreach (string m in hist.Todos("missao"))
                {
                    string[] p = Secao.Partir(m);
                    if (!Catalogo.Missoes.ContainsKey(p[0])) continue;
                    Diario.Missoes[p[0]] = new EstadoDeMissao
                    {
                        Id = p[0],
                        Etapa = hist.LerInteiro(p[1], "missao"),
                        Contador = hist.LerInteiro(p[2], "missao"),
                        Concluida = p[3] == "1",
                    };
                    Diario.Ordem.Add(p[0]);
                }
                foreach (string o in hist.Todos("objeto"))
                {
                    int v = o.LastIndexOf(',');
                    EstadosDeObjetos[o.Substring(0, v)] = o.Substring(v + 1) == "1";
                }
                foreach (string r in hist.Todos("registro")) RegistrosLidos.Add(r);
                foreach (string a in hist.Todos("afinidade"))
                {
                    string[] p = Secao.Partir(a);
                    Afinidade[p[0]] = hist.LerInteiro(p[1], "afinidade");
                }
                foreach (string c in hist.Todos("contador"))
                {
                    int v = c.LastIndexOf(',');
                    Contadores[c.Substring(0, v)] = hist.LerInteiro(c.Substring(v + 1), "contador");
                }
                foreach (string e in hist.Todos("explorado"))
                {
                    int v = e.LastIndexOf(',');
                    var celulas = new HashSet<int>();
                    foreach (string c in e.Substring(v + 1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        celulas.Add(int.Parse(c, Inv));
                    Explorado[e.Substring(0, v)] = celulas;
                }
                foreach (string e in hist.Todos("entregue"))
                {
                    int v = e.LastIndexOf(',');
                    Entregue[e.Substring(0, v)] = hist.LerInteiro(e.Substring(v + 1), "entregue");
                }
            }

            _vidaGuardada = jogo.Real("vida", -1);
            _folegoGuardado = jogo.Real("folego", -1);
            string mapa = jogo.Texto("mapa", MapaInicial);
            if (mapa != "patio" && !Catalogo.Setores.ContainsKey(mapa)) mapa = MapaInicial;
            Mundo = null;
            EntrarNoMapa(mapa, mapa == "patio" ? "inicio" : "patio");
            if (Jogador != null && jogo.Tem("x"))
            {
                var pos = new Vector2(jogo.Real("x"), jogo.Real("z"));
                if (Mundo.Grade.FolgaEm(pos) >= Jogador.Raio * 0.8f)
                {
                    Jogador.Pos = Jogador.PosAnterior = pos;
                }
            }
            Despachante.Despachar(new NotFichaMudou());
        }
    }
}
