using System;
using System.Collections.Generic;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;

namespace Esquecidos.Nucleo.Regras
{
    public enum TipoPoder
    {
        /// <summary>Golpe em arco na frente de quem ataca.</summary>
        Corpo,
        /// <summary>Um ou mais projeteis que voam ate bater.</summary>
        Projetil,
        /// <summary>Area num ponto: explode depois de um atraso, ou fica queimando.</summary>
        Area,
        /// <summary>Avanco rapido ate o alvo, batendo em quem estiver no caminho.</summary>
        Investida,
        /// <summary>Rolamento com invulnerabilidade curta.</summary>
        Esquiva,
        Cura,
        /// <summary>Efeito em si mesmo (furia, pele de ferro, rapidez).</summary>
        Reforco,
        /// <summary>Area em volta de quem usa, sem dano obrigatorio: atordoa, provoca.</summary>
        Grito,
        Armadilha,
        Invocacao,
        Salto,
    }

    public enum FonteDeDano
    {
        Nenhuma,
        Corpo,
        Distancia,
        Tecnologia,
    }

    public enum TipoDeAlvo
    {
        Inimigo,
        Chao,
        Proprio,
    }

    public sealed class EfeitoDePoder
    {
        public string Nome;
        public float Duracao;
        public float Valor;
    }

    /// <summary>
    /// Um poder como esta em dados/poderes.txt. O mesmo formato serve para o
    /// jogador e para os inimigos, como no Flare. "por_rank" diz o que cresce
    /// a cada ponto gasto alem do primeiro.
    /// </summary>
    public sealed class Poder
    {
        public string Id;
        public string Nome;
        public string Descricao = "";
        public string Icone;
        public TipoPoder Tipo;
        public TipoDeAlvo Alvo = TipoDeAlvo.Inimigo;
        public string Arvore = "comum";
        public float Custo;
        public float Recarga;
        public float Antecipacao = 0.18f;
        public float Recuperacao = 0.22f;
        public FonteDeDano Fonte = FonteDeDano.Corpo;
        public float Multiplicador = 1f;
        public float DanoFixoMin, DanoFixoMax;
        public float Alcance = 2.2f;
        public float Angulo = 100f;
        public float Raio;
        public float Velocidade = 18f;
        public int Quantidade = 1;
        public float Espalhamento;
        public bool Perfura;
        public float Empurrao = 2f;
        public float Atraso;
        public float Duracao;
        public float Distancia;
        public string Elemento = "fisico";
        public EfeitoDePoder Efeito;
        public EfeitoDePoder EfeitoProprio;
        public HashSet<ClasseArma> ArmasExigidas = new();
        public bool UsaArma;
        public int NivelMinimo = 1;
        public string RequerPoder;
        public int Coluna, Linha;
        public int RankMaximo = 1;
        public string Animacao = "golpe";
        public string Som = "golpe";
        public string Vfx = "corte";
        public string Cor = "#ffffff";
        public string Invoca;
        public float RouboDeVida;
        public List<(string campo, float valor)> PorRank = new();

        public static Poder Ler(Secao s)
        {
            var p = new Poder
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Descricao = s.Texto("descricao", ""),
                Icone = s.Texto("icone", s.Texto("id")),
                Tipo = LerTipo(s.TextoObrigatorio("tipo"), s),
                Alvo = LerAlvo(s.Texto("alvo", "inimigo"), s),
                Arvore = s.Texto("arvore", "comum"),
                Custo = s.Real("custo"),
                Recarga = s.Real("recarga"),
                Antecipacao = s.Real("antecipacao", 0.18f),
                Recuperacao = s.Real("recuperacao", 0.22f),
                Fonte = LerFonte(s.Texto("dano", "corpo"), s),
                Multiplicador = s.Real("multiplicador", 1f),
                Alcance = s.Real("alcance", 2.2f),
                Angulo = s.Real("angulo", 100f),
                Raio = s.Real("raio"),
                Velocidade = s.Real("velocidade", 18f),
                Quantidade = s.Inteiro("quantidade", 1),
                Espalhamento = s.Real("espalhamento"),
                Perfura = s.Logico("perfura"),
                Empurrao = s.Real("empurrao", 2f),
                Atraso = s.Real("atraso"),
                Duracao = s.Real("duracao"),
                Distancia = s.Real("distancia"),
                Elemento = s.Texto("elemento", "fisico"),
                UsaArma = s.Logico("usa_arma"),
                NivelMinimo = s.Inteiro("nivel_minimo", 1),
                RequerPoder = s.Texto("requer_poder"),
                RankMaximo = s.Inteiro("rank_maximo", 1),
                Animacao = s.Texto("animacao", "golpe"),
                Som = s.Texto("som", "golpe"),
                Vfx = s.Texto("vfx", "corte"),
                Cor = s.Texto("cor", "#ffffff"),
                Invoca = s.Texto("invoca"),
                RouboDeVida = s.Real("roubo_vida"),
            };
            (p.DanoFixoMin, p.DanoFixoMax) = s.Par("dano_fixo");
            p.Efeito = LerEfeito(s, "efeito");
            p.EfeitoProprio = LerEfeito(s, "efeito_proprio");
            foreach (string a in s.Lista("requer_arma")) p.ArmasExigidas.Add(ItemBase.LerClasseArma(a, s));
            if (s.Tem("arvore_pos"))
            {
                (float c, float l) = s.Par("arvore_pos");
                p.Coluna = (int)c;
                p.Linha = (int)l;
            }
            foreach (string linha in s.Todos("por_rank"))
            {
                string[] partes = Secao.Partir(linha);
                p.PorRank.Add((partes[0], s.LerReal(partes[1], "por_rank")));
            }
            return p;
        }

        private static EfeitoDePoder LerEfeito(Secao s, string chave)
        {
            if (!s.Tem(chave)) return null;
            string[] p = s.Lista(chave);
            return new EfeitoDePoder
            {
                Nome = p[0],
                Duracao = p.Length > 1 ? s.LerReal(p[1], chave) : 1f,
                Valor = p.Length > 2 ? s.LerReal(p[2], chave) : 0f,
            };
        }

        /// <summary>
        /// Copia com os ganhos de rank aplicados. A simulacao so enxerga o poder
        /// ja resolvido, nunca o rank.
        /// </summary>
        public Poder NoRank(int rank)
        {
            if (rank <= 1 || PorRank.Count == 0) return this;
            var p = (Poder)MemberwiseClone();
            if (Efeito != null) p.Efeito = new EfeitoDePoder { Nome = Efeito.Nome, Duracao = Efeito.Duracao, Valor = Efeito.Valor };
            if (EfeitoProprio != null) p.EfeitoProprio = new EfeitoDePoder { Nome = EfeitoProprio.Nome, Duracao = EfeitoProprio.Duracao, Valor = EfeitoProprio.Valor };
            int extra = rank - 1;
            foreach ((string campo, float valor) in PorRank)
            {
                float d = valor * extra;
                switch (campo)
                {
                    case "multiplicador": p.Multiplicador += d; break;
                    case "raio": p.Raio += d; break;
                    case "alcance": p.Alcance += d; break;
                    case "quantidade": p.Quantidade += (int)MathF.Round(d); break;
                    case "recarga": p.Recarga = MathF.Max(0.2f, p.Recarga + d); break;
                    case "custo": p.Custo = MathF.Max(0, p.Custo + d); break;
                    case "duracao": p.Duracao += d; break;
                    case "distancia": p.Distancia += d; break;
                    case "dano_fixo": p.DanoFixoMin += d; p.DanoFixoMax += d; break;
                    case "efeito_duracao": if (p.Efeito != null) p.Efeito.Duracao += d; break;
                    case "efeito_valor": if (p.Efeito != null) p.Efeito.Valor += d; break;
                    case "efeito_proprio_duracao": if (p.EfeitoProprio != null) p.EfeitoProprio.Duracao += d; break;
                    case "efeito_proprio_valor": if (p.EfeitoProprio != null) p.EfeitoProprio.Valor += d; break;
                    case "roubo_vida": p.RouboDeVida += d; break;
                    default: throw new FormatException($"poder '{Id}': por_rank desconhecido '{campo}'");
                }
            }
            return p;
        }

        public bool AceitaArma(ClasseArma arma)
        {
            return ArmasExigidas.Count == 0 || ArmasExigidas.Contains(arma);
        }

        private static TipoPoder LerTipo(string v, Secao s)
        {
            switch (v)
            {
                case "corpo": return TipoPoder.Corpo;
                case "projetil": return TipoPoder.Projetil;
                case "area": return TipoPoder.Area;
                case "investida": return TipoPoder.Investida;
                case "esquiva": return TipoPoder.Esquiva;
                case "cura": return TipoPoder.Cura;
                case "reforco": return TipoPoder.Reforco;
                case "grito": return TipoPoder.Grito;
                case "armadilha": return TipoPoder.Armadilha;
                case "invocacao": return TipoPoder.Invocacao;
                case "salto": return TipoPoder.Salto;
            }
            throw new FormatException($"{s.Arquivo}:{s.Linha} tipo de poder desconhecido '{v}'");
        }

        private static TipoDeAlvo LerAlvo(string v, Secao s)
        {
            switch (v)
            {
                case "inimigo": return TipoDeAlvo.Inimigo;
                case "chao": return TipoDeAlvo.Chao;
                case "proprio": return TipoDeAlvo.Proprio;
            }
            throw new FormatException($"{s.Arquivo}:{s.Linha} alvo desconhecido '{v}'");
        }

        private static FonteDeDano LerFonte(string v, Secao s)
        {
            switch (v)
            {
                case "nenhum": return FonteDeDano.Nenhuma;
                case "corpo": return FonteDeDano.Corpo;
                case "distancia": return FonteDeDano.Distancia;
                case "tec": return FonteDeDano.Tecnologia;
            }
            throw new FormatException($"{s.Arquivo}:{s.Linha} fonte de dano desconhecida '{v}'");
        }
    }

    /// <summary>Classe de personagem: atributos iniciais, poderes e equipamento de partida.</summary>
    public sealed class Classe
    {
        public string Id;
        public string Nome;
        public string Descricao;
        public string Papel;
        public int[] Atributos = new int[4];
        public List<string> Poderes = new();
        public List<string> Equipamento = new();
        public List<(string item, int qtd)> Mochila = new();
        public string[] Barra = new string[8];
        public string CorRoupa;

        public static Classe Ler(Secao s)
        {
            var c = new Classe
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Descricao = s.Texto("descricao", ""),
                Papel = s.Texto("papel", ""),
                CorRoupa = s.Texto("cor_roupa", "#556070"),
            };
            foreach (string linha in s.Todos("atributo"))
            {
                string[] p = Secao.Partir(linha);
                c.Atributos[(int)Nomes.AtributoDe(p[0])] = s.LerInteiro(p[1], "atributo");
            }
            c.Poderes.AddRange(s.Lista("poderes"));
            c.Equipamento.AddRange(s.Lista("equipamento"));
            foreach (string linha in s.Todos("mochila"))
            {
                string[] p = Secao.Partir(linha);
                c.Mochila.Add((p[0], p.Length > 1 ? s.LerInteiro(p[1], "mochila") : 1));
            }
            string[] barra = s.Lista("barra");
            for (int i = 0; i < barra.Length && i < c.Barra.Length; i++)
                c.Barra[i] = string.IsNullOrEmpty(barra[i]) || barra[i] == "-" ? null : barra[i];
            return c;
        }
    }
}
