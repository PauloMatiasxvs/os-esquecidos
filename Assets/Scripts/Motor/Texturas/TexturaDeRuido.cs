using UnityEngine;

namespace Esquecidos.Motor.Texturas
{
    /// <summary>
    /// A unica textura do jogo: 256x256, repetivel, gerada na partida.
    /// Cada canal e um ruido diferente e os shaders combinam em varias escalas
    /// para fazer concreto, hera, grama, ferrugem, laje e tudo mais.
    ///   R: fbm suave (5 oitavas)    G: Worley (distancia ao ponto mais perto)
    ///   B: ruido fino               A: outro fbm, de outra semente
    /// </summary>
    public static class TexturaDeRuido
    {
        public const int Tamanho = 256;

        public static Texture2D Gerar(int semente = 1337)
        {
            var t = new Texture2D(Tamanho, Tamanho, TextureFormat.RGBA32, true, true)
            {
                name = "Ruido",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            var px = new Color32[Tamanho * Tamanho];
            Vector2[] pontos = PontosDeWorley(8, semente + 7);
            for (int y = 0; y < Tamanho; y++)
            for (int x = 0; x < Tamanho; x++)
            {
                float u = x / (float)Tamanho, v = y / (float)Tamanho;
                float r = Fbm(u, v, 4, 5, semente);
                float g = Worley(u, v, 8, pontos);
                float b = Fbm(u, v, 32, 2, semente + 3);
                float a = Fbm(u, v, 6, 4, semente + 11);
                px[y * Tamanho + x] = new Color32(Byte(r), Byte(g), Byte(b), Byte(a));
            }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        private static byte Byte(float f) => (byte)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);

        private static float Hash(int x, int y, int periodo, int semente)
        {
            x = ((x % periodo) + periodo) % periodo;
            y = ((y % periodo) + periodo) % periodo;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + semente * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0xffffff;
            }
        }

        /// <summary>Ruido de valor periodico: a grade dá a volta, e o resultado emenda nas bordas.</summary>
        private static float Valor(float u, float v, int periodo, int semente)
        {
            float x = u * periodo, y = v * periodo;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10);
            fy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
            float a = Hash(x0, y0, periodo, semente), b = Hash(x0 + 1, y0, periodo, semente);
            float c = Hash(x0, y0 + 1, periodo, semente), d = Hash(x0 + 1, y0 + 1, periodo, semente);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int periodo, int oitavas, int semente)
        {
            float soma = 0, amp = 0.5f, total = 0;
            for (int o = 0; o < oitavas; o++)
            {
                soma += Valor(u, v, periodo << o, semente + o * 31) * amp;
                total += amp;
                amp *= 0.5f;
            }
            return soma / total;
        }

        private static Vector2[] PontosDeWorley(int celulas, int semente)
        {
            var p = new Vector2[celulas * celulas];
            for (int y = 0; y < celulas; y++)
            for (int x = 0; x < celulas; x++)
                p[y * celulas + x] = new Vector2(Hash(x, y, celulas, semente), Hash(x, y, celulas, semente + 1));
            return p;
        }

        private static float Worley(float u, float v, int celulas, Vector2[] pontos)
        {
            float x = u * celulas, y = v * celulas;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float melhor = 9;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy;
                int wx = ((gx % celulas) + celulas) % celulas, wy = ((gy % celulas) + celulas) % celulas;
                Vector2 f = pontos[wy * celulas + wx];
                float px = gx + f.x, py = gy + f.y;
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < melhor) melhor = d;
            }
            return Mathf.Clamp01(Mathf.Sqrt(melhor) / 1.1f);
        }
    }
}
