using System;
using System.Collections.Generic;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Dados
{
    public sealed class UsoDePoderDoInimigo
    {
        public string Poder;
        public int Peso = 10;
        public float AlcanceMin;
        public float AlcanceMax = 2.2f;
    }

    /// <summary>Um tipo de inimigo de dados/inimigos.txt, no nivel base dele.</summary>
    public sealed class InimigoDef
    {
        public string Id;
        public string Nome;
        public string Modelo;
        public float Escala = 1f;
        public string Cor = "#8a8a8a";
        public string CorOlhos = "#ff3020";
        public int Nivel = 1;
        public float Vida = 30;
        public float Raio = 0.5f;
        public float Velocidade = 4f;
        public int Xp = 10;
        public int SucataMin, SucataMax;
        public Ficha Estat = new();
        public List<UsoDePoderDoInimigo> Poderes = new();
        public string Ia = "corpo";
        public float Visao = 11f;
        public float Perseguicao = 24f;
        public bool Chefe;
        public bool Voa;
        public string Loot;
        public float VidaPorNivel = 0.28f;
        public float DanoPorNivel = 0.14f;
        public string Voz = "tecelao";
        public string Descricao = "";

        public static InimigoDef Ler(Secao s)
        {
            var d = new InimigoDef
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Modelo = s.Texto("modelo", "tecelao"),
                Escala = s.Real("escala", 1f),
                Cor = s.Texto("cor", "#8a8a8a"),
                CorOlhos = s.Texto("cor_olhos", "#ff3020"),
                Nivel = s.Inteiro("nivel", 1),
                Vida = s.Real("vida", 30),
                Raio = s.Real("raio", 0.5f),
                Velocidade = s.Real("velocidade", 4f),
                Xp = s.Inteiro("xp", 10),
                Ia = s.Texto("ia", "corpo"),
                Visao = s.Real("visao", 11f),
                Perseguicao = s.Real("perseguicao", 24f),
                Chefe = s.Logico("chefe"),
                Voa = s.Logico("voa"),
                Loot = s.Texto("loot"),
                VidaPorNivel = s.Real("vida_por_nivel", 0.28f),
                DanoPorNivel = s.Real("dano_por_nivel", 0.14f),
                Voz = s.Texto("voz", "tecelao"),
                Descricao = s.Texto("descricao", ""),
            };
            (float a, float b) = s.Par("sucata");
            d.SucataMin = (int)a;
            d.SucataMax = (int)b;
            foreach (string linha in s.Todos("estat"))
            {
                string[] p = Secao.Partir(linha);
                d.Estat.Somar(Nomes.EstatDe(p[0]), s.LerReal(p[1], "estat"));
            }
            foreach (string linha in s.Todos("poder"))
            {
                // poder=id,peso,alcance_min,alcance_max
                string[] p = Secao.Partir(linha);
                d.Poderes.Add(new UsoDePoderDoInimigo
                {
                    Poder = p[0],
                    Peso = p.Length > 1 ? s.LerInteiro(p[1], "poder") : 10,
                    AlcanceMin = p.Length > 2 ? s.LerReal(p[2], "poder") : 0,
                    AlcanceMax = p.Length > 3 ? s.LerReal(p[3], "poder") : 2.2f,
                });
            }
            return d;
        }
    }

    /// <summary>Aparencia de um humano montado em pecas: cores, cabelo, porte.</summary>
    public sealed class Aparencia
    {
        public string Pele = "#c99a76";
        public string Cabelo = "#2b2018";
        public string Penteado = "curto";
        public string Roupa = "#5a6470";
        public string Calca = "#3a3a38";
        public string Botas = "#2a2420";
        public string Acessorio = "";
        public float Altura = 1f;
        public float Porte = 1f;

        public static Aparencia Ler(string texto)
        {
            var a = new Aparencia();
            if (string.IsNullOrEmpty(texto)) return a;
            foreach (string par in texto.Split(';'))
            {
                int i = par.IndexOf(':');
                if (i < 0) continue;
                string k = par.Substring(0, i).Trim();
                string v = par.Substring(i + 1).Trim();
                switch (k)
                {
                    case "pele": a.Pele = v; break;
                    case "cabelo": a.Cabelo = v; break;
                    case "penteado": a.Penteado = v; break;
                    case "roupa": a.Roupa = v; break;
                    case "calca": a.Calca = v; break;
                    case "botas": a.Botas = v; break;
                    case "acessorio": a.Acessorio = v; break;
                    case "altura": a.Altura = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "porte": a.Porte = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); break;
                    default: throw new FormatException($"aparencia: chave desconhecida '{k}'");
                }
            }
            return a;
        }
    }

    public sealed class NpcDef
    {
        public string Id;
        public string Nome;
        public string Titulo = "";
        public Aparencia Aparencia;
        public string Loja;
        public string Estacao;
        public string Ocio = "parado";

        public static NpcDef Ler(Secao s)
        {
            return new NpcDef
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Titulo = s.Texto("titulo", ""),
                Aparencia = Aparencia.Ler(s.Texto("aparencia")),
                Loja = s.Texto("loja"),
                Estacao = s.Texto("estacao"),
                Ocio = s.Texto("ocio", "parado"),
            };
        }
    }

    /// <summary>
    /// Um assunto de conversa de um NPC. Aparece quando todas as condicoes
    /// batem, e o que ele faz e rodar um roteiro, que e onde a fala mora.
    /// </summary>
    public sealed class DialogoDef
    {
        public string Npc;
        public string Topico;
        public string Roteiro;
        public bool Automatico;
        public int Prioridade;
        public List<string> RequerStatus = new();
        public List<string> RequerNaoStatus = new();
        public List<string> RequerMissao = new();

        public static DialogoDef Ler(Secao s)
        {
            var d = new DialogoDef
            {
                Npc = s.TextoObrigatorio("npc"),
                Topico = s.TextoObrigatorio("topico"),
                Roteiro = s.TextoObrigatorio("roteiro"),
                Automatico = s.Logico("automatico"),
                Prioridade = s.Inteiro("prioridade"),
            };
            foreach (string v in s.Todos("requer_status")) d.RequerStatus.Add(v);
            foreach (string v in s.Todos("requer_nao_status")) d.RequerNaoStatus.Add(v);
            foreach (string v in s.Todos("requer_missao")) d.RequerMissao.Add(v);
            return d;
        }
    }

    public enum TipoObjetivo
    {
        Status,
        Matar,
        Item,
        Mapa,
        Nivel,
    }

    public sealed class EtapaDeMissao
    {
        public TipoObjetivo Tipo;
        public string Alvo;
        public int Quantidade = 1;
        public string Texto;
    }

    public sealed class MissaoDef
    {
        public string Id;
        public string Nome;
        public bool Principal;
        public string Descricao = "";
        public List<EtapaDeMissao> Etapas = new();
        public int RecompensaXp;
        public int RecompensaSucata;
        public List<string> RecompensaItens = new();
        public string StatusAoConcluir;
        public string RoteiroAoConcluir;

        public static MissaoDef Ler(Secao s)
        {
            var m = new MissaoDef
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Principal = s.Texto("tipo", "secundaria") == "principal",
                Descricao = s.Texto("descricao", ""),
                RecompensaXp = s.Inteiro("recompensa_xp"),
                RecompensaSucata = s.Inteiro("recompensa_sucata"),
                StatusAoConcluir = s.Texto("status_ao_concluir"),
                RoteiroAoConcluir = s.Texto("roteiro_ao_concluir"),
            };
            m.RecompensaItens.AddRange(s.Lista("recompensa_item"));
            foreach (string linha in s.Todos("etapa"))
            {
                // etapa=tipo:alvo:qtd|texto
                int barra = linha.IndexOf('|');
                if (barra < 0) throw new FormatException($"{s.Arquivo}:{s.Linha} etapa sem '|texto'");
                string[] cond = linha.Substring(0, barra).Split(':');
                var e = new EtapaDeMissao { Texto = linha.Substring(barra + 1).Trim() };
                switch (cond[0].Trim())
                {
                    case "status": e.Tipo = TipoObjetivo.Status; break;
                    case "matar": e.Tipo = TipoObjetivo.Matar; break;
                    case "item": e.Tipo = TipoObjetivo.Item; break;
                    case "mapa": e.Tipo = TipoObjetivo.Mapa; break;
                    case "nivel": e.Tipo = TipoObjetivo.Nivel; break;
                    default: throw new FormatException($"{s.Arquivo}:{s.Linha} objetivo desconhecido '{cond[0]}'");
                }
                e.Alvo = cond.Length > 1 ? cond[1].Trim() : "";
                if (cond.Length > 2) e.Quantidade = s.LerInteiro(cond[2], "etapa");
                if (e.Tipo == TipoObjetivo.Nivel) e.Quantidade = s.LerInteiro(e.Alvo, "etapa");
                m.Etapas.Add(e);
            }
            if (m.Etapas.Count == 0) throw new FormatException($"{s.Arquivo}:{s.Linha} missao '{m.Id}' sem etapas");
            return m;
        }
    }

    public sealed class TabelaDeLoot
    {
        public string Id;
        public float Chance = 0.3f;
        public int QuantidadeMin = 1, QuantidadeMax = 1;
        public int RaridadeBonus;
        public List<(string entrada, int peso)> Entradas = new();

        public static TabelaDeLoot Ler(Secao s)
        {
            var t = new TabelaDeLoot
            {
                Id = s.TextoObrigatorio("id"),
                Chance = s.Real("chance", 0.3f),
                RaridadeBonus = s.Inteiro("raridade_bonus"),
            };
            (float a, float b) = s.Par("quantidade", 1, 1);
            t.QuantidadeMin = (int)a;
            t.QuantidadeMax = (int)b;
            foreach (string linha in s.Todos("entrada"))
            {
                string[] p = Secao.Partir(linha);
                t.Entradas.Add((p[0], p.Length > 1 ? s.LerInteiro(p[1], "entrada") : 10));
            }
            return t;
        }
    }

    public sealed class LojaDef
    {
        public string Id;
        public string Nome;
        public List<string> Fixos = new();
        public int Aleatorios;
        public HashSet<string> Categorias = new();

        public static LojaDef Ler(Secao s)
        {
            var l = new LojaDef
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Aleatorios = s.Inteiro("aleatorios"),
            };
            l.Fixos.AddRange(s.Lista("fixos"));
            foreach (string c in s.Lista("categorias")) l.Categorias.Add(c);
            return l;
        }
    }

    public sealed class Receita
    {
        public string Id;
        public string Resultado;
        public int QuantidadeResultado = 1;
        public List<(string item, int qtd)> Ingredientes = new();
        public int Sucata;
        public string Estacao;
        public int Nivel = 1;
        public string RequerStatus;

        public static Receita Ler(Secao s)
        {
            string[] res = s.Lista("resultado");
            var r = new Receita
            {
                Id = s.TextoObrigatorio("id"),
                Resultado = res[0],
                QuantidadeResultado = res.Length > 1 ? s.LerInteiro(res[1], "resultado") : 1,
                Sucata = s.Inteiro("sucata"),
                Estacao = s.Texto("estacao", "oficina"),
                Nivel = s.Inteiro("nivel", 1),
                RequerStatus = s.Texto("requer_status"),
            };
            foreach (string linha in s.Todos("ingrediente"))
            {
                string[] p = Secao.Partir(linha);
                r.Ingredientes.Add((p[0], p.Length > 1 ? s.LerInteiro(p[1], "ingrediente") : 1));
            }
            return r;
        }
    }

    /// <summary>Receita de um setor do labirinto. O gerador faz o resto com a semente do dia.</summary>
    public sealed class SetorDef
    {
        public string Id;
        public string Nome;
        public string Tema = "hera";
        public int Colunas = 9, Linhas = 9;
        public float TamanhoCelula = 7f;
        public int Nivel = 1;
        public List<(string inimigo, int peso)> Inimigos = new();
        public List<(string inimigo, int peso)> InimigosNoturnos = new();
        public int Grupos = 8;
        public int GruposNoturnos = 4;
        public string Chefe;
        public string Portao;
        public int Baus = 3;
        public int Terminais = 1;
        public int Alavancas = 1;
        public int MurosMoveis = 6;
        public int Salas = 2;
        public string Proximo;
        public string StatusDoChefe;
        public string RequerStatus;
        public List<string> Terminal = new();
        public string Musica = "labirinto";
        /// <summary>Para onde a saida sul leva: "patio:leste" ou "setor4:norte".</summary>
        public string Volta;
        public string Descricao = "";

        public (string mapa, string entrada) Retorno()
        {
            string v = Volta ?? "patio:" + Portao;
            string[] p = v.Split(':');
            return (p[0], p.Length > 1 ? p[1] : "patio");
        }

        public static SetorDef Ler(Secao s)
        {
            var d = new SetorDef
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Tema = s.Texto("tema", "hera"),
                TamanhoCelula = s.Real("tamanho_celula", 7f),
                Nivel = s.Inteiro("nivel", 1),
                Grupos = s.Inteiro("grupos", 8),
                GruposNoturnos = s.Inteiro("grupos_noturnos", 4),
                Chefe = s.Texto("chefe"),
                Portao = s.Texto("portao"),
                Baus = s.Inteiro("baus", 3),
                Terminais = s.Inteiro("terminais", 1),
                Alavancas = s.Inteiro("alavancas", 1),
                MurosMoveis = s.Inteiro("muros_moveis", 6),
                Salas = s.Inteiro("salas", 2),
                Proximo = s.Texto("proximo"),
                StatusDoChefe = s.Texto("status_do_chefe"),
                RequerStatus = s.Texto("requer_status"),
                Musica = s.Texto("musica", "labirinto"),
                Volta = s.Texto("volta"),
                Descricao = s.Texto("descricao", ""),
            };
            (float c, float l) = s.Par("celulas", 9, 9);
            d.Colunas = (int)c;
            d.Linhas = (int)l;
            d.Inimigos = LerPesos(s, "inimigos");
            d.InimigosNoturnos = LerPesos(s, "inimigos_noturnos");
            d.Terminal.AddRange(s.Todos("terminal"));
            return d;
        }

        private static List<(string, int)> LerPesos(Secao s, string chave)
        {
            var lista = new List<(string, int)>();
            foreach (string parte in s.Lista(chave))
            {
                if (parte.Length == 0) continue;
                string[] p = parte.Split(':');
                lista.Add((p[0].Trim(), p.Length > 1 ? s.LerInteiro(p[1], chave) : 10));
            }
            return lista;
        }
    }

    /// <summary>Texto que se le num terminal da ARCA ou num papel achado. A barra-ene vira quebra de linha.</summary>
    public sealed class RegistroDef
    {
        public string Id;
        public string Titulo;
        public string Texto;
        public string Autor = "";

        public static RegistroDef Ler(Secao s)
        {
            return new RegistroDef
            {
                Id = s.TextoObrigatorio("id"),
                Titulo = s.TextoObrigatorio("titulo"),
                Texto = s.TextoObrigatorio("texto").Replace("\\n", "\n"),
                Autor = s.Texto("autor", ""),
            };
        }
    }
}
