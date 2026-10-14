using System.Collections.Generic;
using Esquecidos.Jogo.Visual;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using MundoSim = Esquecidos.Nucleo.Simulacao.Mundo;
using Random = System.Random;

namespace Esquecidos.Jogo.Cena
{
    /// <summary>O mapa desenhado: guarda o que precisa ser atualizado depois (luzes da noite, objetos).</summary>
    public sealed class Cenario : MonoBehaviour
    {
        public Mapa Mapa;
        public MundoSim Mundo;
        public readonly List<VisualDeObjeto> Objetos = new List<VisualDeObjeto>();
        public readonly List<Light> LuzesDaNoite = new List<Light>();
        public readonly List<Light> Fogueiras = new List<Light>();
        private float _tempo;

        private void Update()
        {
            _tempo += Time.deltaTime;
            for (int i = 0; i < Fogueiras.Count; i++)
            {
                Light l = Fogueiras[i];
                if (l == null) continue;
                // Tremula como chama, nao como lampada.
                float f = Mathf.PerlinNoise(_tempo * 3.1f + i * 7.3f, i) * 0.7f + Mathf.PerlinNoise(_tempo * 9 + i, 3) * 0.3f;
                l.intensity = 2.2f + f * 1.6f;
            }
        }

        public VisualDeObjeto VisualDe(ObjetoDeCena o)
        {
            foreach (VisualDeObjeto v in Objetos)
                if (v != null && v.O == o) return v;
            return null;
        }
    }

    /// <summary>
    /// Constroi a cena de um mapa a partir do que o nucleo gerou. Tudo que e
    /// estatico (chao, muro, enfeite, grama) e fundido em poucas malhas por
    /// trecho de 24 m: o URP desenha cada trecho de uma vez e o recorte de
    /// camera ainda funciona.
    /// </summary>
    public static class ConstrutorDeCena
    {
        private const float Trecho = 24f;

        public static Cenario Construir(Mapa mapa, MundoSim mundo, Transform pai)
        {
            var raiz = new GameObject("Cenario: " + mapa.Id);
            raiz.transform.SetParent(pai, false);
            var cena = raiz.AddComponent<Cenario>();
            cena.Mapa = mapa;
            cena.Mundo = mundo;

            Chao(mapa, raiz.transform);
            Muros(mapa, raiz.transform);
            AnelDeLabirinto(mapa, raiz.transform);
            Pecas(mapa, raiz.transform, cena);
            if (mapa.Tema == Tema.Patio) Grama(mapa, raiz.transform, 2.6f);
            else if (mapa.Tema == Tema.Hera) Grama(mapa, raiz.transform, 0.35f);
            foreach (ObjetoDeCena o in mundo.Objetos)
            {
                VisualDeObjeto v = VisualDeObjeto.Criar(o, mapa.Tema, raiz.transform);
                if (v != null) cena.Objetos.Add(v);
            }
            return cena;
        }

        // ------------------------------------------------------------------ chao

        private static void Chao(Mapa mapa, Transform pai)
        {
            float margem = 60;
            var c = new ConstrutorDeMalha();
            for (float x = -margem; x < mapa.Largura + margem; x += Trecho * 2)
            for (float z = -margem; z < mapa.Profundidade + margem; z += Trecho * 2)
            {
                c.Limpar();
                float w = Trecho * 2;
                c.Cor(Color.white).Quad(new Vector3(x, 0, z), new Vector3(x, 0, z + w), new Vector3(x + w, 0, z + w), new Vector3(x + w, 0, z), Vector3.up);
                GameObject g = Util.Filho(pai, "chao");
                MeshRenderer r = Util.ComMalha(g, c.Gerar("chao"), Paleta.Chao(mapa.ChaoPadrao), false);
                r.receiveShadows = true;
            }
            if (mapa.Tema == Tema.Patio)
            {
                // Fora do muro do Patio, o chao ja e o do labirinto.
                Retangulo fora = new Retangulo(-margem, -margem, mapa.Largura + margem, mapa.Profundidade + margem);
                ZonaComBorda(pai, fora, TipoDeChao.Pedra, 0, true, new Retangulo(ConstrutorDoPatio.Borda0 - 1.5f, ConstrutorDoPatio.Borda0 - 1.5f, ConstrutorDoPatio.Borda1 + 1.5f, ConstrutorDoPatio.Borda1 + 1.5f));
            }
            int ordem = 0;
            foreach (ZonaDeChao z in mapa.Chao) ZonaComBorda(pai, z.Area, z.Tipo, ++ordem, false, default, z.Circular);
        }

        /// <summary>
        /// Zona de chao com borda que se desfaz: alfa 1 no miolo e 0 no anel
        /// de fora; o shader recorta o anel pelo ruido. Com "furo", a zona e a
        /// moldura em volta de um retangulo vazio.
        /// </summary>
        private static void ZonaComBorda(Transform pai, Retangulo r, TipoDeChao tipo, int ordem, bool comFuro, Retangulo furo, bool circular = false)
        {
            var c = new ConstrutorDeMalha();
            float y = 0.004f + (ordem % 7) * 0.0015f;
            float borda = 1.4f;
            if (circular)
            {
                Vector3 centro = new Vector3(r.Centro.X, y, r.Centro.Y);
                float raio = r.Largura / 2;
                int lados = 40;
                c.Cor(Color.white, 1);
                for (int i = 0; i < lados; i++)
                {
                    float a0 = i / (float)lados * Mathf.PI * 2, a1 = (i + 1) / (float)lados * Mathf.PI * 2;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                    c.Cor(Color.white, 1).TrianguloChapado(centro, centro + d1 * (raio - borda), centro + d0 * (raio - borda), Vector3.up);
                    QuadDeBorda(c, centro + d0 * (raio - borda), centro + d1 * (raio - borda), centro + d1 * raio, centro + d0 * raio);
                }
            }
            else if (!comFuro)
            {
                Retangulo dentro = new Retangulo(r.X0 + borda, r.Z0 + borda, r.X1 - borda, r.Z1 - borda);
                if (dentro.Largura <= 0 || dentro.Profundidade <= 0) dentro = Retangulo.Centrado(r.Centro.X, r.Centro.Y, 0.1f, 0.1f);
                c.Cor(Color.white, 1).Quad(V(dentro.X0, y, dentro.Z0), V(dentro.X0, y, dentro.Z1), V(dentro.X1, y, dentro.Z1), V(dentro.X1, y, dentro.Z0), Vector3.up);
                Moldura(c, r, dentro, y);
            }
            else
            {
                Retangulo dentro = new Retangulo(furo.X0 - borda, furo.Z0 - borda, furo.X1 + borda, furo.Z1 + borda);
                // Quatro faixas cheias entre o externo e o furo com borda.
                c.Cor(Color.white, 1);
                c.Quad(V(r.X0, y, r.Z0), V(r.X0, y, dentro.Z0), V(r.X1, y, dentro.Z0), V(r.X1, y, r.Z0), Vector3.up);
                c.Quad(V(r.X0, y, dentro.Z1), V(r.X0, y, r.Z1), V(r.X1, y, r.Z1), V(r.X1, y, dentro.Z1), Vector3.up);
                c.Quad(V(r.X0, y, dentro.Z0), V(r.X0, y, dentro.Z1), V(dentro.X0, y, dentro.Z1), V(dentro.X0, y, dentro.Z0), Vector3.up);
                c.Quad(V(dentro.X1, y, dentro.Z0), V(dentro.X1, y, dentro.Z1), V(r.X1, y, dentro.Z1), V(r.X1, y, dentro.Z0), Vector3.up);
                MolduraParaDentro(c, dentro, furo, y);
            }
            GameObject g = Util.Filho(pai, "zona:" + tipo);
            MeshRenderer mr = Util.ComMalha(g, c.Gerar("zona"), Paleta.Chao(tipo), false);
            mr.receiveShadows = true;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static void QuadDeBorda(ConstrutorDeMalha c, Vector3 a0, Vector3 a1, Vector3 b1, Vector3 b0)
        {
            // a = lado de dentro (alfa 1), b = lado de fora (alfa 0).
            c.Cor(Color.white, 1);
            int ia0 = c.Vertices;
            c.Quad(a0, a1, b1, b0, Vector3.up);
            c.AjustarAlfa(ia0 + 2, 0);
            c.AjustarAlfa(ia0 + 3, 0);
        }

        private static void Moldura(ConstrutorDeMalha c, Retangulo fora, Retangulo dentro, float y)
        {
            QuadDeBorda(c, V(dentro.X0, y, dentro.Z0), V(dentro.X1, y, dentro.Z0), V(fora.X1, y, fora.Z0), V(fora.X0, y, fora.Z0));
            QuadDeBorda(c, V(dentro.X1, y, dentro.Z1), V(dentro.X0, y, dentro.Z1), V(fora.X0, y, fora.Z1), V(fora.X1, y, fora.Z1));
            QuadDeBorda(c, V(dentro.X0, y, dentro.Z1), V(dentro.X0, y, dentro.Z0), V(fora.X0, y, fora.Z0), V(fora.X0, y, fora.Z1));
            QuadDeBorda(c, V(dentro.X1, y, dentro.Z0), V(dentro.X1, y, dentro.Z1), V(fora.X1, y, fora.Z1), V(fora.X1, y, fora.Z0));
        }

        private static void MolduraParaDentro(ConstrutorDeMalha c, Retangulo cheio, Retangulo vazio, float y)
        {
            QuadDeBorda(c, V(cheio.X0, y, cheio.Z0), V(cheio.X1, y, cheio.Z0), V(vazio.X1, y, vazio.Z0), V(vazio.X0, y, vazio.Z0));
            QuadDeBorda(c, V(cheio.X1, y, cheio.Z1), V(cheio.X0, y, cheio.Z1), V(vazio.X0, y, vazio.Z1), V(vazio.X1, y, vazio.Z1));
            QuadDeBorda(c, V(cheio.X0, y, cheio.Z1), V(cheio.X0, y, cheio.Z0), V(vazio.X0, y, vazio.Z0), V(vazio.X0, y, vazio.Z1));
            QuadDeBorda(c, V(cheio.X1, y, cheio.Z0), V(cheio.X1, y, cheio.Z1), V(vazio.X1, y, vazio.Z1), V(vazio.X1, y, vazio.Z0));
        }

        // ------------------------------------------------------------------ muros

        private static void Muros(Mapa mapa, Transform pai)
        {
            var grupos = new Dictionary<(string, int, int), ConstrutorDeMalha>();
            foreach (TrechoDeMuro m in mapa.Muros)
            {
                string estilo = Paleta.EstiloDoMuro(m.Estilo, mapa.Tema);
                Vector2 centro = new Vector2(m.Area.Centro.X, m.Area.Centro.Y);
                var chave = (estilo, Mathf.FloorToInt(centro.x / Trecho), Mathf.FloorToInt(centro.y / Trecho));
                if (!grupos.TryGetValue(chave, out ConstrutorDeMalha c))
                {
                    c = new ConstrutorDeMalha();
                    grupos[chave] = c;
                }
                float h = m.Altura;
                if (m.Estilo == "verga")
                {
                    // Viga de concreto atravessando o alto do vao do portao.
                    float y0 = h - 2.4f;
                    c.Cor(Color.white, Mathf.Clamp01(h / 30f)).Caixa(new Vector3(centro.x, (y0 + h) / 2, centro.y), new Vector3(m.Area.Largura, h - y0, m.Area.Profundidade), 0.08f);
                    continue;
                }
                c.Cor(Color.white, Mathf.Clamp01(h / 30f)).Caixa(new Vector3(centro.x, h / 2, centro.y), new Vector3(m.Area.Largura, h, m.Area.Profundidade), m.Estilo == "pilar" ? 0.12f : 0.05f);
            }
            foreach (KeyValuePair<(string, int, int), ConstrutorDeMalha> kv in grupos)
            {
                GameObject g = Util.Filho(pai, "muro:" + kv.Key.Item1);
                MeshRenderer r = Util.ComMalha(g, kv.Value.Gerar("muro"), Paleta.Muro(kv.Key.Item1));
                r.receiveShadows = true;
            }
        }

        /// <summary>
        /// Muros de enfeite em volta do mapa, fora da area de jogo: de cima, o
        /// labirinto parece continuar ate o horizonte em vez de acabar num vazio.
        /// </summary>
        private static void AnelDeLabirinto(Mapa mapa, Transform pai)
        {
            if (mapa.Tema == Tema.Laboratorio) return;
            var rng = new Random(mapa.Semente ^ 0x51ab);
            float t = 7f, faixa = 42f;
            float h = mapa.AlturaDosMuros;
            var c = new ConstrutorDeMalha();
            float x0 = -faixa, z0 = -faixa, x1 = mapa.Largura + faixa, z1 = mapa.Profundidade + faixa;
            for (float x = x0; x < x1; x += t)
            for (float z = z0; z < z1; z += t)
            {
                bool dentro = x > -3 && x < mapa.Largura + 3 - t && z > -3 && z < mapa.Profundidade + 3 - t;
                if (dentro) continue;
                float alt = h * (0.9f + (float)rng.NextDouble() * 0.35f);
                if (rng.NextDouble() < 0.55)
                    c.Cor(Color.white, Mathf.Clamp01(alt / 30f)).Caixa(new Vector3(x + t / 2, alt / 2, z), new Vector3(t + 1.4f, alt, 1.4f), 0.05f);
                if (rng.NextDouble() < 0.55)
                    c.Cor(Color.white, Mathf.Clamp01(alt / 30f)).Caixa(new Vector3(x, alt / 2, z + t / 2), new Vector3(1.4f, alt, t + 1.4f), 0.05f);
            }
            string estilo = mapa.Tema == Tema.Ferrugem || mapa.Tema == Tema.Engrenagens ? "metal" : mapa.Tema == Tema.Nucleo ? "nucleo" : "hera";
            GameObject g = Util.Filho(pai, "anel");
            MeshRenderer r = Util.ComMalha(g, c.Gerar("anel"), Paleta.Muro(estilo));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ pecas

        private static void Pecas(Mapa mapa, Transform pai, Cenario cena)
        {
            var comContorno = new Dictionary<(int, int), ConstrutorDeMalha>();
            var semContorno = new Dictionary<(int, int), ConstrutorDeMalha>();
            foreach (Peca p in mapa.Pecas)
            {
                var chave = (Mathf.FloorToInt(p.Pos.X / Trecho), Mathf.FloorToInt(p.Pos.Y / Trecho));
                Dictionary<(int, int), ConstrutorDeMalha> dic = FabricaDePecas.SemContorno(p.Tipo) ? semContorno : comContorno;
                if (!dic.TryGetValue(chave, out ConstrutorDeMalha c))
                {
                    c = new ConstrutorDeMalha();
                    dic[chave] = c;
                }
                FabricaDePecas.Desenhar(c, p);
                Extras(p, pai, cena);
            }
            foreach (ConstrutorDeMalha c in comContorno.Values)
            {
                MeshRenderer r = Util.ComMalha(Util.Filho(pai, "pecas"), c.Gerar("pecas"), Paleta.ToonCenario);
                r.receiveShadows = true;
            }
            foreach (ConstrutorDeMalha c in semContorno.Values)
            {
                MeshRenderer r = Util.ComMalha(Util.Filho(pai, "miudezas"), c.Gerar("miudezas"), Paleta.ToonSemContorno, false);
                r.receiveShadows = true;
            }
        }

        /// <summary>O que uma peca tem alem da malha: luz, fogo.</summary>
        private static void Extras(Peca p, Transform pai, Cenario cena)
        {
            Vector3 pos = new Vector3(p.Pos.X, 0, p.Pos.Y);
            switch (p.Tipo)
            {
                case "fogueira":
                {
                    GameObject g = Util.Filho(pai, "fogueira", pos);
                    Efeitos.Particulas.FogoContinuo(g.transform, new Vector3(0, 0.15f, 0), 0.32f, new Color(1f, 0.6f, 0.25f), 38);
                    Light l = Luz(g.transform, new Vector3(0, 1.2f, 0), new Color(1f, 0.55f, 0.25f), 3f, 11f, true);
                    cena.Fogueiras.Add(l);
                    break;
                }
                case "poste":
                {
                    Quaternion rot = Quaternion.Euler(0, -p.Direcao * Mathf.Rad2Deg, 0);
                    GameObject g = Util.Filho(pai, "poste", pos + rot * new Vector3(0.6f, 2.7f, 0));
                    cena.LuzesDaNoite.Add(Luz(g.transform, Vector3.zero, new Color(1f, 0.75f, 0.45f), 2.6f, 10f, false));
                    break;
                }
                case "monta_cargas":
                {
                    GameObject g = Util.Filho(pai, "luz_monta_cargas", pos + new Vector3(0, 3.8f, 0));
                    cena.LuzesDaNoite.Add(Luz(g.transform, Vector3.zero, new Color(1f, 0.6f, 0.2f), 2f, 9f, false));
                    break;
                }
                case "cristal_grande":
                case "coluna_dados":
                {
                    GameObject g = Util.Filho(pai, "luz", pos + Vector3.up * 1.2f);
                    Luz(g.transform, Vector3.zero, new Color(0.3f, 0.9f, 1f), 1.6f, 6f, false);
                    break;
                }
                case "buraco":
                {
                    GameObject g = Util.Filho(pai, "luz", pos + Vector3.down * 0.5f);
                    Luz(g.transform, Vector3.zero, new Color(1f, 0.35f, 0.1f), 1.5f, 4f, false);
                    break;
                }
            }
        }

        private static Light Luz(Transform pai, Vector3 local, Color cor, float intensidade, float alcance, bool sombra)
        {
            GameObject g = Util.Filho(pai, "luz", local);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = cor;
            l.intensity = intensidade;
            l.range = alcance;
            l.shadows = sombra ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ grama

        /// <summary>
        /// Laminas de grama onde o chao e grama e nada pisa: fora de muro, de
        /// cabana, de caminho e de plantacao. Densidade em touceiras por m2.
        /// </summary>
        private static void Grama(Mapa mapa, Transform pai, float densidade)
        {
            var rng = new Random(mapa.Semente ^ 0x6a3a);
            Grade g = mapa.Grade;
            var trechos = new Dictionary<(int, int), ConstrutorDeMalha>();
            float passo = 1f / Mathf.Sqrt(densidade);
            for (float x = 1; x < mapa.Largura - 1; x += passo)
            for (float z = 1; z < mapa.Profundidade - 1; z += passo)
            {
                float px = x + (float)(rng.NextDouble() - 0.5) * passo, pz = z + (float)(rng.NextDouble() - 0.5) * passo;
                var p2 = new System.Numerics.Vector2(px, pz);
                if (g.FolgaEm(p2) < 0.35f) continue;
                if (mapa.Tema != Tema.Patio)
                {
                    // No labirinto, so tufo no pe do muro.
                    if (g.FolgaEm(p2) > 1.2f) continue;
                }
                else if (!EmGrama(mapa, px, pz)) continue;
                var chave = (Mathf.FloorToInt(px / 16), Mathf.FloorToInt(pz / 16));
                if (!trechos.TryGetValue(chave, out ConstrutorDeMalha c))
                {
                    c = new ConstrutorDeMalha();
                    trechos[chave] = c;
                }
                Touceira(c, new Vector3(px, 0, pz), rng, mapa.Tema == Tema.Patio ? 1f : 0.7f);
            }
            foreach (ConstrutorDeMalha c in trechos.Values)
            {
                MeshRenderer r = Util.ComMalha(Util.Filho(pai, "grama"), c.GerarSemSuave("grama"), Paleta.Grama, false);
                r.receiveShadows = true;
            }
        }

        private static bool EmGrama(Mapa mapa, float x, float z)
        {
            if (x < ConstrutorDoPatio.Borda0 + 1.6f || x > ConstrutorDoPatio.Borda1 - 1.6f || z < ConstrutorDoPatio.Borda0 + 1.6f || z > ConstrutorDoPatio.Borda1 - 1.6f) return false;
            foreach (ZonaDeChao zona in mapa.Chao)
            {
                Retangulo r = zona.Area;
                bool dentro = zona.Circular
                    ? new Vector2(x - r.Centro.X, z - r.Centro.Y).magnitude < r.Largura / 2 - 0.6f
                    : x > r.X0 + 0.6f && x < r.X1 - 0.6f && z > r.Z0 + 0.6f && z < r.Z1 - 0.6f;
                if (dentro) return false;
            }
            return true;
        }

        private static void Touceira(ConstrutorDeMalha c, Vector3 centro, Random rng, float escala)
        {
            Color tom = Color.Lerp(new Color(0.85f, 0.95f, 0.8f), new Color(1.1f, 1.0f, 0.75f), (float)rng.NextDouble());
            int laminas = 5 + rng.Next(4);
            for (int i = 0; i < laminas; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2;
                Vector3 b = centro + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (float)rng.NextDouble() * 0.25f;
                float alt = (0.22f + (float)rng.NextDouble() * 0.35f) * escala;
                float giro = (float)rng.NextDouble() * Mathf.PI * 2;
                Vector3 lado = new Vector3(Mathf.Cos(giro), 0, Mathf.Sin(giro)) * 0.035f;
                Vector3 pende = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * alt * 0.35f;
                c.Cor(tom).Lamina(b - lado, b + lado, b + pende + Vector3.up * alt);
            }
        }
    }
}
