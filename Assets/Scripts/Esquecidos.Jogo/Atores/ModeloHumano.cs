using System.Collections.Generic;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Dados;
using UnityEngine;

namespace Esquecidos.Jogo.Atores
{
    /// <summary>Tudo que define como um humano parece: aparencia, roupa por cima e o que leva na mao.</summary>
    public sealed class DadosDeHumano
    {
        public Aparencia Ap = new Aparencia();
        public string Tipo = "npc";
        public string Arma;
        public Color CorArma = Color.gray;
        public string Escudo;
        public Color CorEscudo = Color.gray;
        public string Chapeu;
        public Color CorChapeu = Color.gray;
        public string Veste;
        public Color? CorVeste;
        public Color? CorLuvas;
        public Color? CorBotas;
        public Color? CorOlhos;
        /// <summary>Modelo do item nos pes e nas maos ("botas", "tenis", "luvas", "manoplas"); nulo e o padrao.</summary>
        public string ModeloBotas;
        public string ModeloLuvas;

        public string Chave()
        {
            return string.Join("|", Ap.Pele, Ap.Cabelo, Ap.Penteado, Ap.Roupa, Ap.Calca, Ap.Botas, Ap.Acessorio, Ap.Porte.ToString("0.00"), Tipo, ModeloBotas, ModeloLuvas,
                Chapeu, Util.Hex(CorChapeu), Veste, CorVeste.HasValue ? Util.Hex(CorVeste.Value) : "-",
                CorLuvas.HasValue ? Util.Hex(CorLuvas.Value) : "-", CorBotas.HasValue ? Util.Hex(CorBotas.Value) : "-",
                CorOlhos.HasValue ? Util.Hex(CorOlhos.Value) : "-");
        }
    }

    /// <summary>As malhas de cada pedaco do corpo, ja pintadas, prontas para pendurar no esqueleto.</summary>
    public sealed class PecasDoHumano
    {
        public Mesh Quadril, Tronco, Cabeca, BracoE, BracoD, AntebracoE, AntebracoD, CoxaE, CoxaD, CanelaE, CanelaD;
    }

    /// <summary>
    /// Monta o humano em pecas: cada osso e uma malha propria, e o esqueleto e
    /// so a hierarquia de pivos. Medidas em metros para um adulto de ~1,78 m;
    /// "porte" alarga, "altura" estica.
    /// </summary>
    public static class ModeloHumano
    {
        public const float AlturaQuadril = 0.93f;
        public const float Tronco = 0.52f;
        public const float Ombro = 0.21f;
        public const float Braco = 0.28f;
        public const float Antebraco = 0.26f;
        public const float Coxa = 0.44f;
        public const float Canela = 0.42f;
        public const float LarguraQuadril = 0.1f;

        private static readonly Dictionary<string, PecasDoHumano> _cache = new Dictionary<string, PecasDoHumano>();

        public static PecasDoHumano Pecas(DadosDeHumano d)
        {
            string chave = d.Chave();
            if (_cache.TryGetValue(chave, out PecasDoHumano p)) return p;
            p = Construir(d);
            _cache[chave] = p;
            return p;
        }

        private static PecasDoHumano Construir(DadosDeHumano d)
        {
            Aparencia a = d.Ap;
            Color pele = Util.Cor(a.Pele);
            Color cabelo = Util.Cor(a.Cabelo);
            Color roupa = d.CorVeste ?? Util.Cor(a.Roupa);
            Color calca = Util.Cor(a.Calca);
            Color botas = d.CorBotas ?? Util.Cor(a.Botas);
            Color maos = d.CorLuvas ?? pele;
            float w = a.Porte;
            bool cinzento = d.Tipo == "cinzento" || d.Tipo == "bruto";
            bool guarda = d.Tipo == "guarda" || d.Tipo == "atirador";
            if (cinzento)
            {
                pele = Color.Lerp(pele, new Color(0.52f, 0.53f, 0.5f), 0.85f);
                roupa = Util.Escurecer(roupa, 0.55f);
            }
            var p = new PecasDoHumano();
            var c = new ConstrutorDeMalha();

            // Quadril: a bacia vestida.
            c.Cor(calca).Caixa(new Vector3(0, -0.04f, 0), new Vector3(0.34f * w, 0.2f, 0.22f * w), 0.05f);
            c.Cor(Util.Escurecer(calca, 0.6f)).Caixa(new Vector3(0, 0.07f, 0), new Vector3(0.35f * w, 0.05f, 0.23f * w), 0.015f);
            c.Cor(new Color(0.6f, 0.55f, 0.4f)).Caixa(new Vector3(0, 0.07f, 0.115f * w), new Vector3(0.05f, 0.04f, 0.01f));
            p.Quadril = c.Gerar("quadril");
            c.Limpar();

            // Tronco: cintura, peito, ombros arredondados. O pivo fica na cintura.
            c.Cor(roupa).Caixa(new Vector3(0, 0.13f, 0), new Vector3(0.31f * w, 0.26f, 0.2f * w), 0.06f);
            c.Caixa(new Vector3(0, 0.36f, 0.005f), new Vector3(0.4f * w, 0.26f, 0.24f * w), 0.08f);
            c.Esfera(new Vector3(-Ombro * w, 0.47f, 0), new Vector3(0.075f, 0.07f, 0.08f), 6, 8);
            c.Esfera(new Vector3(Ombro * w, 0.47f, 0), new Vector3(0.075f, 0.07f, 0.08f), 6, 8);
            c.Cor(pele).Tubo(new Vector3(0, 0.48f, 0), new Vector3(0, 0.6f, 0.01f), 0.055f, 0.05f, 8);
            if (!cinzento && !guarda) Camisa(c, d, roupa, w);
            Veste(c, d, roupa, w);
            Acessorio(c, a.Acessorio, w);
            if (cinzento)
            {
                // Rasgos: manchas de pele cinza aparecendo pela roupa.
                c.Cor(pele).Esfera(new Vector3(0.08f * w, 0.3f, 0.125f * w), new Vector3(0.06f, 0.08f, 0.01f), 4, 6);
                c.Cor(pele).Esfera(new Vector3(-0.1f * w, 0.18f, -0.105f * w), new Vector3(0.05f, 0.06f, 0.01f), 4, 6);
            }
            p.Tronco = c.Gerar("tronco");
            c.Limpar();

            // Cabeca, com pivo na base do pescoco.
            Cabeca(c, d, pele, cabelo, cinzento, guarda);
            p.Cabeca = c.Gerar("cabeca");
            c.Limpar();

            for (int lado = -1; lado <= 1; lado += 2)
            {
                bool mangaLonga = d.Veste == "jaqueta" || d.Veste == "jaleco" || guarda || d.Veste == "peitoral";
                Color manga = d.Veste == "jaleco" ? new Color(0.94f, 0.94f, 0.95f) : roupa;
                c.Cor(manga).Capsula(Vector3.zero, new Vector3(0, -Braco, 0), 0.056f * w, 0.05f * w, 8);
                if (!mangaLonga && !cinzento)
                {
                    // Barra da manga curta: um anel mais largo que o braco, e o braco de fora.
                    c.Cor(Util.Escurecer(manga, 0.82f)).Tubo(new Vector3(0, -Braco * 0.55f, 0), new Vector3(0, -Braco * 0.68f, 0), 0.062f * w, 0.06f * w, 8);
                    c.Cor(pele).Capsula(new Vector3(0, -Braco * 0.68f, 0), new Vector3(0, -Braco, 0), 0.047f * w, 0.046f * w, 8);
                }
                if (guarda) c.Cor(new Color(0.88f, 0.9f, 0.92f)).Esfera(new Vector3(0, -0.02f, 0), new Vector3(0.09f, 0.07f, 0.09f), 5, 8);
                if (d.Veste == "peitoral")
                {
                    // Ombreira de placas, a peca que mais muda a silhueta.
                    c.Cor(Util.Escurecer(roupa, 0.85f)).Esfera(new Vector3(lado * 0.015f, -0.01f, 0), new Vector3(0.095f, 0.07f, 0.095f), 5, 8);
                    c.Cor(Util.Escurecer(roupa, 0.6f)).Tubo(new Vector3(0, -0.045f, 0), new Vector3(0, -0.06f, 0), 0.09f, 0.086f, 8);
                }
                if (d.Veste == "jaqueta") c.Cor(Util.Escurecer(roupa, 0.7f)).Tubo(new Vector3(0, -Braco + 0.02f, 0), new Vector3(0, -Braco - 0.02f, 0), 0.058f * w, 0.058f * w, 8);
                Mesh braco = c.Gerar("braco");
                c.Limpar();
                c.Cor(mangaLonga ? manga : pele).Capsula(Vector3.zero, new Vector3(0, -Antebraco, 0), 0.048f * w, 0.04f * w, 8);
                if (mangaLonga) c.Cor(Util.Escurecer(manga, 0.75f)).Tubo(new Vector3(0, -Antebraco + 0.035f, 0), new Vector3(0, -Antebraco + 0.005f, 0), 0.047f * w, 0.046f * w, 8);
                Mao(c, d.ModeloLuvas, maos, lado);
                Mesh antebraco = c.Gerar("antebraco");
                c.Limpar();
                if (lado < 0) { p.BracoE = braco; p.AntebracoE = antebraco; }
                else { p.BracoD = braco; p.AntebracoD = antebraco; }

                c.Cor(calca).Capsula(Vector3.zero, new Vector3(0, -Coxa, 0), 0.08f * w, 0.065f * w, 8);
                // Bolso lateral: a calca deixa de ser um tubo liso.
                c.Cor(Util.Escurecer(calca, 0.8f)).Caixa(new Vector3(lado * 0.07f * w, -0.16f, 0), new Vector3(0.02f, 0.11f, 0.09f), 0.01f);
                Mesh coxa = c.Gerar("coxa");
                c.Limpar();
                c.Cor(calca).Capsula(Vector3.zero, new Vector3(0, -Canela + 0.1f, 0), 0.062f * w, 0.05f * w, 8);
                // Joelho gasto, mais escuro.
                c.Cor(Util.Escurecer(calca, 0.78f)).Esfera(new Vector3(0, -0.01f, 0.045f * w), new Vector3(0.05f * w, 0.05f, 0.025f), 4, 6);
                Calcado(c, d.ModeloBotas, botas, w);
                Mesh canela = c.Gerar("canela");
                c.Limpar();
                if (lado < 0) { p.CoxaE = coxa; p.CanelaE = canela; }
                else { p.CoxaD = coxa; p.CanelaD = canela; }
            }
            return p;
        }

        /// <summary>Gola, barra e bolso: a roupa de baixo de todo mundo.</summary>
        private static void Camisa(ConstrutorDeMalha c, DadosDeHumano d, Color roupa, float w)
        {
            Color escura = Util.Escurecer(roupa, 0.78f);
            // Gola em V aberta, duas abas inclinadas.
            for (int l = -1; l <= 1; l += 2)
            {
                c.Empurrar(new Vector3(l * 0.045f, 0.47f, 0.095f * w), Quaternion.Euler(-20, 0, l * 35));
                c.Cor(escura).Caixa(Vector3.zero, new Vector3(0.07f, 0.03f, 0.04f), 0.008f);
                c.Puxar();
            }
            // Barra da camisa por cima do cinto.
            c.Cor(escura).Tubo(new Vector3(0, 0.025f, 0), new Vector3(0, 0.055f, 0), 0.165f * w, 0.165f * w, 12);
            if (d.Veste == null && d.Ap.Acessorio != "avental" && d.Ap.Acessorio != "avental_couro")
            {
                c.Cor(escura).Caixa(new Vector3(-0.09f * w, 0.36f, 0.123f * w), new Vector3(0.07f, 0.075f, 0.01f), 0.004f);
                c.Cor(Util.Escurecer(roupa, 0.65f));
                for (int i = 0; i < 3; i++) c.Esfera(new Vector3(0, 0.2f + i * 0.09f, 0.122f * w), Vector3.one * 0.008f, 2, 4);
            }
        }

        /// <summary>Mao com dedos juntos e polegar; luva com punho; manopla de placas, maior.</summary>
        private static void Mao(ConstrutorDeMalha c, string modelo, Color cor, int lado)
        {
            float y = -Antebraco - 0.045f;
            if (modelo == "manoplas")
            {
                c.Cor(cor).Tubo(new Vector3(0, -Antebraco + 0.06f, 0), new Vector3(0, -Antebraco - 0.01f, 0), 0.05f, 0.056f, 8);
                c.Caixa(new Vector3(0, y - 0.005f, 0.012f), new Vector3(0.09f, 0.1f, 0.075f), 0.02f);
                c.Cor(Util.Escurecer(cor, 0.6f)).Caixa(new Vector3(0, y + 0.02f, 0.05f), new Vector3(0.092f, 0.012f, 0.012f));
                c.Cor(cor).Esfera(new Vector3(lado * -0.045f, y + 0.015f, 0.04f), new Vector3(0.022f, 0.035f, 0.022f), 3, 5);
                return;
            }
            if (modelo == "luvas") c.Cor(Util.Escurecer(cor, 0.8f)).Tubo(new Vector3(0, -Antebraco + 0.03f, 0), new Vector3(0, -Antebraco - 0.005f, 0), 0.046f, 0.048f, 8);
            c.Cor(cor).Esfera(new Vector3(0, y, 0.01f), new Vector3(0.05f, 0.06f, 0.045f), 5, 8);
            c.Esfera(new Vector3(0, y - 0.045f, 0.022f), new Vector3(0.042f, 0.03f, 0.03f), 4, 6);
            c.Esfera(new Vector3(lado * -0.035f, y + 0.015f, 0.035f), new Vector3(0.018f, 0.03f, 0.018f), 3, 5);
        }

        /// <summary>Bota de cano alto com cadarco, tenis de sola branca, ou o calcado comum.</summary>
        private static void Calcado(ConstrutorDeMalha c, string modelo, Color cor, float w)
        {
            float pe = -Canela;
            switch (modelo)
            {
                case "tenis":
                    c.Cor(cor).Tubo(new Vector3(0, pe + 0.07f, 0), new Vector3(0, pe - 0.02f, 0), 0.056f * w, 0.055f * w, 8);
                    c.Caixa(new Vector3(0, pe - 0.03f, 0.05f), new Vector3(0.1f * w, 0.065f, 0.24f), 0.03f);
                    c.Cor(new Color(0.93f, 0.92f, 0.88f)).Caixa(new Vector3(0, pe - 0.066f, 0.05f), new Vector3(0.106f * w, 0.022f, 0.25f), 0.008f);
                    c.Cor(Util.Escurecer(cor, 0.55f)).Caixa(new Vector3(0.052f * w, pe - 0.035f, 0.04f), new Vector3(0.006f, 0.02f, 0.14f));
                    c.Caixa(new Vector3(-0.052f * w, pe - 0.035f, 0.04f), new Vector3(0.006f, 0.02f, 0.14f));
                    break;
                case "botas":
                    c.Cor(cor).Tubo(new Vector3(0, pe + 0.22f, 0), new Vector3(0, pe - 0.02f, 0), 0.064f * w, 0.058f * w, 8);
                    c.Cor(Util.Escurecer(cor, 0.75f)).Tubo(new Vector3(0, pe + 0.22f, 0), new Vector3(0, pe + 0.19f, 0), 0.068f * w, 0.068f * w, 8);
                    c.Cor(cor).Caixa(new Vector3(0, pe - 0.035f, 0.055f), new Vector3(0.11f * w, 0.075f, 0.26f), 0.03f);
                    c.Cor(Util.Escurecer(cor, 0.45f)).Caixa(new Vector3(0, pe - 0.072f, 0.055f), new Vector3(0.114f * w, 0.02f, 0.265f));
                    c.Cor(Color.Lerp(cor, Color.white, 0.35f));
                    for (int i = 0; i < 3; i++) c.Caixa(new Vector3(0, pe + 0.04f + i * 0.05f, 0.06f * w), new Vector3(0.05f, 0.008f, 0.01f));
                    break;
                default:
                    c.Cor(cor).Tubo(new Vector3(0, pe + 0.16f, 0), new Vector3(0, pe - 0.02f, 0), 0.058f * w, 0.055f * w, 8);
                    c.Caixa(new Vector3(0, pe - 0.035f, 0.05f), new Vector3(0.1f * w, 0.07f, 0.24f), 0.025f);
                    c.Cor(Util.Escurecer(cor, 0.5f)).Caixa(new Vector3(0, pe - 0.068f, 0.05f), new Vector3(0.104f * w, 0.015f, 0.245f));
                    break;
            }
        }

        private static void Veste(ConstrutorDeMalha c, DadosDeHumano d, Color roupa, float w)
        {
            switch (d.Veste)
            {
                case "colete":
                    c.Cor(Util.Escurecer(roupa, 0.8f)).Caixa(new Vector3(0, 0.3f, 0.005f), new Vector3(0.42f * w, 0.36f, 0.26f * w), 0.06f);
                    c.Cor(new Color(0.75f, 0.7f, 0.55f)).Caixa(new Vector3(0.09f * w, 0.33f, 0.135f * w), new Vector3(0.08f, 0.09f, 0.01f));
                    // Bolsos de baixo e as alcas sobre os ombros.
                    c.Cor(Util.Escurecer(roupa, 0.62f));
                    c.Caixa(new Vector3(-0.1f * w, 0.18f, 0.137f * w), new Vector3(0.09f, 0.08f, 0.02f), 0.01f);
                    c.Caixa(new Vector3(0.1f * w, 0.18f, 0.137f * w), new Vector3(0.09f, 0.08f, 0.02f), 0.01f);
                    c.Caixa(new Vector3(-0.11f * w, 0.48f, 0), new Vector3(0.06f, 0.02f, 0.26f * w), 0.008f);
                    c.Caixa(new Vector3(0.11f * w, 0.48f, 0), new Vector3(0.06f, 0.02f, 0.26f * w), 0.008f);
                    break;
                case "peitoral":
                    c.Cor(roupa).Caixa(new Vector3(0, 0.34f, 0.01f), new Vector3(0.44f * w, 0.32f, 0.28f * w), 0.05f);
                    c.Cor(Util.Escurecer(roupa, 0.6f));
                    for (int i = 0; i < 3; i++) c.Caixa(new Vector3(0, 0.22f + i * 0.1f, 0.15f * w), new Vector3(0.36f * w, 0.012f, 0.01f));
                    break;
                case "jaqueta":
                    c.Cor(Util.Escurecer(roupa, 0.9f)).Caixa(new Vector3(0, 0.08f, 0), new Vector3(0.34f * w, 0.1f, 0.23f * w), 0.04f);
                    c.Cor(Util.Escurecer(roupa, 0.6f)).Caixa(new Vector3(0, 0.3f, 0.125f * w), new Vector3(0.02f, 0.4f, 0.01f));
                    // Gola alta e bolsos com aba.
                    c.Cor(Util.Escurecer(roupa, 0.85f)).Tubo(new Vector3(0, 0.46f, -0.005f), new Vector3(0, 0.54f, 0.0f), 0.085f * w, 0.075f * w, 10);
                    c.Cor(Util.Escurecer(roupa, 0.7f));
                    c.Caixa(new Vector3(-0.1f * w, 0.17f, 0.124f * w), new Vector3(0.08f, 0.015f, 0.02f), 0.004f);
                    c.Caixa(new Vector3(0.1f * w, 0.17f, 0.124f * w), new Vector3(0.08f, 0.015f, 0.02f), 0.004f);
                    break;
                case "jaleco":
                    c.Cor(new Color(0.94f, 0.94f, 0.95f)).Caixa(new Vector3(0, 0.12f, 0), new Vector3(0.36f * w, 0.36f, 0.25f * w), 0.05f);
                    c.Caixa(new Vector3(0, -0.12f, 0), new Vector3(0.38f * w, 0.22f, 0.25f * w), 0.04f);
                    break;
            }
        }

        private static void Acessorio(ConstrutorDeMalha c, string ac, float w)
        {
            switch (ac)
            {
                case "mochila":
                    c.Cor(new Color(0.32f, 0.3f, 0.22f)).Caixa(new Vector3(0, 0.3f, -0.19f * w), new Vector3(0.3f, 0.36f, 0.16f), 0.04f);
                    c.Cor(new Color(0.22f, 0.2f, 0.15f)).Caixa(new Vector3(0, 0.15f, -0.27f * w), new Vector3(0.24f, 0.12f, 0.06f), 0.02f);
                    c.Cor(new Color(0.55f, 0.5f, 0.42f)).Tubo(new Vector3(-0.12f, 0.5f, -0.25f * w), new Vector3(0.12f, 0.5f, -0.25f * w), 0.04f, 0.04f, 6);
                    break;
                case "avental":
                    c.Cor(new Color(0.92f, 0.9f, 0.84f)).Caixa(new Vector3(0, 0.1f, 0.115f * w), new Vector3(0.3f * w, 0.5f, 0.02f), 0.01f);
                    break;
                case "avental_couro":
                    c.Cor(new Color(0.36f, 0.24f, 0.16f)).Caixa(new Vector3(0, 0.12f, 0.125f * w), new Vector3(0.34f * w, 0.56f, 0.025f), 0.01f);
                    c.Cor(new Color(0.6f, 0.6f, 0.62f)).Caixa(new Vector3(0.08f, 0.0f, 0.14f * w), new Vector3(0.06f, 0.08f, 0.02f));
                    break;
            }
        }

        private static void Cabeca(ConstrutorDeMalha c, DadosDeHumano d, Color pele, Color cabelo, bool cinzento, bool guarda)
        {
            Aparencia a = d.Ap;
            float y = 0.15f;
            c.Cor(pele).Esfera(new Vector3(0, y, 0.005f), new Vector3(0.105f, 0.125f, 0.112f), 10, 14);
            // Queixo e mandibula.
            c.Esfera(new Vector3(0, y - 0.07f, 0.035f), new Vector3(0.075f, 0.06f, 0.07f), 6, 10);
            // Orelhas e nariz.
            c.Esfera(new Vector3(-0.103f, y - 0.005f, 0), new Vector3(0.018f, 0.032f, 0.022f), 4, 6);
            c.Esfera(new Vector3(0.103f, y - 0.005f, 0), new Vector3(0.018f, 0.032f, 0.022f), 4, 6);
            // Nariz: ponte e ponta.
            c.Cor(Util.Escurecer(pele, 0.94f)).Esfera(new Vector3(0, y, 0.108f), new Vector3(0.012f, 0.03f, 0.014f), 4, 6);
            c.Esfera(new Vector3(0, y - 0.02f, 0.114f), new Vector3(0.019f, 0.016f, 0.02f), 4, 6);
            // Bochechas levemente coradas.
            c.Cor(Color.Lerp(pele, new Color(0.85f, 0.4f, 0.35f), cinzento ? 0f : 0.14f));
            c.Esfera(new Vector3(-0.052f, y - 0.022f, 0.082f), new Vector3(0.03f, 0.022f, 0.02f), 4, 6);
            c.Esfera(new Vector3(0.052f, y - 0.022f, 0.082f), new Vector3(0.03f, 0.022f, 0.02f), 4, 6);
            // Olhos: branco, iris, brilho, palpebra por cima. Olho de cinzento brilha.
            Color olho = d.CorOlhos ?? new Color(0.1f, 0.07f, 0.05f);
            bool brilha = cinzento || d.CorOlhos.HasValue;
            for (int l = -1; l <= 1; l += 2)
            {
                c.Cor(new Color(0.92f, 0.9f, 0.86f)).Esfera(new Vector3(l * 0.038f, y + 0.015f, 0.096f), new Vector3(0.02f, 0.014f, 0.012f), 4, 6);
                if (brilha) c.Cor(olho, 0f);
                else c.Cor(olho);
                c.Esfera(new Vector3(l * 0.038f, y + 0.014f, 0.105f), new Vector3(0.011f, 0.012f, 0.006f), 3, 5);
                if (!brilha) c.Cor(new Color(0.95f, 0.95f, 0.95f)).Esfera(new Vector3(l * 0.038f + 0.004f, y + 0.019f, 0.11f), Vector3.one * 0.0035f, 2, 4);
                c.Cor(Util.Escurecer(pele, 0.82f)).Caixa(new Vector3(l * 0.038f, y + 0.027f, 0.1f), new Vector3(0.042f, 0.007f, 0.016f), 0.003f);
                // Sobrancelha.
                c.Cor(Util.Escurecer(cabelo, 0.8f)).Caixa(new Vector3(l * 0.04f, y + 0.045f, 0.1f), new Vector3(0.04f, 0.009f, 0.012f), 0.002f);
            }
            // Boca: linha escura e labio de baixo.
            c.Cor(Util.Escurecer(pele, 0.55f)).Caixa(new Vector3(0, y - 0.056f, 0.101f), new Vector3(0.038f, 0.006f, 0.01f));
            c.Cor(Color.Lerp(pele, new Color(0.7f, 0.32f, 0.3f), 0.35f)).Caixa(new Vector3(0, y - 0.064f, 0.099f), new Vector3(0.03f, 0.009f, 0.012f), 0.003f);

            if (guarda)
            {
                // Capacete branco fechado com visor que brilha.
                c.Cor(new Color(0.88f, 0.9f, 0.92f)).Esfera(new Vector3(0, y + 0.01f, 0), new Vector3(0.13f, 0.15f, 0.135f), 8, 12);
                c.Cor(d.CorOlhos ?? new Color(0.3f, 0.75f, 1f), 0f).Caixa(new Vector3(0, y + 0.02f, 0.118f), new Vector3(0.18f, 0.035f, 0.03f), 0.01f);
                return;
            }

            if (!string.IsNullOrEmpty(d.Chapeu))
            {
                Chapeu(c, d.Chapeu, d.CorChapeu, y, cabelo);
                return;
            }
            Penteado(c, a.Penteado, cabelo, y);
            if (a.Acessorio == "oculos")
            {
                c.Cor(new Color(0.15f, 0.12f, 0.1f));
                c.Toro(new Vector3(-0.038f, y + 0.015f, 0.112f), 0.022f, 0.004f, 10, 4);
                c.Toro(new Vector3(0.038f, y + 0.015f, 0.112f), 0.022f, 0.004f, 10, 4);
            }
        }

        private static void Penteado(ConstrutorDeMalha c, string estilo, Color cor, float y)
        {
            c.Cor(cor);
            switch (estilo)
            {
                case "careca":
                    return;
                case "raspado":
                    c.Esfera(new Vector3(0, y + 0.025f, -0.005f), new Vector3(0.108f, 0.11f, 0.114f), 8, 12);
                    return;
                case "longo":
                    c.Esfera(new Vector3(0, y + 0.03f, -0.01f), new Vector3(0.115f, 0.12f, 0.12f), 8, 12);
                    c.Caixa(new Vector3(0, y - 0.1f, -0.07f), new Vector3(0.2f, 0.26f, 0.08f), 0.04f);
                    return;
                case "tranca":
                    c.Esfera(new Vector3(0, y + 0.03f, -0.01f), new Vector3(0.113f, 0.118f, 0.12f), 8, 12);
                    for (int i = 0; i < 5; i++)
                        c.Esfera(new Vector3(0, y - 0.06f - i * 0.055f, -0.11f - i * 0.008f), new Vector3(0.03f, 0.035f, 0.03f), 4, 6);
                    return;
                case "coque":
                    c.Esfera(new Vector3(0, y + 0.03f, -0.01f), new Vector3(0.113f, 0.118f, 0.12f), 8, 12);
                    c.Esfera(new Vector3(0, y + 0.11f, -0.07f), new Vector3(0.055f, 0.05f, 0.055f), 6, 8);
                    return;
                case "rabo":
                    c.Esfera(new Vector3(0, y + 0.03f, -0.01f), new Vector3(0.113f, 0.118f, 0.12f), 8, 12);
                    c.Tubo(new Vector3(0, y + 0.03f, -0.11f), new Vector3(0, y - 0.15f, -0.16f), 0.035f, 0.015f, 6);
                    return;
                case "cacheado":
                    for (int i = 0; i < 14; i++)
                    {
                        float ang = i * 0.9f;
                        float alt = 0.04f + (i % 3) * 0.035f;
                        c.Esfera(new Vector3(Mathf.Cos(ang) * 0.085f, y + alt, Mathf.Sin(ang) * 0.085f - 0.015f), Vector3.one * 0.05f, 4, 6);
                    }
                    return;
                case "baguncado":
                    c.Esfera(new Vector3(0, y + 0.035f, -0.01f), new Vector3(0.115f, 0.115f, 0.12f), 8, 12);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * 0.8f;
                        Vector3 b = new Vector3(Mathf.Cos(ang) * 0.06f, y + 0.1f, Mathf.Sin(ang) * 0.06f - 0.02f);
                        c.Tubo(b, b + new Vector3(Mathf.Cos(ang) * 0.05f, 0.05f, Mathf.Sin(ang) * 0.05f), 0.025f, 0.0f, 5);
                    }
                    return;
                default:
                    c.Esfera(new Vector3(0, y + 0.035f, -0.01f), new Vector3(0.112f, 0.11f, 0.118f), 8, 12);
                    c.Caixa(new Vector3(0, y + 0.07f, 0.07f), new Vector3(0.17f, 0.05f, 0.06f), 0.02f);
                    // Costeletas e uma mecha caindo na testa.
                    c.Caixa(new Vector3(-0.1f, y - 0.01f, 0.03f), new Vector3(0.016f, 0.05f, 0.03f), 0.006f);
                    c.Caixa(new Vector3(0.1f, y - 0.01f, 0.03f), new Vector3(0.016f, 0.05f, 0.03f), 0.006f);
                    c.Tubo(new Vector3(0.02f, y + 0.09f, 0.09f), new Vector3(0.045f, y + 0.05f, 0.112f), 0.02f, 0.006f, 5);
                    return;
            }
        }

        private static void Chapeu(ConstrutorDeMalha c, string modelo, Color cor, float y, Color cabelo)
        {
            switch (modelo)
            {
                case "bandana":
                    c.Cor(cabelo).Esfera(new Vector3(0, y + 0.03f, -0.01f), new Vector3(0.112f, 0.11f, 0.118f), 8, 12);
                    c.Cor(cor).Tubo(new Vector3(0, y + 0.055f, -0.005f), new Vector3(0, y + 0.09f, -0.005f), 0.116f, 0.112f, 14);
                    c.Tubo(new Vector3(0, y + 0.07f, -0.11f), new Vector3(0.03f, y - 0.02f, -0.17f), 0.02f, 0.008f, 4);
                    break;
                case "capuz":
                    c.Cor(cor).Esfera(new Vector3(0, y + 0.02f, -0.02f), new Vector3(0.135f, 0.15f, 0.14f), 8, 12);
                    c.Caixa(new Vector3(0, y - 0.12f, -0.06f), new Vector3(0.24f, 0.08f, 0.16f), 0.03f);
                    break;
                case "capacete_obra":
                    c.Cor(cor).Esfera(new Vector3(0, y + 0.04f, 0), new Vector3(0.13f, 0.1f, 0.135f), 8, 12);
                    c.Cilindro(new Vector3(0, y + 0.03f, 0.01f), 0.15f, 0.012f, 14);
                    break;
                case "mascara":
                    c.Cor(cabelo).Esfera(new Vector3(0, y + 0.03f, -0.01f), new Vector3(0.112f, 0.11f, 0.118f), 8, 12);
                    c.Cor(cor).Esfera(new Vector3(0, y - 0.03f, 0.07f), new Vector3(0.09f, 0.08f, 0.06f), 6, 10);
                    c.Cor(new Color(0.55f, 0.75f, 0.8f), 0.4f).Esfera(new Vector3(-0.04f, y + 0.015f, 0.1f), Vector3.one * 0.025f, 4, 6);
                    c.Esfera(new Vector3(0.04f, y + 0.015f, 0.1f), Vector3.one * 0.025f, 4, 6);
                    c.Cor(Util.Escurecer(cor, 0.6f)).Tubo(new Vector3(0, y - 0.07f, 0.12f), new Vector3(0, y - 0.09f, 0.17f), 0.035f, 0.04f, 8);
                    break;
                case "capacete_sucata":
                    c.Cor(cor).Esfera(new Vector3(0, y + 0.03f, 0), new Vector3(0.13f, 0.13f, 0.14f), 8, 12);
                    c.Cor(Util.Escurecer(cor, 0.6f)).Caixa(new Vector3(0, y + 0.02f, 0.12f), new Vector3(0.16f, 0.02f, 0.03f));
                    c.Caixa(new Vector3(0, y + 0.13f, 0), new Vector3(0.03f, 0.04f, 0.2f), 0.01f);
                    break;
                case "capacete_arca":
                    c.Cor(cor).Esfera(new Vector3(0, y + 0.02f, 0), new Vector3(0.13f, 0.145f, 0.135f), 8, 12);
                    c.Cor(new Color(0.3f, 0.75f, 1f), 0f).Caixa(new Vector3(0, y + 0.02f, 0.118f), new Vector3(0.17f, 0.03f, 0.03f), 0.01f);
                    break;
                case "coroa":
                    c.Cor(cabelo).Esfera(new Vector3(0, y + 0.03f, -0.01f), new Vector3(0.112f, 0.11f, 0.118f), 8, 12);
                    c.Cor(cor);
                    for (int i = 0; i < 7; i++)
                    {
                        float ang = i / 7f * Mathf.PI * 2;
                        Vector3 b = new Vector3(Mathf.Cos(ang) * 0.1f, y + 0.08f, Mathf.Sin(ang) * 0.1f);
                        c.Tubo(b, b + new Vector3(Mathf.Cos(ang) * 0.03f, 0.1f, Mathf.Sin(ang) * 0.03f), 0.022f, 0.0f, 5);
                    }
                    break;
                default:
                    Penteado(c, "curto", cabelo, y);
                    break;
            }
        }
    }
}
