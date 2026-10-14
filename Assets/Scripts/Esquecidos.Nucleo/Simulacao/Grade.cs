using System;
using System.Numerics;

namespace Esquecidos.Nucleo.Simulacao
{
    public readonly struct Retangulo
    {
        public readonly float X0, Z0, X1, Z1;

        public Retangulo(float x0, float z0, float x1, float z1)
        {
            X0 = MathF.Min(x0, x1);
            Z0 = MathF.Min(z0, z1);
            X1 = MathF.Max(x0, x1);
            Z1 = MathF.Max(z0, z1);
        }

        public static Retangulo Centrado(float cx, float cz, float largura, float profundidade)
        {
            return new Retangulo(cx - largura / 2, cz - profundidade / 2, cx + largura / 2, cz + profundidade / 2);
        }

        public float Largura => X1 - X0;
        public float Profundidade => Z1 - Z0;
        public Vector2 Centro => new((X0 + X1) / 2, (Z0 + Z1) / 2);

        public bool Contem(Vector2 p) => p.X >= X0 && p.X <= X1 && p.Y >= Z0 && p.Y <= Z1;
    }

    /// <summary>
    /// O chao do mapa visto de cima, em celulas de meio metro. Serve as tres
    /// coisas que precisam saber onde e parede: colisao, caminho e linha de
    /// visao. Cada celula conta quantos bloqueios a cobrem, para uma porta e
    /// um muro movel poderem se sobrepor sem um apagar o outro.
    /// </summary>
    public sealed class Grade
    {
        public const float Celula = 0.5f;
        public readonly int Largura;
        public readonly int Altura;
        private readonly short[] _bloqueios;
        private readonly float[] _folga;
        private bool _folgaSuja = true;

        /// <summary>Muda toda vez que um bloqueio entra ou sai; caminhos guardados se invalidam.</summary>
        public int Versao { get; private set; }

        public Grade(float larguraMetros, float alturaMetros)
        {
            Largura = Math.Max(1, (int)MathF.Ceiling(larguraMetros / Celula));
            Altura = Math.Max(1, (int)MathF.Ceiling(alturaMetros / Celula));
            _bloqueios = new short[Largura * Altura];
            _folga = new float[Largura * Altura];
        }

        public float LarguraMetros => Largura * Celula;
        public float AlturaMetros => Altura * Celula;

        public bool Dentro(int x, int z) => x >= 0 && z >= 0 && x < Largura && z < Altura;

        public bool Solido(int x, int z) => !Dentro(x, z) || _bloqueios[z * Largura + x] > 0;

        public bool SolidoEm(Vector2 p)
        {
            (int x, int z) = CelulaDe(p);
            return Solido(x, z);
        }

        public (int x, int z) CelulaDe(Vector2 p) => ((int)MathF.Floor(p.X / Celula), (int)MathF.Floor(p.Y / Celula));

        public Vector2 Centro(int x, int z) => new((x + 0.5f) * Celula, (z + 0.5f) * Celula);

        /// <summary>Soma (ou tira, com delta negativo) um bloqueio em toda celula que o retangulo toca.</summary>
        public void Bloquear(Retangulo r, int delta = 1)
        {
            int x0 = Math.Max(0, (int)MathF.Floor(r.X0 / Celula + 0.001f));
            int z0 = Math.Max(0, (int)MathF.Floor(r.Z0 / Celula + 0.001f));
            int x1 = Math.Min(Largura - 1, (int)MathF.Ceiling(r.X1 / Celula - 0.001f) - 1);
            int z1 = Math.Min(Altura - 1, (int)MathF.Ceiling(r.Z1 / Celula - 0.001f) - 1);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                int i = z * Largura + x;
                _bloqueios[i] = (short)Math.Max(0, _bloqueios[i] + delta);
            }
            _folgaSuja = true;
            Versao++;
        }

        public void BloquearCirculo(Vector2 centro, float raio, int delta = 1)
        {
            int x0 = Math.Max(0, (int)MathF.Floor((centro.X - raio) / Celula));
            int z0 = Math.Max(0, (int)MathF.Floor((centro.Y - raio) / Celula));
            int x1 = Math.Min(Largura - 1, (int)MathF.Floor((centro.X + raio) / Celula));
            int z1 = Math.Min(Altura - 1, (int)MathF.Floor((centro.Y + raio) / Celula));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (Vector2.Distance(Centro(x, z), centro) > raio) continue;
                int i = z * Largura + x;
                _bloqueios[i] = (short)Math.Max(0, _bloqueios[i] + delta);
            }
            _folgaSuja = true;
            Versao++;
        }

        /// <summary>
        /// Distancia em metros do centro da celula ate a parede mais proxima,
        /// por transformada de distancia em duas passadas. E o que deixa o
        /// caminho de um chefe largo diferente do de um bicho pequeno.
        /// </summary>
        public float Folga(int x, int z)
        {
            if (!Dentro(x, z)) return 0;
            if (_folgaSuja) RecalcularFolga();
            return _folga[z * Largura + x];
        }

        public float FolgaEm(Vector2 p)
        {
            (int x, int z) = CelulaDe(p);
            return Folga(x, z);
        }

        private void RecalcularFolga()
        {
            const float diag = 1.41421356f;
            int w = Largura, h = Altura;
            float infinito = w + h;
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                int i = z * w + x;
                if (_bloqueios[i] > 0) _folga[i] = 0;
                else if (x == 0 || z == 0 || x == w - 1 || z == h - 1) _folga[i] = 1;
                else _folga[i] = infinito;
            }
            for (int z = 1; z < h; z++)
            for (int x = 1; x < w - 1; x++)
            {
                int i = z * w + x;
                float v = _folga[i];
                v = MathF.Min(v, _folga[i - 1] + 1);
                v = MathF.Min(v, _folga[i - w] + 1);
                v = MathF.Min(v, _folga[i - w - 1] + diag);
                v = MathF.Min(v, _folga[i - w + 1] + diag);
                _folga[i] = v;
            }
            for (int z = h - 2; z >= 0; z--)
            for (int x = w - 2; x >= 1; x--)
            {
                int i = z * w + x;
                float v = _folga[i];
                v = MathF.Min(v, _folga[i + 1] + 1);
                v = MathF.Min(v, _folga[i + w] + 1);
                v = MathF.Min(v, _folga[i + w + 1] + diag);
                v = MathF.Min(v, _folga[i + w - 1] + diag);
                _folga[i] = v;
            }
            // Em celulas, medido de centro a centro: meia celula a menos e a
            // distancia ate a borda da celula solida.
            for (int i = 0; i < _folga.Length; i++)
                _folga[i] = _folga[i] <= 0 ? 0 : (_folga[i] - 0.5f) * Celula;
            _folgaSuja = false;
        }

        /// <summary>Raio fino de olho: so parede de verdade corta a visao.</summary>
        public bool LinhaDeVisao(Vector2 a, Vector2 b)
        {
            return PrimeiroSolido(a, b, out _) == false;
        }

        /// <summary>
        /// Percorre as celulas cruzadas pelo segmento (DDA de Amanatides-Woo) e
        /// diz se bateu em parede, e onde.
        /// </summary>
        public bool PrimeiroSolido(Vector2 a, Vector2 b, out Vector2 ponto)
        {
            ponto = b;
            (int x, int z) = CelulaDe(a);
            (int xf, int zf) = CelulaDe(b);
            if (Solido(x, z))
            {
                ponto = a;
                return true;
            }
            Vector2 d = b - a;
            int passoX = d.X > 0 ? 1 : -1, passoZ = d.Y > 0 ? 1 : -1;
            float deltaX = d.X != 0 ? MathF.Abs(Celula / d.X) : float.MaxValue;
            float deltaZ = d.Y != 0 ? MathF.Abs(Celula / d.Y) : float.MaxValue;
            float bordaX = passoX > 0 ? (x + 1) * Celula : x * Celula;
            float bordaZ = passoZ > 0 ? (z + 1) * Celula : z * Celula;
            float tX = d.X != 0 ? (bordaX - a.X) / d.X : float.MaxValue;
            float tZ = d.Y != 0 ? (bordaZ - a.Y) / d.Y : float.MaxValue;
            int limite = Largura + Altura + 4;
            while ((x != xf || z != zf) && limite-- > 0)
            {
                float t;
                if (tX < tZ)
                {
                    t = tX;
                    tX += deltaX;
                    x += passoX;
                }
                else
                {
                    t = tZ;
                    tZ += deltaZ;
                    z += passoZ;
                }
                if (t > 1f) break;
                if (Solido(x, z))
                {
                    ponto = a + d * MathF.Max(0, t - 0.001f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Da para ir em linha reta com esse raio sem raspar em parede?</summary>
        public bool CaminhoLivre(Vector2 a, Vector2 b, float raio)
        {
            float dist = Vector2.Distance(a, b);
            int passos = Math.Max(1, (int)MathF.Ceiling(dist / (Celula * 0.5f)));
            for (int i = 0; i <= passos; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)passos);
                if (FolgaEm(p) < raio) return false;
            }
            return true;
        }

        /// <summary>
        /// Empurra um circulo para fora de toda celula solida que ele invade.
        /// Duas passadas resolvem quinas, onde duas paredes empurram ao mesmo tempo.
        /// </summary>
        public Vector2 ResolverColisao(Vector2 pos, float raio)
        {
            for (int passada = 0; passada < 3; passada++)
            {
                bool mexeu = false;
                int x0 = (int)MathF.Floor((pos.X - raio) / Celula);
                int z0 = (int)MathF.Floor((pos.Y - raio) / Celula);
                int x1 = (int)MathF.Floor((pos.X + raio) / Celula);
                int z1 = (int)MathF.Floor((pos.Y + raio) / Celula);
                for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!Solido(x, z)) continue;
                    float cx0 = x * Celula, cz0 = z * Celula;
                    float px = Math.Clamp(pos.X, cx0, cx0 + Celula);
                    float pz = Math.Clamp(pos.Y, cz0, cz0 + Celula);
                    float dx = pos.X - px, dz = pos.Y - pz;
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= raio * raio) continue;
                    if (d2 > 1e-8f)
                    {
                        float d = MathF.Sqrt(d2);
                        pos += new Vector2(dx, dz) / d * (raio - d);
                    }
                    else
                    {
                        // Centro dentro da celula: sai pelo lado mais perto.
                        float esq = pos.X - cx0, dir = cx0 + Celula - pos.X;
                        float cima = pos.Y - cz0, baixo = cz0 + Celula - pos.Y;
                        float m = MathF.Min(MathF.Min(esq, dir), MathF.Min(cima, baixo));
                        if (m == esq) pos.X = cx0 - raio;
                        else if (m == dir) pos.X = cx0 + Celula + raio;
                        else if (m == cima) pos.Y = cz0 - raio;
                        else pos.Y = cz0 + Celula + raio;
                    }
                    mexeu = true;
                }
                if (!mexeu) break;
            }
            return pos;
        }

        /// <summary>A celula livre (com a folga pedida) mais perto do ponto, em espiral.</summary>
        public bool LivreMaisProximo(Vector2 p, float raio, out Vector2 livre, int alcanceCelulas = 24)
        {
            (int cx, int cz) = CelulaDe(p);
            if (Folga(cx, cz) >= raio)
            {
                livre = p;
                return true;
            }
            float melhor = float.MaxValue;
            livre = p;
            for (int r = 1; r <= alcanceCelulas; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dz) != r) continue;
                    int x = cx + dx, z = cz + dz;
                    if (Folga(x, z) < raio) continue;
                    float d = dx * dx + dz * dz;
                    if (d < melhor)
                    {
                        melhor = d;
                        livre = Centro(x, z);
                    }
                }
                if (melhor < float.MaxValue) return true;
            }
            return false;
        }
    }
}
