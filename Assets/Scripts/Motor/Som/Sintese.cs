using System;
using UnityEngine;

namespace Esquecidos.Motor.Som
{
    /// <summary>
    /// Ferramentas para gerar efeitos sonoros em codigo: osciladores, ruido,
    /// filtros e envelopes. Cada som do jogo e uma receita curta que escreve
    /// num buffer de amostras; nenhum arquivo de audio no projeto.
    /// </summary>
    public static class Sintese
    {
        public const int Taxa = 44100;

        public static float[] Buffer(float segundos) => new float[Mathf.Max(1, Mathf.CeilToInt(segundos * Taxa))];

        public static AudioClip Clip(string nome, float[] amostras, float volume = 1f)
        {
            Normalizar(amostras, volume);
            AudioClip c = AudioClip.Create(nome, amostras.Length, 1, Taxa, false);
            c.SetData(amostras, 0);
            return c;
        }

        /// <summary>Leva o pico ao volume pedido e suaviza as pontas para nao estalar.</summary>
        public static void Normalizar(float[] a, float volume)
        {
            float pico = 1e-6f;
            for (int i = 0; i < a.Length; i++) pico = Mathf.Max(pico, Mathf.Abs(a[i]));
            float g = volume / pico;
            int borda = Mathf.Min(64, a.Length / 4);
            for (int i = 0; i < a.Length; i++)
            {
                float e = 1;
                if (i < borda) e = i / (float)borda;
                else if (i > a.Length - borda) e = (a.Length - i) / (float)borda;
                a[i] *= g * e;
            }
        }

        // ---------------------------------------------------------------- envelopes

        /// <summary>Ataque linear e queda exponencial: a forma de quase todo som de impacto.</summary>
        public static float Env(float t, float ataque, float queda)
        {
            if (t < 0) return 0;
            if (t < ataque) return t / ataque;
            return Mathf.Exp(-(t - ataque) / Mathf.Max(queda, 1e-4f));
        }

        public static float Adsr(float t, float dur, float a, float d, float s, float r)
        {
            if (t < 0) return 0;
            if (t < a) return t / a;
            if (t < a + d) return Mathf.Lerp(1, s, (t - a) / d);
            if (t < dur) return s;
            return s * Mathf.Exp(-(t - dur) / Mathf.Max(r, 1e-4f));
        }

        // ---------------------------------------------------------------- osciladores

        public static float Seno(float fase) => Mathf.Sin(fase * 2 * Mathf.PI);
        public static float Serra(float fase) => 2 * (fase - Mathf.Floor(fase + 0.5f));
        public static float Quadrada(float fase, float pw = 0.5f) => (fase - Mathf.Floor(fase)) < pw ? 1 : -1;
        public static float Triangulo(float fase) => 1 - 4 * Mathf.Abs((fase - Mathf.Floor(fase)) - 0.5f);

        /// <summary>Oscilador com fase acumulada: aceita frequencia que muda no tempo (varredura).</summary>
        public sealed class Osc
        {
            private double _fase;

            public float Avancar(float freq)
            {
                _fase += freq / Taxa;
                if (_fase > 1e6) _fase -= Math.Floor(_fase);
                return (float)(_fase - Math.Floor(_fase));
            }
        }

        // ---------------------------------------------------------------- ruido

        public sealed class Ruido
        {
            private uint _s;
            private float _rosa0, _rosa1, _rosa2, _marrom;

            public Ruido(int semente)
            {
                _s = (uint)(semente * 747796405 + 2891336453);
                if (_s == 0) _s = 1;
            }

            public float Branco()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return (_s & 0xffffff) / (float)0x7fffff - 1f;
            }

            public float Rosa()
            {
                float b = Branco();
                _rosa0 = 0.99765f * _rosa0 + b * 0.0990460f;
                _rosa1 = 0.96300f * _rosa1 + b * 0.2965164f;
                _rosa2 = 0.57000f * _rosa2 + b * 1.0526913f;
                return (_rosa0 + _rosa1 + _rosa2 + b * 0.1848f) * 0.25f;
            }

            public float Marrom()
            {
                _marrom = Mathf.Clamp(_marrom + Branco() * 0.02f, -1, 1);
                return _marrom * 3.5f;
            }
        }

        // ---------------------------------------------------------------- filtros

        /// <summary>Biquad (Robert Bristow-Johnson). Passa-baixa, passa-alta e passa-faixa.</summary>
        public sealed class Filtro
        {
            private float _b0, _b1, _b2, _a1, _a2, _x1, _x2, _y1, _y2;
            private readonly int _tipo;

            public const int PassaBaixa = 0, PassaAlta = 1, PassaFaixa = 2;

            public Filtro(int tipo, float freq, float q = 0.707f)
            {
                _tipo = tipo;
                Ajustar(freq, q);
            }

            public void Ajustar(float freq, float q = 0.707f)
            {
                freq = Mathf.Clamp(freq, 10, Taxa * 0.45f);
                float w = 2 * Mathf.PI * freq / Taxa;
                float cw = Mathf.Cos(w), sw = Mathf.Sin(w);
                float alfa = sw / (2 * q);
                float b0, b1, b2, a0 = 1 + alfa, a1 = -2 * cw, a2 = 1 - alfa;
                switch (_tipo)
                {
                    case PassaAlta:
                        b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2; break;
                    case PassaFaixa:
                        b0 = alfa; b1 = 0; b2 = -alfa; break;
                    default:
                        b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; break;
                }
                _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
            }

            public float Processar(float x)
            {
                float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
                return y;
            }
        }

        public static float Distorcer(float x, float ganho) => (float)Math.Tanh(x * ganho);
    }
}
