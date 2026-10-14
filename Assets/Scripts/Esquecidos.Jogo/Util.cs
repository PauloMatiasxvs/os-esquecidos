using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using V2 = System.Numerics.Vector2;

namespace Esquecidos.Jogo
{
    /// <summary>Conversoes entre o plano da simulacao (x, z) e o mundo do Unity.</summary>
    public static class Util
    {
        public static Vector3 Mundo(V2 p, float y = 0) => new Vector3(p.X, y, p.Y);

        public static V2 Plano(Vector3 p) => new V2(p.x, p.z);

        /// <summary>Angulo da simulacao (0 = +x, sentido de +z) para rotacao do Unity (modelo olha para +z).</summary>
        public static Quaternion Rotacao(float angulo) => Quaternion.LookRotation(new Vector3(Mathf.Cos(angulo), 0, Mathf.Sin(angulo)));

        public static float Angulo(Vector3 direcao) => Mathf.Atan2(direcao.z, direcao.x);

        private static readonly Dictionary<string, Color> _cores = new Dictionary<string, Color>();

        /// <summary>"#rrggbb" ou "#rrggbbaa" para cor linear do Unity.</summary>
        public static Color Cor(string hex, Color padrao = default)
        {
            if (string.IsNullOrEmpty(hex)) return padrao == default ? Color.magenta : padrao;
            if (_cores.TryGetValue(hex, out Color c)) return c;
            string h = hex.TrimStart('#');
            if (h.Length < 6 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
                return padrao == default ? Color.magenta : padrao;
            if (h.Length == 6) v = (v << 8) | 0xff;
            c = new Color(((v >> 24) & 0xff) / 255f, ((v >> 16) & 0xff) / 255f, ((v >> 8) & 0xff) / 255f, (v & 0xff) / 255f);
            _cores[hex] = c;
            return c;
        }

        public static Color Escurecer(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        public static Color Variar(Color c, System.Random rng, float quanto)
        {
            float f = 1 + ((float)rng.NextDouble() * 2 - 1) * quanto;
            return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static GameObject Filho(Transform pai, string nome, Vector3 pos = default)
        {
            var g = new GameObject(nome);
            g.transform.SetParent(pai, false);
            g.transform.localPosition = pos;
            return g;
        }

        /// <summary>GameObject com malha e material. Sombra ligada por padrao.</summary>
        public static MeshRenderer ComMalha(GameObject g, Mesh m, Material mat, bool sombra = true)
        {
            g.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = sombra ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        public static float Suavizar(float atual, float alvo, float velocidade, float dt) => Mathf.Lerp(atual, alvo, 1 - Mathf.Exp(-velocidade * dt));

        public static Vector3 Suavizar(Vector3 atual, Vector3 alvo, float velocidade, float dt) => Vector3.Lerp(atual, alvo, 1 - Mathf.Exp(-velocidade * dt));
    }
}
