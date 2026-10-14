using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Esquecidos.Motor.Malhas
{
    /// <summary>
    /// Monta malhas em codigo: caixa chanfrada, cilindro, esfera, tubo entre
    /// dois pontos, plano com relevo. Cada primitiva sai na cor atual (cor de
    /// vertice), para um objeto inteiro caber numa malha e num material so.
    ///
    /// Ao gerar, a malha ganha no canal UV3 a normal suavizada de cada vertice:
    /// o contorno do shader toon empurra o casco por ela, e assim caixa de
    /// faces chapadas nao abre buraco no contorno das quinas.
    /// </summary>
    public sealed class ConstrutorDeMalha
    {
        private readonly List<Vector3> _v = new List<Vector3>(1024);
        private readonly List<Vector3> _n = new List<Vector3>(1024);
        private readonly List<Vector2> _uv = new List<Vector2>(1024);
        private readonly List<Color32> _c = new List<Color32>(1024);
        private readonly List<int> _t = new List<int>(2048);
        private readonly Stack<Matrix4x4> _pilha = new Stack<Matrix4x4>();
        private Matrix4x4 _m = Matrix4x4.identity;
        private Color32 _cor = new Color32(255, 255, 255, 255);

        public int Vertices => _v.Count;

        public ConstrutorDeMalha Cor(Color c)
        {
            _cor = c;
            return this;
        }

        public ConstrutorDeMalha Cor(Color c, float alfa)
        {
            c.a = alfa;
            _cor = c;
            return this;
        }

        public Color CorAtual => _cor;

        // ---------------------------------------------------------------- pilha

        public ConstrutorDeMalha Empurrar(Vector3 posicao, Quaternion rotacao, Vector3 escala)
        {
            _pilha.Push(_m);
            _m = _m * Matrix4x4.TRS(posicao, rotacao, escala);
            return this;
        }

        public ConstrutorDeMalha Empurrar(Vector3 posicao, Quaternion rotacao) => Empurrar(posicao, rotacao, Vector3.one);

        public ConstrutorDeMalha Empurrar(Vector3 posicao) => Empurrar(posicao, Quaternion.identity, Vector3.one);

        public ConstrutorDeMalha Puxar()
        {
            _m = _pilha.Pop();
            return this;
        }

        // ---------------------------------------------------------------- base

        private int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(_m.MultiplyPoint3x4(p));
            _n.Add(_m.MultiplyVector(n).normalized);
            _uv.Add(uv);
            _c.Add(_cor);
            return _v.Count - 1;
        }

        public void Triangulo(int a, int b, int c)
        {
            _t.Add(a);
            _t.Add(b);
            _t.Add(c);
        }

        /// <summary>
        /// Triangulo virado para o lado das normais dos vertices. O Unity
        /// desenha a face cujos vertices giram no sentido horario; em vez de
        /// acertar a ordem em cada primitiva, ela e conferida aqui.
        /// </summary>
        private void TrianguloAuto(int a, int b, int c, Vector3? normalDesejada = null)
        {
            Vector3 frente = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
            Vector3 alvo = normalDesejada ?? (_n[a] + _n[b] + _n[c]);
            if (Vector3.Dot(frente, alvo) < 0) Triangulo(a, c, b);
            else Triangulo(a, b, c);
        }

        /// <summary>Quad com normal chapada. Ordem anti-horaria vista de fora.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            Quad(a, b, c, d, n);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            float u = Vector3.Distance(a, b), w = Vector3.Distance(a, d);
            int i0 = Vert(a, n, new Vector2(0, 0));
            int i1 = Vert(b, n, new Vector2(u, 0));
            int i2 = Vert(c, n, new Vector2(u, w));
            int i3 = Vert(d, n, new Vector2(0, w));
            TrianguloAuto(i0, i2, i1);
            TrianguloAuto(i0, i3, i2);
        }

        /// <summary>
        /// Quad com UV de 0 a 1 nas quatro pontas e as duas faces: para efeitos
        /// (facho de luz, rastro) que o shader recorta pela UV.
        /// </summary>
        public void QuadUv(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(d - a, b - a).normalized;
            int i0 = Vert(a, n, new Vector2(0, 0)), i1 = Vert(b, n, new Vector2(1, 0));
            int i2 = Vert(c, n, new Vector2(1, 1)), i3 = Vert(d, n, new Vector2(0, 1));
            Triangulo(i0, i2, i1);
            Triangulo(i0, i3, i2);
            Triangulo(i0, i1, i2);
            Triangulo(i0, i2, i3);
        }

        public void TrianguloChapado(Vector3 a, Vector3 b, Vector3 c, Vector3 normalDesejada)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, normalDesejada) < 0) n = -n;
            int i0 = Vert(a, n, Vector2.zero), i1 = Vert(b, n, Vector2.right), i2 = Vert(c, n, Vector2.up);
            TrianguloAuto(i0, i1, i2);
        }

        // ---------------------------------------------------------------- caixa

        /// <summary>
        /// Caixa alinhada aos eixos locais. Com chanfro, as arestas viram faces
        /// inclinadas: o toon pega luz nelas e o objeto deixa de parecer cubo
        /// de prototipo.
        /// </summary>
        public void Caixa(Vector3 centro, Vector3 tamanho, float chanfro = 0f)
        {
            Vector3 h = tamanho * 0.5f;
            chanfro = Mathf.Min(chanfro, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
            if (chanfro <= 0.0001f)
            {
                Vector3 p0 = centro + new Vector3(-h.x, -h.y, -h.z), p1 = centro + new Vector3(h.x, -h.y, -h.z);
                Vector3 p2 = centro + new Vector3(h.x, -h.y, h.z), p3 = centro + new Vector3(-h.x, -h.y, h.z);
                Vector3 p4 = centro + new Vector3(-h.x, h.y, -h.z), p5 = centro + new Vector3(h.x, h.y, -h.z);
                Vector3 p6 = centro + new Vector3(h.x, h.y, h.z), p7 = centro + new Vector3(-h.x, h.y, h.z);
                Quad(p4, p5, p6, p7, Vector3.up);
                Quad(p3, p2, p1, p0, Vector3.down);
                Quad(p0, p1, p5, p4, Vector3.back);
                Quad(p2, p3, p7, p6, Vector3.forward);
                Quad(p1, p2, p6, p5, Vector3.right);
                Quad(p3, p0, p4, p7, Vector3.left);
                return;
            }
            float c = chanfro;
            // Faces principais encolhidas.
            Vector3 hi = h - new Vector3(c, c, c);
            FaceEixo(centro, hi, h, Vector3.up);
            FaceEixo(centro, hi, h, Vector3.down);
            FaceEixo(centro, hi, h, Vector3.forward);
            FaceEixo(centro, hi, h, Vector3.back);
            FaceEixo(centro, hi, h, Vector3.right);
            FaceEixo(centro, hi, h, Vector3.left);
            // Arestas e cantos: liga os vertices das faces vizinhas.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 px = centro + new Vector3(sx * h.x, sy * hi.y, sz * hi.z);
                Vector3 py = centro + new Vector3(sx * hi.x, sy * h.y, sz * hi.z);
                Vector3 pz = centro + new Vector3(sx * hi.x, sy * hi.y, sz * h.z);
                TrianguloChapado(px, py, pz, new Vector3(sx, sy, sz));
            }
            // Arestas ao longo de X.
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 a = centro + new Vector3(-hi.x, sy * h.y, sz * hi.z), b = centro + new Vector3(hi.x, sy * h.y, sz * hi.z);
                Vector3 d = centro + new Vector3(-hi.x, sy * hi.y, sz * h.z), e = centro + new Vector3(hi.x, sy * hi.y, sz * h.z);
                QuadOrientado(a, b, e, d, new Vector3(0, sy, sz));
            }
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 a = centro + new Vector3(sx * h.x, -hi.y, sz * hi.z), b = centro + new Vector3(sx * h.x, hi.y, sz * hi.z);
                Vector3 d = centro + new Vector3(sx * hi.x, -hi.y, sz * h.z), e = centro + new Vector3(sx * hi.x, hi.y, sz * h.z);
                QuadOrientado(a, b, e, d, new Vector3(sx, 0, sz));
            }
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector3 a = centro + new Vector3(sx * h.x, sy * hi.y, -hi.z), b = centro + new Vector3(sx * h.x, sy * hi.y, hi.z);
                Vector3 d = centro + new Vector3(sx * hi.x, sy * h.y, -hi.z), e = centro + new Vector3(sx * hi.x, sy * h.y, hi.z);
                QuadOrientado(a, b, e, d, new Vector3(sx, sy, 0));
            }
        }

        private void FaceEixo(Vector3 c, Vector3 hi, Vector3 h, Vector3 n)
        {
            Vector3 u, v;
            if (n.y != 0)
            {
                u = new Vector3(hi.x, 0, 0);
                v = new Vector3(0, 0, hi.z);
            }
            else if (n.z != 0)
            {
                u = new Vector3(hi.x, 0, 0);
                v = new Vector3(0, hi.y, 0);
            }
            else
            {
                u = new Vector3(0, 0, hi.z);
                v = new Vector3(0, hi.y, 0);
            }
            Vector3 centroFace = c + Vector3.Scale(n, h);
            Vector3 a = centroFace - u - v, b = centroFace + u - v, cc = centroFace + u + v, d = centroFace - u + v;
            QuadOrientado(a, b, cc, d, n);
        }

        /// <summary>Quad que se vira sozinho para a normal pedida.</summary>
        private void QuadOrientado(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            normal = normal.normalized;
            Vector3 n = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(n, normal) > 0) Quad(a, d, c, b, normal);
            else Quad(a, b, c, d, normal);
        }

        // ---------------------------------------------------------------- revolucao

        /// <summary>Cilindro (ou tronco de cone) em pe, com a base no ponto dado.</summary>
        public void Cilindro(Vector3 baseCentro, float raio, float altura, int lados = 12, bool tampas = true, float raioTopo = -1f, bool chapado = false)
        {
            if (raioTopo < 0) raioTopo = raio;
            Tubo(baseCentro, baseCentro + Vector3.up * altura, raio, raioTopo, lados, tampas, chapado);
        }

        /// <summary>
        /// Tronco de cone entre dois pontos quaisquer. E o osso de tudo: braco,
        /// perna, galho, pata de Tecelao, cano.
        /// </summary>
        public void Tubo(Vector3 a, Vector3 b, float raioA, float raioB, int lados = 10, bool tampas = true, bool chapado = false)
        {
            Vector3 eixo = b - a;
            float comp = eixo.magnitude;
            if (comp < 1e-5f) return;
            Vector3 dir = eixo / comp;
            Vector3 ref1 = Mathf.Abs(dir.y) < 0.95f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(dir, ref1).normalized;
            Vector3 w = Vector3.Cross(dir, u);
            float inclinacao = (raioA - raioB) / comp;
            int primeiro = _v.Count;
            for (int i = 0; i <= lados; i++)
            {
                float ang = i / (float)lados * Mathf.PI * 2;
                Vector3 radial = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                Vector3 n = (radial + dir * inclinacao).normalized;
                Vert(a + radial * raioA, n, new Vector2(i / (float)lados, 0));
                Vert(b + radial * raioB, n, new Vector2(i / (float)lados, comp));
            }
            for (int i = 0; i < lados; i++)
            {
                int k = primeiro + i * 2;
                TrianguloAuto(k, k + 1, k + 3);
                TrianguloAuto(k, k + 3, k + 2);
            }
            if (chapado) Achatar(primeiro);
            if (!tampas) return;
            Tampa(a, -dir, u, w, raioA, lados);
            Tampa(b, dir, u, w, raioB, lados);
        }

        private void Tampa(Vector3 centro, Vector3 normal, Vector3 u, Vector3 w, float raio, int lados)
        {
            if (raio <= 0.0001f) return;
            int c = Vert(centro, normal, new Vector2(0.5f, 0.5f));
            int primeiro = _v.Count;
            for (int i = 0; i <= lados; i++)
            {
                float ang = i / (float)lados * Mathf.PI * 2;
                Vert(centro + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * raio, normal, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.5f + Vector2.one * 0.5f);
            }
            for (int i = 0; i < lados; i++) TrianguloAuto(c, primeiro + i, primeiro + i + 1);
        }

        /// <summary>Elipsoide com normais suaves.</summary>
        public void Esfera(Vector3 centro, Vector3 raios, int segLat = 10, int segLon = 14)
        {
            int primeiro = _v.Count;
            for (int lat = 0; lat <= segLat; lat++)
            {
                float t = lat / (float)segLat;
                float th = t * Mathf.PI;
                float y = Mathf.Cos(th), r = Mathf.Sin(th);
                for (int lon = 0; lon <= segLon; lon++)
                {
                    float ph = lon / (float)segLon * Mathf.PI * 2;
                    var unit = new Vector3(r * Mathf.Cos(ph), y, r * Mathf.Sin(ph));
                    Vector3 p = centro + Vector3.Scale(unit, raios);
                    Vector3 n = new Vector3(unit.x / raios.x, unit.y / raios.y, unit.z / raios.z).normalized;
                    Vert(p, n, new Vector2(lon / (float)segLon, t));
                }
            }
            int linha = segLon + 1;
            for (int lat = 0; lat < segLat; lat++)
            for (int lon = 0; lon < segLon; lon++)
            {
                int a = primeiro + lat * linha + lon, b = a + linha;
                if (lat > 0) TrianguloAuto(a, a + 1, b);
                if (lat < segLat - 1) TrianguloAuto(a + 1, b + 1, b);
            }
        }

        public void Esfera(Vector3 centro, float raio, int segLat = 10, int segLon = 14) => Esfera(centro, Vector3.one * raio, segLat, segLon);

        /// <summary>Capsula entre dois pontos: tubo com meia esfera nas pontas.</summary>
        public void Capsula(Vector3 a, Vector3 b, float raioA, float raioB, int lados = 10)
        {
            Tubo(a, b, raioA, raioB, lados, false);
            Esfera(a, raioA, Mathf.Max(4, lados / 2), lados);
            Esfera(b, raioB, Mathf.Max(4, lados / 2), lados);
        }

        public void Capsula(Vector3 a, Vector3 b, float raio, int lados = 10) => Capsula(a, b, raio, raio, lados);

        public void Toro(Vector3 centro, float raioMaior, float raioMenor, int seg = 18, int lados = 8)
        {
            int primeiro = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var cAnel = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j <= lados; j++)
                {
                    float b = j / (float)lados * Mathf.PI * 2;
                    Vector3 n = cAnel * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    Vert(centro + cAnel * raioMaior + n * raioMenor, n, new Vector2(i / (float)seg, j / (float)lados));
                }
            }
            int linha = lados + 1;
            for (int i = 0; i < seg; i++)
            for (int j = 0; j < lados; j++)
            {
                int a = primeiro + i * linha + j, b = a + linha;
                TrianguloAuto(a, b, a + 1);
                TrianguloAuto(a + 1, b, b + 1);
            }
        }

        /// <summary>Contorno no plano XZ extrudado para cima, faces chapadas. Engrenagens, placas recortadas.</summary>
        public void Extrusao(IList<Vector2> contorno, float altura, float y0 = 0)
        {
            int n = contorno.Count;
            Vector3 cs = Vector3.zero;
            foreach (Vector2 p in contorno) cs += new Vector3(p.x, 0, p.y);
            cs /= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = contorno[i], q = contorno[(i + 1) % n];
                Vector3 a = new Vector3(p.x, y0, p.y), b = new Vector3(q.x, y0, q.y);
                Vector3 meio = (a + b) * 0.5f;
                Vector3 fora = new Vector3(meio.x - cs.x, 0, meio.z - cs.z);
                QuadOrientado(a, b, b + Vector3.up * altura, a + Vector3.up * altura, fora);
            }
            Vector3 topo = new Vector3(cs.x, y0 + altura, cs.z), baixo = new Vector3(cs.x, y0, cs.z);
            int ct = Vert(topo, Vector3.up, Vector2.zero);
            int cb = Vert(baixo, Vector3.down, Vector2.zero);
            int pt = _v.Count;
            for (int i = 0; i < n; i++) Vert(new Vector3(contorno[i].x, y0 + altura, contorno[i].y), Vector3.up, contorno[i]);
            int pb = _v.Count;
            for (int i = 0; i < n; i++) Vert(new Vector3(contorno[i].x, y0, contorno[i].y), Vector3.down, contorno[i]);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                TrianguloAuto(ct, pt + j, pt + i);
                TrianguloAuto(cb, pb + i, pb + j);
            }
        }

        /// <summary>Plano no XZ subdividido, com altura opcional por ponto. Normais calculadas pelo relevo.</summary>
        public void Plano(Vector3 centro, Vector2 tamanho, int divX, int divZ, Func<float, float, float> altura = null, float escalaUv = 1f)
        {
            int primeiro = _v.Count;
            float dx = tamanho.x / divX, dz = tamanho.y / divZ;
            float x0 = centro.x - tamanho.x / 2, z0 = centro.z - tamanho.y / 2;
            float H(float x, float z) => altura?.Invoke(x, z) ?? 0;
            for (int j = 0; j <= divZ; j++)
            for (int i = 0; i <= divX; i++)
            {
                float x = x0 + i * dx, z = z0 + j * dz;
                float y = centro.y + H(x, z);
                Vector3 n = altura == null ? Vector3.up : new Vector3(H(x - 0.1f, z) - H(x + 0.1f, z), 0.2f, H(x, z - 0.1f) - H(x, z + 0.1f)).normalized;
                Vert(new Vector3(x, y, z), n, new Vector2(x, z) * escalaUv);
            }
            int linha = divX + 1;
            for (int j = 0; j < divZ; j++)
            for (int i = 0; i < divX; i++)
            {
                int a = primeiro + j * linha + i, b = a + linha;
                TrianguloAuto(a, b, a + 1);
                TrianguloAuto(a + 1, b, b + 1);
            }
        }

        /// <summary>Faz as faces geradas desde 'primeiro' ficarem chapadas (vertices proprios por triangulo).</summary>
        private void Achatar(int primeiroVertice)
        {
            // Mantido simples: so recalcula normais por triangulo trocando a media por face.
            for (int i = 0; i < _t.Count; i += 3)
            {
                int a = _t[i], b = _t[i + 1], c = _t[i + 2];
                if (a < primeiroVertice) continue;
                Vector3 n = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]).normalized;
                _n[a] = _n[b] = _n[c] = n;
            }
        }

        /// <summary>Copia outra malha para dentro desta, com a transformacao atual e a cor atual.</summary>
        public void Juntar(Mesh m, Matrix4x4 local, bool usarCorDaMalha = false)
        {
            Matrix4x4 mat = _m * local;
            Vector3[] vs = m.vertices;
            Vector3[] ns = m.normals;
            Vector2[] uvs = m.uv;
            Color32[] cs = m.colors32;
            int[] ts = m.triangles;
            int primeiro = _v.Count;
            for (int i = 0; i < vs.Length; i++)
            {
                _v.Add(mat.MultiplyPoint3x4(vs[i]));
                _n.Add(ns.Length > i ? mat.MultiplyVector(ns[i]).normalized : Vector3.up);
                _uv.Add(uvs.Length > i ? uvs[i] : Vector2.zero);
                _c.Add(usarCorDaMalha && cs.Length > i ? cs[i] : _cor);
            }
            foreach (int t in ts) _t.Add(primeiro + t);
        }

        public void Limpar()
        {
            _v.Clear();
            _n.Clear();
            _uv.Clear();
            _c.Clear();
            _t.Clear();
            _pilha.Clear();
            _m = Matrix4x4.identity;
        }

        /// <summary>Muda o alfa da cor de um vertice ja escrito (borda de zona de chao).</summary>
        public void AjustarAlfa(int indice, float alfa)
        {
            Color32 c = _c[indice];
            c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(alfa * 255), 0, 255);
            _c[indice] = c;
        }

        /// <summary>Lamina de grama: triangulo com UV.y 0 na base e 1 na ponta, para o vento.</summary>
        public void Lamina(Vector3 baseA, Vector3 baseB, Vector3 ponta)
        {
            Vector3 n = Vector3.up;
            int a = Vert(baseA, n, new Vector2(0, 0));
            int b = Vert(baseB, n, new Vector2(1, 0));
            int c = Vert(ponta, n, new Vector2(0.5f, 1));
            Triangulo(a, b, c);
        }

        /// <summary>Como Gerar, sem a normal suavizada do contorno: para malhas enormes sem contorno (grama).</summary>
        public Mesh GerarSemSuave(string nome)
        {
            var m = new Mesh { name = nome };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetColors(_c);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return m;
        }

        public Mesh Gerar(string nome)
        {
            var m = new Mesh { name = nome };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetColors(_c);
            m.SetTriangles(_t, 0);
            m.SetUVs(3, NormaisSuaves());
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Media das normais de todos os vertices que caem no mesmo ponto. E a
        /// direcao em que o contorno empurra o casco.
        /// </summary>
        private List<Vector3> NormaisSuaves()
        {
            var soma = new Dictionary<Vector3Int, Vector3>(_v.Count);
            for (int i = 0; i < _v.Count; i++)
            {
                Vector3Int k = Chave(_v[i]);
                soma.TryGetValue(k, out Vector3 s);
                soma[k] = s + _n[i];
            }
            var r = new List<Vector3>(_v.Count);
            for (int i = 0; i < _v.Count; i++)
            {
                Vector3 s = soma[Chave(_v[i])];
                r.Add(s.sqrMagnitude > 1e-8f ? s.normalized : _n[i]);
            }
            return r;
        }

        private static Vector3Int Chave(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000), Mathf.RoundToInt(p.y * 2000), Mathf.RoundToInt(p.z * 2000));
    }
}
