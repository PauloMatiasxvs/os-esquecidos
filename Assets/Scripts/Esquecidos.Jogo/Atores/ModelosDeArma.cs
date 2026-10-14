using System.Collections.Generic;
using Esquecidos.Motor.Malhas;
using UnityEngine;

namespace Esquecidos.Jogo.Atores
{
    /// <summary>
    /// Armas, escudos e chapeus montados em codigo. A arma sai com a empunhadura
    /// na origem e a ponta para +Z: a mao do boneco so precisa apontar.
    /// </summary>
    public static class ModelosDeArma
    {
        private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

        private static readonly Color Madeira = new Color(0.45f, 0.3f, 0.18f);
        private static readonly Color Ferro = new Color(0.42f, 0.42f, 0.44f);
        private static readonly Color Couro = new Color(0.28f, 0.18f, 0.12f);
        private static readonly Color Fita = new Color(0.55f, 0.52f, 0.45f);

        public static Mesh Arma(string modelo, Color cor)
        {
            string chave = "arma:" + modelo + ":" + Util.Hex(cor);
            if (_cache.TryGetValue(chave, out Mesh m)) return m;
            var c = new ConstrutorDeMalha();
            switch (modelo)
            {
                case "faca":
                case "faca_osso":
                    c.Cor(modelo == "faca_osso" ? new Color(0.8f, 0.76f, 0.66f) : Couro).Caixa(new Vector3(0, 0, -0.02f), new Vector3(0.03f, 0.035f, 0.12f), 0.008f);
                    c.Cor(cor).Caixa(new Vector3(0, 0, 0.16f), new Vector3(0.008f, 0.045f, 0.24f), 0.003f);
                    c.TrianguloChapadoSimples(new Vector3(0, 0.022f, 0.28f), new Vector3(0, -0.022f, 0.28f), new Vector3(0, 0.0f, 0.33f));
                    break;
                case "machete":
                    c.Cor(Couro).Caixa(new Vector3(0, 0, -0.03f), new Vector3(0.032f, 0.038f, 0.14f), 0.008f);
                    c.Cor(cor).Caixa(new Vector3(0, 0.01f, 0.24f), new Vector3(0.008f, 0.07f, 0.42f), 0.003f);
                    break;
                case "lamina_tecelao":
                    c.Cor(Couro).Caixa(new Vector3(0, 0, -0.03f), new Vector3(0.03f, 0.035f, 0.14f), 0.008f);
                    for (int i = 0; i < 3; i++)
                        c.Cor(Color.Lerp(cor, Color.black, i * 0.2f)).Tubo(new Vector3(0, i * 0.02f, 0.05f + i * 0.13f), new Vector3(0, 0.02f + i * 0.025f, 0.18f + i * 0.13f), 0.025f - i * 0.006f, 0.018f - i * 0.005f, 6);
                    break;
                case "cano":
                    c.Cor(Fita).Tubo(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.08f), 0.03f, 0.03f, 8);
                    c.Cor(cor).Tubo(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.85f), 0.025f, 0.025f, 8);
                    c.Cor(Util.Escurecer(cor, 0.7f)).Tubo(new Vector3(0, 0, 0.8f), new Vector3(0, 0, 0.9f), 0.04f, 0.04f, 8);
                    break;
                case "taco":
                    c.Cor(Madeira).Tubo(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.8f), 0.025f, 0.05f, 8);
                    c.Cor(Ferro);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 1.1f;
                        Vector3 b = new Vector3(Mathf.Cos(a) * 0.04f, Mathf.Sin(a) * 0.04f, 0.5f + i * 0.05f);
                        c.Tubo(b, b * 2.1f + new Vector3(0, 0, b.z * -1.1f + b.z), 0.006f, 0.002f, 4);
                    }
                    break;
                case "marreta":
                    c.Cor(Madeira).Tubo(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.75f), 0.028f, 0.028f, 8);
                    c.Cor(cor).Caixa(new Vector3(0, 0, 0.78f), new Vector3(0.28f, 0.13f, 0.13f), 0.02f);
                    break;
                case "viga":
                    c.Cor(cor).Caixa(new Vector3(0, 0, 0.42f), new Vector3(0.07f, 0.12f, 1.05f), 0.01f);
                    c.Cor(Ferro);
                    for (int i = 0; i < 4; i++) c.Caixa(new Vector3(0, 0.08f, 0.6f + i * 0.08f), new Vector3(0.02f, 0.06f, 0.02f));
                    break;
                case "martelo":
                    c.Cor(Ferro).Tubo(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.7f), 0.03f, 0.03f, 8);
                    c.Cor(cor).Tubo(new Vector3(-0.14f, 0, 0.78f), new Vector3(0.14f, 0, 0.78f), 0.09f, 0.09f, 10);
                    c.Cor(new Color(0.2f, 0.2f, 0.22f)).Caixa(new Vector3(0, 0.1f, 0.72f), new Vector3(0.05f, 0.08f, 0.12f), 0.01f);
                    break;
                case "lanca":
                case "forcado":
                case "arpao":
                    c.Cor(modelo == "lanca" ? Madeira : Ferro).Tubo(new Vector3(0, 0, -0.5f), new Vector3(0, 0, 1.25f), 0.022f, 0.022f, 6);
                    if (modelo == "forcado")
                    {
                        c.Cor(cor).Caixa(new Vector3(0, 0, 1.25f), new Vector3(0.22f, 0.02f, 0.03f));
                        for (int i = -1; i <= 1; i++) c.Tubo(new Vector3(i * 0.1f, 0, 1.25f), new Vector3(i * 0.1f, 0, 1.5f), 0.012f, 0.004f, 5);
                    }
                    else
                    {
                        c.Cor(cor).Tubo(new Vector3(0, 0, 1.22f), new Vector3(0, 0, 1.5f), 0.04f, 0.0f, 6);
                        if (modelo == "arpao") c.Tubo(new Vector3(0, 0, 1.3f), new Vector3(0, 0.08f, 1.22f), 0.012f, 0.003f, 4);
                    }
                    break;
                case "estilingue":
                    c.Cor(cor).Tubo(new Vector3(0, -0.08f, 0), new Vector3(0, 0.04f, 0), 0.02f, 0.02f, 6);
                    c.Tubo(new Vector3(0, 0.04f, 0), new Vector3(-0.06f, 0.14f, 0), 0.016f, 0.013f, 6);
                    c.Tubo(new Vector3(0, 0.04f, 0), new Vector3(0.06f, 0.14f, 0), 0.016f, 0.013f, 6);
                    c.Cor(new Color(0.15f, 0.12f, 0.1f)).Tubo(new Vector3(-0.06f, 0.14f, 0), new Vector3(0, 0.12f, -0.1f), 0.006f, 0.006f, 4);
                    c.Tubo(new Vector3(0.06f, 0.14f, 0), new Vector3(0, 0.12f, -0.1f), 0.006f, 0.006f, 4);
                    break;
                case "besta":
                    c.Cor(Madeira).Caixa(new Vector3(0, 0, 0.12f), new Vector3(0.05f, 0.06f, 0.55f), 0.01f);
                    c.Cor(cor).Caixa(new Vector3(0, 0.02f, 0.36f), new Vector3(0.6f, 0.03f, 0.04f), 0.008f);
                    c.Cor(Ferro).Caixa(new Vector3(0, 0.05f, 0.2f), new Vector3(0.015f, 0.015f, 0.35f));
                    break;
                case "lanca_dardos":
                    c.Cor(cor).Tubo(new Vector3(0, 0.04f, -0.1f), new Vector3(0, 0.04f, 0.5f), 0.035f, 0.03f, 8);
                    c.Cor(Couro).Caixa(new Vector3(0, -0.04f, 0f), new Vector3(0.035f, 0.12f, 0.05f), 0.01f);
                    break;
                case "bastao":
                    c.Cor(new Color(0.12f, 0.12f, 0.14f)).Tubo(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.55f), 0.028f, 0.024f, 8);
                    c.Cor(cor).Tubo(new Vector3(0, 0, 0.5f), new Vector3(0, 0, 0.62f), 0.03f, 0.03f, 8);
                    c.Cor(new Color(0.5f, 0.8f, 1f), 0f).Esfera(new Vector3(0, 0, 0.64f), 0.022f, 4, 6);
                    break;
                case "rifle":
                    c.Cor(new Color(0.85f, 0.87f, 0.9f)).Caixa(new Vector3(0, 0.02f, 0.15f), new Vector3(0.05f, 0.08f, 0.6f), 0.01f);
                    c.Cor(new Color(0.15f, 0.15f, 0.17f)).Tubo(new Vector3(0, 0.03f, 0.4f), new Vector3(0, 0.03f, 0.75f), 0.015f, 0.015f, 6);
                    c.Cor(new Color(1f, 0.2f, 0.1f), 0f).Esfera(new Vector3(0, 0.08f, 0.2f), 0.012f, 3, 4);
                    break;
                default:
                    c.Cor(cor).Caixa(new Vector3(0, 0, 0.2f), new Vector3(0.03f, 0.03f, 0.45f), 0.006f);
                    break;
            }
            m = c.Gerar(chave);
            _cache[chave] = m;
            return m;
        }

        /// <summary>Escudo preso no antebraco esquerdo, face para fora (+X local do braco).</summary>
        public static Mesh Escudo(string modelo, Color cor)
        {
            string chave = "escudo:" + modelo + ":" + Util.Hex(cor);
            if (_cache.TryGetValue(chave, out Mesh m)) return m;
            var c = new ConstrutorDeMalha();
            switch (modelo)
            {
                case "tampa":
                    c.Cor(cor).Cilindro(Vector3.zero, 0.26f, 0.03f, 16);
                    c.Cor(Util.Escurecer(cor, 0.7f)).Toro(new Vector3(0, 0.03f, 0), 0.24f, 0.015f, 18, 5);
                    c.Cor(Ferro).Caixa(new Vector3(0, 0.05f, 0), new Vector3(0.18f, 0.03f, 0.04f), 0.01f);
                    break;
                case "aljava":
                    c.Cor(cor).Cilindro(new Vector3(0, 0, 0), 0.07f, 0.5f, 8);
                    c.Cor(new Color(0.8f, 0.75f, 0.6f));
                    for (int i = 0; i < 4; i++) c.Tubo(new Vector3((i - 1.5f) * 0.025f, 0.48f, 0), new Vector3((i - 1.5f) * 0.03f, 0.62f, 0.01f), 0.006f, 0.006f, 4);
                    break;
                default:
                    float alto = modelo == "escudo_antimotim" ? 0.85f : 0.62f;
                    c.Cor(cor).Caixa(new Vector3(0, 0.02f, 0), new Vector3(0.44f, 0.04f, alto), 0.015f);
                    c.Cor(Util.Escurecer(cor, 0.65f));
                    if (modelo == "escudo_placas")
                        for (int i = 0; i < 3; i++) c.Caixa(new Vector3(0, 0.045f, -alto / 3 + i * alto / 3), new Vector3(0.42f, 0.012f, 0.03f));
                    else if (modelo == "escudo_antimotim")
                        c.Cor(new Color(0.5f, 0.7f, 0.9f), 0.9f).Caixa(new Vector3(0, 0.045f, alto * 0.25f), new Vector3(0.3f, 0.01f, 0.12f));
                    else
                        for (int i = 0; i < 4; i++) c.Caixa(new Vector3(-0.15f + i * 0.1f, 0.045f, 0), new Vector3(0.012f, 0.01f, alto * 0.95f));
                    break;
            }
            m = c.Gerar(chave);
            _cache[chave] = m;
            return m;
        }
    }

    internal static class ExtensoesDeMalha
    {
        public static void TrianguloChapadoSimples(this ConstrutorDeMalha c, Vector3 a, Vector3 b, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, d - a);
            c.TrianguloChapado(a, b, d, n);
            c.TrianguloChapado(a, d, b, -n);
        }
    }
}
