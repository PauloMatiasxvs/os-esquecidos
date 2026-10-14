using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Visual;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo.Efeitos
{
    /// <summary>
    /// Aviso no chao do golpe que vem: contorno vermelho e um preenchimento
    /// que cresce ate a hora do bote. E o tempo que o jogador tem para rolar.
    /// </summary>
    public sealed class Indicador : MonoBehaviour
    {
        private Transform _cheio;
        private float _duracao, _t;
        private Transform _seguir;
        private MeshRenderer _contorno, _miolo;
        private static MaterialPropertyBlock _mpb;

        public static void Circulo(Transform pai, Vector3 centro, float raio, float duracao, Color? cor = null)
        {
            Indicador i = Novo(pai, centro, duracao);
            Color c = cor ?? new Color(1f, 0.15f, 0.08f);
            var anel = new ConstrutorDeMalha();
            Disco(anel, raio, raio * 0.92f, c, 0.75f);
            i._contorno = Util.ComMalha(Util.Filho(i.transform, "contorno"), anel.Gerar("ind_anel"), Paleta.Mistura("indicador", 3, 1.6f), false);
            var miolo = new ConstrutorDeMalha();
            Disco(miolo, raio, 0, c, 0.32f);
            GameObject g = Util.Filho(i.transform, "miolo");
            i._miolo = Util.ComMalha(g, miolo.Gerar("ind_disco"), Paleta.Mistura("indicador", 3, 1.6f), false);
            i._cheio = g.transform;
        }

        public static void Linha(Transform pai, Vector3 de, Vector3 para, float comprimento, float largura, float duracao)
        {
            Vector3 dir = para - de;
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            Indicador i = Novo(pai, de, duracao);
            i.transform.rotation = Quaternion.LookRotation(dir.normalized);
            Color c = new Color(1f, 0.15f, 0.08f);
            var m = new ConstrutorDeMalha();
            m.Cor(c, 0.75f);
            float w = largura / 2;
            Retangulo(m, -w, 0, w, comprimento, 0.12f);
            i._contorno = Util.ComMalha(Util.Filho(i.transform, "contorno"), m.Gerar("ind_linha"), Paleta.Mistura("indicador", 3, 1.6f), false);
            var cheio = new ConstrutorDeMalha();
            cheio.Cor(c, 0.32f).Quad(new Vector3(-w, 0.05f, 0), new Vector3(-w, 0.05f, 1), new Vector3(w, 0.05f, 1), new Vector3(w, 0.05f, 0), Vector3.up);
            GameObject g = Util.Filho(i.transform, "miolo");
            i._miolo = Util.ComMalha(g, cheio.Gerar("ind_cheio"), Paleta.Mistura("indicador", 3, 1.6f), false);
            i._cheio = g.transform;
            i._cheio.localScale = new Vector3(1, 1, 0.001f);
            i._comprimento = comprimento;
        }

        public static void Cone(Transform pai, Transform dono, float raio, float angulo, float duracao)
        {
            Indicador i = Novo(pai, dono.position, duracao);
            i._seguir = dono;
            Color c = new Color(1f, 0.15f, 0.08f);
            var m = new ConstrutorDeMalha();
            Setor(m, raio, angulo, c, 0.3f);
            GameObject g = Util.Filho(i.transform, "miolo");
            i._miolo = Util.ComMalha(g, m.Gerar("ind_cone"), Paleta.Mistura("indicador", 3, 1.6f), false);
            i._cheio = g.transform;
        }

        private float _comprimento;

        private static Indicador Novo(Transform pai, Vector3 centro, float duracao)
        {
            var g = new GameObject("indicador");
            g.transform.SetParent(pai, false);
            g.transform.position = new Vector3(centro.x, 0, centro.z);
            var i = g.AddComponent<Indicador>();
            i._duracao = Mathf.Max(0.1f, duracao);
            return i;
        }

        private static void Disco(ConstrutorDeMalha m, float fora, float dentro, Color c, float alfa)
        {
            int lados = 48;
            m.Cor(c, alfa);
            for (int k = 0; k < lados; k++)
            {
                float a0 = k / (float)lados * Mathf.PI * 2, a1 = (k + 1) / (float)lados * Mathf.PI * 2;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 y = Vector3.up * 0.05f;
                if (dentro <= 0) m.TrianguloChapado(y, d1 * fora + y, d0 * fora + y, Vector3.up);
                else m.Quad(d0 * dentro + y, d1 * dentro + y, d1 * fora + y, d0 * fora + y, Vector3.up);
            }
        }

        private static void Retangulo(ConstrutorDeMalha m, float x0, float z0, float x1, float z1, float esp)
        {
            Vector3 y = Vector3.up * 0.05f;
            m.Quad(new Vector3(x0, 0, z0) + y, new Vector3(x0, 0, z1) + y, new Vector3(x0 + esp, 0, z1) + y, new Vector3(x0 + esp, 0, z0) + y, Vector3.up);
            m.Quad(new Vector3(x1 - esp, 0, z0) + y, new Vector3(x1 - esp, 0, z1) + y, new Vector3(x1, 0, z1) + y, new Vector3(x1, 0, z0) + y, Vector3.up);
            m.Quad(new Vector3(x0, 0, z1 - esp) + y, new Vector3(x0, 0, z1) + y, new Vector3(x1, 0, z1) + y, new Vector3(x1, 0, z1 - esp) + y, Vector3.up);
        }

        private static void Setor(ConstrutorDeMalha m, float raio, float angulo, Color c, float alfa)
        {
            int lados = 24;
            float meio = angulo * Mathf.Deg2Rad / 2;
            m.Cor(c, alfa);
            Vector3 y = Vector3.up * 0.05f;
            for (int k = 0; k < lados; k++)
            {
                float a0 = -meio + 2 * meio * k / lados, a1 = -meio + 2 * meio * (k + 1) / lados;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                m.TrianguloChapado(y, d0 * raio + y, d1 * raio + y, Vector3.up);
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float p = Mathf.Clamp01(_t / _duracao);
            if (_seguir != null)
            {
                transform.position = new Vector3(_seguir.position.x, 0, _seguir.position.z);
                Transform raiz = _seguir.Find("raiz");
                if (raiz != null) transform.rotation = Quaternion.Euler(0, raiz.eulerAngles.y, 0);
                if (_cheio != null) _cheio.localScale = new Vector3(p, 1, p);
            }
            else if (_comprimento > 0)
            {
                _cheio.localScale = new Vector3(1, 1, _comprimento * p);
            }
            else if (_cheio != null)
            {
                _cheio.localScale = new Vector3(p, 1, p);
            }
            // Pisca nos ultimos instantes: agora!
            float pisca = p > 0.8f ? (Mathf.Sin(_t * 40) * 0.5f + 0.5f) : 1;
            if (_miolo != null)
            {
                _mpb ??= new MaterialPropertyBlock();
                _miolo.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Cor", new Color(1, 1, 1, 0.6f + 0.4f * pisca));
                _miolo.SetPropertyBlock(_mpb);
            }
            if (_t > _duracao + 0.08f) Destroy(gameObject);
        }
    }

    /// <summary>Rastro do golpe: um arco claro que some em um quinto de segundo.</summary>
    public sealed class Rastro : MonoBehaviour
    {
        private float _t;
        private MeshRenderer _r;
        private static MaterialPropertyBlock _mpb;
        private const float Vida = 0.2f;

        public static void Criar(Transform pai, VisualDeEntidade quem, float alcance, float angulo, Color cor, string vfx)
        {
            var g = new GameObject("rastro");
            g.transform.SetParent(pai, false);
            Transform raiz = quem.transform.Find("raiz");
            g.transform.position = quem.PosicaoVisual + Vector3.up * (vfx == "pancada" ? 0.7f : 1.1f) * quem.Escala;
            g.transform.rotation = raiz != null ? raiz.rotation : quem.transform.rotation;
            var m = new ConstrutorDeMalha();
            float meio = Mathf.Min(angulo, 359) * Mathf.Deg2Rad / 2;
            int lados = 18;
            float dentro = Mathf.Max(0.3f, alcance * 0.35f);
            Color c = vfx == "corte_fundo" || vfx == "corte_duplo" ? new Color(1f, 0.55f, 0.45f) : cor;
            for (int k = 0; k < lados; k++)
            {
                float t0 = k / (float)lados, t1 = (k + 1) / (float)lados;
                float a0 = -meio + 2 * meio * t0, a1 = -meio + 2 * meio * t1;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                // O alfa cresce no sentido do golpe: a lamina esta na ponta mais clara.
                m.Cor(c, t0 * 0.8f);
                int i0 = m.Vertices;
                m.QuadUv(d0 * dentro, d1 * dentro, d1 * alcance, d0 * alcance);
            }
            var r = Util.ComMalha(g, m.Gerar("rastro"), Paleta.Aditiva("rastro", 3, 2.2f), false);
            var rs = g.AddComponent<Rastro>();
            rs._r = r;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float a = 1 - _t / Vida;
            if (a <= 0)
            {
                Destroy(gameObject);
                return;
            }
            _mpb ??= new MaterialPropertyBlock();
            _r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Cor", new Color(1, 1, 1, a));
            _r.SetPropertyBlock(_mpb);
            transform.localScale = Vector3.one * (1 + _t * 0.6f);
        }
    }

    /// <summary>Modelos dos projeteis, com rastro.</summary>
    public static class Projeteis
    {
        public static Transform Criar(Transform pai, Projetil p)
        {
            var g = new GameObject("projetil");
            g.transform.SetParent(pai, false);
            g.transform.position = Util.Mundo(p.Pos, p.Altura);
            var c = new ConstrutorDeMalha();
            Color cor = Util.Cor(p.Poder.Cor, Color.white);
            bool brilha = false;
            float rastro = 0.12f;
            switch (p.Poder.Vfx)
            {
                case "faca":
                    c.Cor(new Color(0.8f, 0.82f, 0.85f)).Caixa(new Vector3(0, 0, 0.1f), new Vector3(0.015f, 0.05f, 0.24f), 0.005f);
                    c.Cor(new Color(0.3f, 0.2f, 0.12f)).Caixa(new Vector3(0, 0, -0.06f), new Vector3(0.025f, 0.035f, 0.1f));
                    break;
                case "dardo":
                    c.Cor(new Color(0.8f, 0.8f, 0.85f)).Tubo(new Vector3(0, 0, -0.15f), new Vector3(0, 0, 0.15f), 0.012f, 0.002f, 4);
                    c.Cor(new Color(0.9f, 0.3f, 0.2f)).Esfera(new Vector3(0, 0, -0.15f), Vector3.one * 0.03f, 3, 4);
                    break;
                case "acido":
                    c.Cor(new Color(0.5f, 1f, 0.25f), 0.2f).Esfera(Vector3.zero, Vector3.one * 0.18f, 5, 7);
                    brilha = true;
                    rastro = 0.25f;
                    break;
                case "laser":
                    c.Cor(cor, 0f).Capsula(new Vector3(0, 0, -0.35f), new Vector3(0, 0, 0.35f), 0.06f, 6);
                    brilha = true;
                    rastro = 0.08f;
                    break;
                case "raio":
                    c.Cor(cor, 0f).Esfera(Vector3.zero, Vector3.one * 0.35f, 6, 8);
                    brilha = true;
                    rastro = 0.35f;
                    break;
                case "bala":
                    c.Cor(new Color(1f, 0.85f, 0.4f), 0f).Capsula(new Vector3(0, 0, -0.25f), new Vector3(0, 0, 0.1f), 0.03f, 4);
                    brilha = true;
                    rastro = 0.06f;
                    break;
                case "choque":
                    c.Cor(new Color(0.5f, 0.75f, 1f), 0f).Esfera(Vector3.zero, Vector3.one * 0.15f, 4, 6);
                    brilha = true;
                    break;
                default:
                    c.Cor(new Color(0.5f, 0.48f, 0.44f)).Esfera(Vector3.zero, Vector3.one * 0.08f, 4, 6);
                    break;
            }
            Util.ComMalha(Util.Filho(g.transform, "modelo"), c.Gerar("projetil:" + p.Poder.Vfx), Paleta.Toon, false);
            GameObject gt = Util.Filho(g.transform, "rastro");
            var tr = gt.AddComponent<TrailRenderer>();
            tr.time = rastro;
            tr.startWidth = p.Poder.Vfx == "raio" ? 0.5f : p.Poder.Vfx == "acido" ? 0.25f : 0.12f;
            tr.endWidth = 0;
            tr.sharedMaterial = brilha ? Paleta.Aditiva("rastro", 3, 2.2f) : Paleta.Mistura("rastro_poeira", 3, 1f);
            Color inicio = brilha ? cor : new Color(0.9f, 0.88f, 0.8f, 0.5f);
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(inicio, 0), new GradientColorKey(inicio, 1) }, new[] { new GradientAlphaKey(brilha ? 1 : 0.5f, 0), new GradientAlphaKey(0, 1) });
            tr.colorGradient = grad;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (brilha)
            {
                var l = Util.Filho(g.transform, "luz").AddComponent<Light>();
                l.type = LightType.Point;
                l.color = cor;
                l.range = p.Poder.Vfx == "raio" ? 6 : 3;
                l.intensity = 2;
            }
            return g.transform;
        }
    }

    /// <summary>Granada ou missil caindo em arco ate o centro da area.</summary>
    public sealed class Lancado : MonoBehaviour
    {
        private Vector3 _de, _para;
        private float _t, _dur;

        public static void Criar(Transform pai, Vector3 de, Vector3 para, float duracao)
        {
            var g = new GameObject("lancado");
            g.transform.SetParent(pai, false);
            var c = new ConstrutorDeMalha();
            c.Cor(new Color(0.4f, 0.42f, 0.38f)).Tubo(new Vector3(0, -0.1f, 0), new Vector3(0, 0.1f, 0), 0.09f, 0.09f, 8);
            c.Cor(new Color(1f, 0.5f, 0.1f), 0f).Esfera(new Vector3(0, 0.13f, 0), Vector3.one * 0.04f, 3, 4);
            Util.ComMalha(g, c.Gerar("lancado"), Paleta.Toon, false);
            var l = g.AddComponent<Lancado>();
            l._de = de;
            l._para = para + Vector3.up * 0.12f;
            l._dur = Mathf.Max(0.1f, duracao);
            g.transform.position = de;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float t = Mathf.Clamp01(_t / _dur);
            Vector3 p = Vector3.Lerp(_de, _para, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * Mathf.Min(3f, Vector3.Distance(_de, _para) * 0.35f);
            transform.position = p;
            transform.rotation = Quaternion.Euler(_t * 400, _t * 230, 0);
        }
    }

    /// <summary>Estrelinhas girando sobre a cabeca de quem esta atordoado.</summary>
    public sealed class Estrelas : MonoBehaviour
    {
        private float _t;

        public static GameObject Criar(Transform pai, float altura)
        {
            var g = new GameObject("estrelas");
            g.transform.SetParent(pai, false);
            g.transform.localPosition = Vector3.up * altura;
            var c = new ConstrutorDeMalha();
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2;
                c.Cor(new Color(1f, 0.9f, 0.4f), 0f).Esfera(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.35f, Vector3.one * 0.07f, 3, 4);
            }
            Util.ComMalha(g, c.Gerar("estrelas"), Paleta.Toon, false);
            g.AddComponent<Estrelas>();
            return g;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            transform.localRotation = Quaternion.Euler(Mathf.Sin(_t * 3) * 12, _t * 260, 0);
        }
    }
}
