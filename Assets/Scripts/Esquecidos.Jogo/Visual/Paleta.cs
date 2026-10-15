using Esquecidos.Motor.Materiais;
using Esquecidos.Nucleo.Mapas;
using UnityEngine;

namespace Esquecidos.Jogo.Visual
{
    /// <summary>
    /// Os materiais do jogo e o acabamento de cada um. Ha poucos: o toon para
    /// tudo que tem cor de vertice, um muro por estilo, um chao por tipo, a
    /// grama e as particulas. Poucos materiais, poucas trocas de estado.
    /// </summary>
    public static class Paleta
    {
        public static Material Toon => FabricaDeMateriais.Obter("toon", "Toon");

        /// <summary>Toon das pecas grandes do cenario: copa e telhado entre a camera e o jogador somem em pontilhado.</summary>
        public static Material ToonCenario => FabricaDeMateriais.Obter("toon_cenario", "Toon", m => m.SetFloat("_CorteJogador", 2.4f));

        /// <summary>Toon sem contorno: para pecas pequenas e numerosas (folha, pedrinha) e chao de enfeite.</summary>
        public static Material ToonSemContorno => FabricaDeMateriais.Obter("toon_liso", "Toon", m => m.SetFloat("_Contorno", 0));

        /// <summary>Toon com contorno mais grosso, para os chefes saltarem da tela.</summary>
        public static Material ToonChefe => FabricaDeMateriais.Obter("toon_chefe", "Toon", m => m.SetFloat("_Contorno", 2.4f));

        public static Material Muro(string estilo)
        {
            return FabricaDeMateriais.Obter("muro:" + estilo, "Muro", m =>
            {
                switch (estilo)
                {
                    case "patio":
                        m.SetColor("_CorBase", new Color(0.56f, 0.54f, 0.5f));
                        m.SetFloat("_Hera", 0.78f);
                        m.SetFloat("_Musgo", 0.6f);
                        break;
                    case "hera":
                        m.SetColor("_CorBase", new Color(0.5f, 0.5f, 0.48f));
                        m.SetFloat("_Hera", 0.6f);
                        m.SetFloat("_Musgo", 0.5f);
                        break;
                    case "pilar":
                        m.SetColor("_CorBase", new Color(0.44f, 0.43f, 0.41f));
                        m.SetFloat("_Hera", 0.45f);
                        m.SetFloat("_Musgo", 0.6f);
                        break;
                    case "verga":
                    case "concreto":
                        m.SetColor("_CorBase", new Color(0.52f, 0.51f, 0.49f));
                        m.SetFloat("_Hera", 0.15f);
                        m.SetFloat("_Musgo", 0.3f);
                        break;
                    case "metal":
                        m.SetColor("_CorBase", new Color(0.4f, 0.38f, 0.36f));
                        m.SetFloat("_Hera", 0.08f);
                        m.SetFloat("_Musgo", 0.1f);
                        m.SetFloat("_Ferrugem", 1f);
                        m.SetFloat("_Forma", 0.3f);
                        break;
                    case "nucleo":
                        m.SetColor("_CorBase", new Color(0.3f, 0.31f, 0.34f));
                        m.SetFloat("_Hera", 0f);
                        m.SetFloat("_Musgo", 0f);
                        m.SetFloat("_Ferrugem", 0.5f);
                        break;
                    case "laboratorio":
                        m.SetColor("_CorBase", new Color(0.36f, 0.39f, 0.44f));
                        m.SetFloat("_Hera", 0f);
                        m.SetFloat("_Musgo", 0f);
                        m.SetFloat("_Lab", 1f);
                        m.SetFloat("_Corte", 2.6f);
                        break;
                }
            });
        }

        public static string EstiloDoMuro(string estilo, Tema tema)
        {
            if (estilo == "pilar" || estilo == "verga" || estilo == "patio") return tema == Tema.Laboratorio ? "laboratorio" : tema == Tema.Nucleo ? "nucleo" : estilo;
            if (tema == Tema.Nucleo) return "nucleo";
            return estilo;
        }

        public static Material Chao(TipoDeChao tipo)
        {
            return FabricaDeMateriais.Obter("chao:" + tipo, "Chao", m =>
            {
                int t;
                switch (tipo)
                {
                    case TipoDeChao.Grama: t = 0; break;
                    case TipoDeChao.Terra: t = 1; break;
                    case TipoDeChao.Pedra: t = 2; break;
                    case TipoDeChao.Plantacao: t = 3; break;
                    case TipoDeChao.Concreto: t = 4; break;
                    case TipoDeChao.Metal: t = 5; break;
                    case TipoDeChao.Ladrilho: t = 6; break;
                    default: t = 7; break;
                }
                m.SetFloat("_Tipo", t);
                // Cada tipo numa camada de fila: zona por cima da base sem briga de profundidade.
                m.renderQueue = 1990 + t;
            });
        }

        public static Material Grama => FabricaDeMateriais.Obter("grama", "Grama");

        public static Material Ceu => FabricaDeMateriais.Obter("ceu", "Ceu");

        public static Material Aditiva(string chave, int forma, float brilho = 2f) => FabricaDeMateriais.Particula(chave, true, forma, brilho);

        public static Material Mistura(string chave, int forma, float brilho = 1f) => FabricaDeMateriais.Particula(chave, false, forma, brilho);
    }
}
