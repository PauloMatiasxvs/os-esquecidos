using System;
using System.Collections.Generic;
using Esquecidos.Nucleo.Dados;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Nucleo.Regras
{
    /// <summary>Os quatro atributos primarios, os mesmos papeis dos do Flare.</summary>
    public enum Atributo
    {
        Fisico,
        Mente,
        Ofensiva,
        Defesa,
    }

    /// <summary>
    /// Tudo que se soma numa ficha. Os quatro primeiros espelham os atributos
    /// para que um item possa dar "+3 Fisico" pelo mesmo caminho que da
    /// "+10 Vida".
    /// </summary>
    public enum Estat
    {
        Fisico,
        Mente,
        Ofensiva,
        Defesa,
        Vida,
        RegenVida,
        Energia,
        RegenEnergia,
        Precisao,
        Esquiva,
        Critico,
        DanoCritico,
        DanoCorpoMin,
        DanoCorpoMax,
        DanoDistMin,
        DanoDistMax,
        DanoTecMin,
        DanoTecMax,
        DanoPct,
        AbsorcaoMin,
        AbsorcaoMax,
        Bloqueio,
        Velocidade,
        VelocidadeAtaque,
        Tenacidade,
        ResVeneno,
        ResFogo,
        ResChoque,
        RouboVida,
        ReducaoRecarga,
        BonusXp,
        BonusSucata,
        AchadoRaro,
        Espinhos,
    }

    public static class Nomes
    {
        private static readonly Dictionary<string, Estat> _estatPorChave = new();
        private static readonly string[] _chaves;

        static Nomes()
        {
            Array valores = Enum.GetValues(typeof(Estat));
            _chaves = new string[valores.Length];
            foreach (Estat e in valores)
            {
                string chave = ParaChave(e.ToString());
                _chaves[(int)e] = chave;
                _estatPorChave[chave] = e;
            }
        }

        /// <summary>"DanoCorpoMin" vira "dano_corpo_min", a forma usada nos arquivos.</summary>
        public static string ParaChave(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public static string Chave(Estat e) => _chaves[(int)e];

        public static Estat EstatDe(string chave)
        {
            if (_estatPorChave.TryGetValue(chave.Trim().ToLowerInvariant(), out Estat e)) return e;
            throw new FormatException($"estatistica desconhecida: '{chave}'");
        }

        public static Atributo AtributoDe(string chave)
        {
            switch (chave.Trim().ToLowerInvariant())
            {
                case "fisico": return Atributo.Fisico;
                case "mente": return Atributo.Mente;
                case "ofensiva": return Atributo.Ofensiva;
                case "defesa": return Atributo.Defesa;
            }
            throw new FormatException($"atributo desconhecido: '{chave}'");
        }

        public static string Exibir(Atributo a)
        {
            switch (a)
            {
                case Atributo.Fisico: return T("Físico");
                case Atributo.Mente: return T("Mente");
                case Atributo.Ofensiva: return T("Ofensiva");
                default: return T("Defesa");
            }
        }

        /// <summary>Como a estatistica aparece numa dica de item. {0} e o valor.</summary>
        public static string Exibir(Estat e, float valor)
        {
            string v = valor >= 0 ? "+" + Formatar(valor) : Formatar(valor);
            switch (e)
            {
                case Estat.Fisico: return F("{0} Físico", v);
                case Estat.Mente: return $"{v} Mente";
                case Estat.Ofensiva: return $"{v} Ofensiva";
                case Estat.Defesa: return $"{v} Defesa";
                case Estat.Vida: return $"{v} Vida";
                case Estat.RegenVida: return F("{0} Vida por segundo", v);
                case Estat.Energia: return F("{0} Fôlego", v);
                case Estat.RegenEnergia: return F("{0} Fôlego por segundo", v);
                case Estat.Precisao: return F("{0} Precisão", v);
                case Estat.Esquiva: return $"{v} Esquiva";
                case Estat.Critico: return F("{0}% chance de crítico", v);
                case Estat.DanoCritico: return F("{0}% dano crítico", v);
                case Estat.DanoCorpoMin: return F("{0} dano corpo a corpo mínimo", v);
                case Estat.DanoCorpoMax: return F("{0} dano corpo a corpo máximo", v);
                case Estat.DanoDistMin: return F("{0} dano à distância mínimo", v);
                case Estat.DanoDistMax: return F("{0} dano à distância máximo", v);
                case Estat.DanoTecMin: return F("{0} dano de engenhoca mínimo", v);
                case Estat.DanoTecMax: return F("{0} dano de engenhoca máximo", v);
                case Estat.DanoPct: return $"{v}% dano";
                case Estat.AbsorcaoMin: return F("{0} absorção mínima", v);
                case Estat.AbsorcaoMax: return F("{0} absorção máxima", v);
                case Estat.Bloqueio: return $"{v}% bloqueio";
                case Estat.Velocidade: return F("{0}% velocidade de movimento", v);
                case Estat.VelocidadeAtaque: return F("{0}% velocidade de ataque", v);
                case Estat.Tenacidade: return $"{v} Tenacidade";
                case Estat.ResVeneno: return F("{0}% resistência a veneno", v);
                case Estat.ResFogo: return F("{0}% resistência a fogo", v);
                case Estat.ResChoque: return F("{0}% resistência a choque", v);
                case Estat.RouboVida: return F("{0}% do dano vira vida", v);
                case Estat.ReducaoRecarga: return F("{0}% recarga mais rápida", v);
                case Estat.BonusXp: return F("{0}% experiência", v);
                case Estat.BonusSucata: return F("{0}% sucata encontrada", v);
                case Estat.AchadoRaro: return F("{0}% chance de achado raro", v);
                case Estat.Espinhos: return F("{0} dano devolvido ao ser atingido", v);
            }
            return $"{v} {e}";
        }

        public static string Formatar(float valor)
        {
            return Math.Abs(valor - MathF.Round(valor)) < 0.05f
                ? MathF.Round(valor).ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                : valor.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Vetor de estatisticas: soma, copia e consulta por indice.</summary>
    public sealed class Ficha
    {
        public static readonly int Tamanho = Enum.GetValues(typeof(Estat)).Length;
        private readonly float[] _v = new float[Tamanho];

        public float this[Estat e]
        {
            get => _v[(int)e];
            set => _v[(int)e] = value;
        }

        public void Somar(Estat e, float valor) => _v[(int)e] += valor;

        public void Somar(Ficha outra)
        {
            for (int i = 0; i < Tamanho; i++) _v[i] += outra._v[i];
        }

        public void Zerar() => Array.Clear(_v, 0, _v.Length);

        public Ficha Copia()
        {
            var f = new Ficha();
            Array.Copy(_v, f._v, Tamanho);
            return f;
        }
    }

    /// <summary>
    /// As contas do personagem, lidas de dados/regras.txt. O formato e o do
    /// stats.txt do Flare: valor base, ganho por nivel e ganho por ponto de
    /// atributo, cada um numa linha.
    /// </summary>
    public sealed class Regras
    {
        public int PontosAtributoPorNivel { get; private set; } = 3;
        public int PontosPoderPorNivel { get; private set; } = 1;
        public int NivelMaximo { get; private set; } = 30;
        public int AtributoMaximo { get; private set; } = 60;
        public float XpBase { get; private set; } = 100;
        public float XpExpoente { get; private set; } = 1.55f;
        public float AcertoMinimo { get; private set; } = 5;
        public float AcertoMaximo { get; private set; } = 95;
        public float AbsorcaoMaximaPct { get; private set; } = 75;
        public float ResistenciaMaxima { get; private set; } = 75;
        public float MultiplicadorCritico { get; private set; } = 1.5f;
        public float PerdaSucataAoMorrer { get; private set; } = 0.15f;

        public Ficha Base { get; } = new();
        public Ficha PorNivel { get; } = new();
        private readonly Ficha[] _porAtributo = { new(), new(), new(), new() };

        public Ficha PorAtributo(Atributo a) => _porAtributo[(int)a];

        public static Regras Ler(Secao s)
        {
            var r = new Regras
            {
                PontosAtributoPorNivel = s.Inteiro("pontos_atributo_por_nivel", 3),
                PontosPoderPorNivel = s.Inteiro("pontos_poder_por_nivel", 1),
                NivelMaximo = s.Inteiro("nivel_maximo", 30),
                AtributoMaximo = s.Inteiro("atributo_maximo", 60),
                XpBase = s.Real("xp_base", 100),
                XpExpoente = s.Real("xp_expoente", 1.55f),
                AcertoMinimo = s.Real("acerto_minimo", 5),
                AcertoMaximo = s.Real("acerto_maximo", 95),
                AbsorcaoMaximaPct = s.Real("absorcao_maxima_pct", 75),
                ResistenciaMaxima = s.Real("resistencia_maxima", 75),
                MultiplicadorCritico = s.Real("multiplicador_critico", 1.5f),
                PerdaSucataAoMorrer = s.Real("perda_sucata_ao_morrer", 0.15f),
            };
            foreach (string linha in s.Todos("estat"))
            {
                string[] p = Secao.Partir(linha);
                r.Base.Somar(Nomes.EstatDe(p[0]), s.LerReal(p[1], "estat"));
            }
            foreach (string linha in s.Todos("estat_por_nivel"))
            {
                string[] p = Secao.Partir(linha);
                r.PorNivel.Somar(Nomes.EstatDe(p[0]), s.LerReal(p[1], "estat_por_nivel"));
            }
            foreach (string linha in s.Todos("estat_por_atributo"))
            {
                string[] p = Secao.Partir(linha);
                r._porAtributo[(int)Nomes.AtributoDe(p[0])].Somar(Nomes.EstatDe(p[1]), s.LerReal(p[2], "estat_por_atributo"));
            }
            return r;
        }

        /// <summary>Experiencia total para estar no nivel dado. Nivel 1 comeca em zero.</summary>
        public int XpTotalPara(int nivel)
        {
            if (nivel <= 1) return 0;
            return (int)MathF.Round(XpBase * MathF.Pow(nivel - 1, XpExpoente) / 5f) * 5;
        }

        public int NivelPara(int xp)
        {
            int n = 1;
            while (n < NivelMaximo && xp >= XpTotalPara(n + 1)) n++;
            return n;
        }

        /// <summary>
        /// A ficha completa: base, nivel, atributos (com o bonus de itens ja
        /// somado neles) e por fim os bonus diretos de equipamento e efeitos.
        /// </summary>
        public Ficha Calcular(int nivel, int[] atributos, Ficha bonus)
        {
            var f = Base.Copia();
            for (int i = 0; i < Ficha.Tamanho; i++)
            {
                var e = (Estat)i;
                f.Somar(e, PorNivel[e] * (nivel - 1));
            }
            for (int a = 0; a < 4; a++)
            {
                float total = atributos[a] + (bonus != null ? bonus[(Estat)a] : 0);
                f[(Estat)a] = total;
                Ficha por = _porAtributo[a];
                for (int i = 4; i < Ficha.Tamanho; i++) f.Somar((Estat)i, por[(Estat)i] * total);
            }
            if (bonus != null)
            {
                for (int i = 4; i < Ficha.Tamanho; i++) f.Somar((Estat)i, bonus[(Estat)i]);
            }
            if (f[Estat.DanoCorpoMax] < f[Estat.DanoCorpoMin]) f[Estat.DanoCorpoMax] = f[Estat.DanoCorpoMin];
            if (f[Estat.DanoDistMax] < f[Estat.DanoDistMin]) f[Estat.DanoDistMax] = f[Estat.DanoDistMin];
            if (f[Estat.DanoTecMax] < f[Estat.DanoTecMin]) f[Estat.DanoTecMax] = f[Estat.DanoTecMin];
            if (f[Estat.AbsorcaoMax] < f[Estat.AbsorcaoMin]) f[Estat.AbsorcaoMax] = f[Estat.AbsorcaoMin];
            return f;
        }
    }
}
