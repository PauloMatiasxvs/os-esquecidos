using System.Collections.Generic;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo.Atores
{
    /// <summary>
    /// O Tecelao: meio bicho, meio maquina. Seis patas com cinematica inversa
    /// de dois ossos: o pe fica plantado no chao ate o corpo se afastar demais,
    /// entao da um passo em arco. As patas andam em dois tripes alternados,
    /// como aranha. O ferrao de escorpiao curva por cima do corpo e pica para
    /// a frente no ataque.
    /// </summary>
    public sealed class Tecelao : VisualDeEntidade
    {
        public string Variante = "";
        public Color CorCorpo = new Color(0.5f, 0.48f, 0.44f);
        public Color CorOlhos = new Color(1f, 0.2f, 0.1f);

        private sealed class Pata
        {
            public Transform Femur, Tibia;
            public Vector3 Quadril;
            public Vector3 Repouso;
            public Vector3 Pe;
            public Vector3 PeOrigem, PeDestino;
            public float Passo = 1;
            public int Grupo;
            public int Lado;
        }

        private readonly List<Pata> _patas = new List<Pata>();
        private Transform _corpo, _abdomen, _cabeca;
        private readonly List<Transform> _ferrao = new List<Transform>();
        private float _alturaCorpo = 0.75f;
        private float _femur = 0.75f, _tibia = 0.95f;
        private Vector3 _ultimaPos;
        private Vector3 _velocidade;
        private float _morte;
        private bool _temFerrao;

        protected override void Construir()
        {
            bool rastejador = Variante == "rastejador";
            _temFerrao = Variante == "" || Variante == "alfa" || Variante == "primordial" || Variante == "noturno" || Variante == "blindado";
            if (rastejador)
            {
                _alturaCorpo = 0.42f;
                _femur = 0.7f;
                _tibia = 0.75f;
            }
            if (Variante == "mae") _alturaCorpo = 0.85f;
            AlturaDaCabeca = _alturaCorpo + 0.9f;
            Material mat = E != null && E.Chefe ? Visual.Paleta.ToonChefe : Visual.Paleta.Toon;

            _corpo = Util.Filho(Raiz, "corpo", new Vector3(0, _alturaCorpo, 0)).transform;
            Util.ComMalha(_corpo.gameObject, Torax(), mat);
            _abdomen = Util.Filho(_corpo, "abdomen", new Vector3(0, 0.08f, -0.35f)).transform;
            Util.ComMalha(_abdomen.gameObject, Abdomen(), mat);
            _cabeca = Util.Filho(_corpo, "cabeca", new Vector3(0, -0.04f, 0.5f)).transform;
            Util.ComMalha(_cabeca.gameObject, Cabeca(), mat);

            if (_temFerrao)
            {
                Mesh seg = SegmentoFerrao(false), ponta = SegmentoFerrao(true);
                Transform pai = _abdomen;
                Vector3 pos = new Vector3(0, 0.35f, -0.55f);
                for (int i = 0; i < 6; i++)
                {
                    Transform t = Util.Filho(pai, "ferrao" + i, pos).transform;
                    Util.ComMalha(t.gameObject, i == 5 ? ponta : seg, mat);
                    _ferrao.Add(t);
                    pai = t;
                    pos = new Vector3(0, 0, 0.22f - i * 0.012f);
                }
            }

            Mesh femur = Osso(0.075f, 0.05f, true), tibia = Osso(0.05f, 0.012f, false);
            float[] zs = rastejador ? new[] { 0.35f, 0.05f, -0.25f } : new[] { 0.32f, 0.08f, -0.16f };
            float[] zPe = rastejador ? new[] { 1.1f, 0.15f, -0.9f } : new[] { 1.0f, 0.2f, -0.7f };
            for (int i = 0; i < 6; i++)
            {
                int par = i / 2;
                int lado = i % 2 == 0 ? -1 : 1;
                var p = new Pata
                {
                    Lado = lado,
                    Grupo = (par + (lado > 0 ? 1 : 0)) % 2,
                    Quadril = new Vector3(lado * 0.26f, -0.02f, zs[par]),
                    Repouso = new Vector3(lado * (rastejador ? 1.35f : 1.15f), 0, zPe[par]),
                };
                p.Femur = Util.Filho(_corpo, "femur" + i).transform;
                Util.ComMalha(p.Femur.gameObject, femur, mat);
                p.Tibia = Util.Filho(Raiz, "tibia" + i).transform;
                Util.ComMalha(p.Tibia.gameObject, tibia, mat);
                _patas.Add(p);
            }
            _ultimaPos = transform.position;
            foreach (Pata p in _patas)
            {
                p.Pe = PeDesejado(p, Vector3.zero);
                p.PeOrigem = p.PeDestino = p.Pe;
            }
        }

        private Color Metal => Color.Lerp(new Color(0.32f, 0.34f, 0.37f), CorCorpo, 0.25f);

        private Mesh Torax()
        {
            var c = new ConstrutorDeMalha();
            c.Cor(CorCorpo).Esfera(Vector3.zero, new Vector3(0.36f, 0.24f, 0.45f), 8, 12);
            c.Cor(Metal).Caixa(new Vector3(0, 0.17f, 0.05f), new Vector3(0.42f, 0.08f, 0.5f), 0.04f);
            c.Cor(Util.Escurecer(Metal, 0.7f));
            for (int i = 0; i < 3; i++) c.Caixa(new Vector3(0, 0.22f, -0.15f + i * 0.17f), new Vector3(0.3f, 0.03f, 0.05f), 0.01f);
            if (Variante == "blindado" || Variante == "alfa" || Variante == "primordial")
            {
                c.Cor(Metal).Caixa(new Vector3(-0.3f, 0.05f, 0.05f), new Vector3(0.1f, 0.2f, 0.45f), 0.03f);
                c.Caixa(new Vector3(0.3f, 0.05f, 0.05f), new Vector3(0.1f, 0.2f, 0.45f), 0.03f);
            }
            if (Variante == "noturno" || Variante == "alfa")
            {
                c.Cor(Util.Escurecer(CorCorpo, 0.5f));
                for (int i = 0; i < 4; i++) c.Tubo(new Vector3(0, 0.2f, -0.2f + i * 0.13f), new Vector3(0, 0.42f, -0.28f + i * 0.13f), 0.04f, 0f, 5);
            }
            return c.Gerar("tecelao_torax");
        }

        private Mesh Abdomen()
        {
            var c = new ConstrutorDeMalha();
            float tam = Variante == "mae" ? 1.6f : Variante == "jovem" ? 0.8f : 1f;
            Vector3 r = new Vector3(0.48f, 0.4f, 0.62f) * tam;
            Color carne = Color.Lerp(CorCorpo, new Color(0.75f, 0.68f, 0.6f), 0.35f);
            c.Cor(carne).Esfera(new Vector3(0, 0.12f * tam, -0.35f * tam), r, 10, 14);
            // Veias e placas segmentadas: o tecido vivo costurado no metal.
            c.Cor(Util.Escurecer(carne, 0.6f));
            for (int i = 0; i < 4; i++)
            {
                float z = -0.05f - i * 0.18f * tam;
                c.Toro(new Vector3(0, 0.12f * tam, z), r.x * (0.95f - Mathf.Abs(i - 1.5f) * 0.12f), 0.025f, 16, 4);
            }
            c.Cor(Metal).Caixa(new Vector3(0, 0.12f * tam + r.y * 0.92f, -0.35f * tam), new Vector3(0.18f, 0.06f, r.z * 1.4f), 0.03f);
            switch (Variante)
            {
                case "cuspidor":
                    c.Cor(new Color(0.55f, 1f, 0.25f), 0.2f).Esfera(new Vector3(0, 0.3f, -0.45f), new Vector3(0.32f, 0.3f, 0.36f), 8, 12);
                    break;
                case "mae":
                    c.Cor(new Color(1f, 0.55f, 0.2f), 0.3f);
                    for (int i = 0; i < 9; i++)
                    {
                        float ang = i * 0.7f;
                        c.Esfera(new Vector3(Mathf.Cos(ang) * 0.55f, 0.3f + (i % 3) * 0.18f, -0.55f + Mathf.Sin(ang) * 0.5f), Vector3.one * 0.13f, 5, 8);
                    }
                    break;
                case "blindado":
                    c.Cor(Metal);
                    for (int i = 0; i < 4; i++) c.Caixa(new Vector3(0, 0.12f + r.y * 0.75f, -0.1f - i * 0.17f), new Vector3(r.x * 1.7f, 0.06f, 0.15f), 0.02f);
                    break;
                case "primordial":
                    c.Cor(new Color(0.3f, 1f, 1f), 0.2f);
                    for (int i = 0; i < 7; i++)
                    {
                        float ang = i * 0.9f;
                        Vector3 b = new Vector3(Mathf.Cos(ang) * 0.3f, 0.4f, -0.4f + Mathf.Sin(ang) * 0.4f);
                        c.Tubo(b, b + new Vector3(Mathf.Cos(ang) * 0.15f, 0.35f + (i % 2) * 0.15f, Mathf.Sin(ang) * 0.1f), 0.07f, 0f, 5);
                    }
                    break;
            }
            return c.Gerar("tecelao_abdomen:" + Variante);
        }

        private Mesh Cabeca()
        {
            var c = new ConstrutorDeMalha();
            c.Cor(Util.Escurecer(CorCorpo, 0.85f)).Esfera(Vector3.zero, new Vector3(0.22f, 0.16f, 0.22f), 8, 10);
            c.Cor(Metal).Caixa(new Vector3(0, 0.1f, -0.02f), new Vector3(0.26f, 0.05f, 0.22f), 0.02f);
            // Cacho de olhos: o que brilha no escuro do corredor.
            c.Cor(CorOlhos, 0f);
            c.Esfera(new Vector3(-0.07f, 0.06f, 0.17f), Vector3.one * 0.045f, 4, 6);
            c.Esfera(new Vector3(0.07f, 0.06f, 0.17f), Vector3.one * 0.045f, 4, 6);
            c.Esfera(new Vector3(-0.12f, 0.03f, 0.13f), Vector3.one * 0.028f, 3, 5);
            c.Esfera(new Vector3(0.12f, 0.03f, 0.13f), Vector3.one * 0.028f, 3, 5);
            c.Esfera(new Vector3(-0.035f, 0.1f, 0.15f), Vector3.one * 0.022f, 3, 5);
            c.Esfera(new Vector3(0.035f, 0.1f, 0.15f), Vector3.one * 0.022f, 3, 5);
            // Mandibulas.
            c.Cor(new Color(0.25f, 0.23f, 0.22f));
            for (int l = -1; l <= 1; l += 2)
            {
                c.Tubo(new Vector3(l * 0.08f, -0.06f, 0.15f), new Vector3(l * 0.1f, -0.12f, 0.3f), 0.035f, 0.025f, 5);
                c.Tubo(new Vector3(l * 0.1f, -0.12f, 0.3f), new Vector3(l * 0.03f, -0.13f, 0.4f), 0.025f, 0.0f, 5);
            }
            if (Variante == "cuspidor") c.Cor(new Color(0.55f, 1f, 0.25f), 0.3f).Tubo(new Vector3(0, -0.04f, 0.18f), new Vector3(0, -0.05f, 0.33f), 0.05f, 0.04f, 6);
            return c.Gerar("tecelao_cabeca:" + Util.Hex(CorOlhos) + Variante);
        }

        private Mesh SegmentoFerrao(bool ponta)
        {
            var c = new ConstrutorDeMalha();
            if (ponta)
            {
                c.Cor(new Color(0.85f, 0.85f, 0.75f)).Tubo(Vector3.zero, new Vector3(0, -0.05f, 0.3f), 0.06f, 0f, 6);
                c.Cor(new Color(0.6f, 1f, 0.3f), 0.2f).Esfera(new Vector3(0, -0.04f, 0.26f), Vector3.one * 0.03f, 3, 4);
            }
            else
            {
                c.Cor(Metal).Esfera(new Vector3(0, 0, 0.1f), new Vector3(0.09f, 0.08f, 0.13f), 5, 8);
                c.Cor(Util.Escurecer(Metal, 0.7f)).Caixa(new Vector3(0, 0.07f, 0.1f), new Vector3(0.04f, 0.04f, 0.12f));
            }
            return c.Gerar("ferrao" + ponta);
        }

        /// <summary>Osso de pata com comprimento 1 no eixo +Z: o transform estica para o tamanho real.</summary>
        private Mesh Osso(float r0, float r1, bool juntaNoFim)
        {
            var c = new ConstrutorDeMalha();
            c.Cor(Metal).Tubo(Vector3.zero, Vector3.forward, r0, r1, 6);
            if (juntaNoFim) c.Cor(CorCorpo).Esfera(Vector3.forward, Vector3.one * r0 * 1.2f, 4, 6);
            return c.Gerar("osso");
        }

        // ------------------------------------------------------------------ animacao

        private Vector3 PeDesejado(Pata p, Vector3 adiante)
        {
            Vector3 local = p.Repouso * Mathf.Max(0.001f, 1f);
            Vector3 mundo = Raiz.TransformPoint(local) + adiante;
            mundo.y = transform.position.y - (E != null && E.Voa ? 0 : 0);
            mundo.y = 0.02f;
            return mundo;
        }

        protected override void Animar(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 mov = pos - _ultimaPos;
            _ultimaPos = pos;
            if (dt > 0 && mov.magnitude < 3) _velocidade = Util.Suavizar(_velocidade, mov / dt, 10, dt);
            float vel = new Vector3(_velocidade.x, 0, _velocidade.z).magnitude;

            // Pose do corpo: balanco ao andar, bote, empinar, ferroada.
            Vector3 corpoPos = new Vector3(0, _alturaCorpo, 0);
            Vector3 corpoRot = Vector3.zero;
            corpoPos.y += Mathf.Sin(Tempo * 9) * 0.03f * Mathf.Clamp01(vel / 3);
            corpoRot.z = Mathf.Sin(Tempo * 4.5f) * 3 * Mathf.Clamp01(vel / 3);
            Vector3 abdRot = new Vector3(Mathf.Sin(Tempo * 1.3f + E.Id) * 3, 0, 0);
            float ferraoArco = 1f, ferraoPica = 0f;

            string anim = AnimacaoAtual;
            if (Preparando(out float tp))
            {
                switch (anim)
                {
                    case "mordida":
                        corpoPos += new Vector3(0, 0.08f * tp, -0.15f * tp);
                        corpoRot.x = -10 * tp;
                        break;
                    case "ferroada":
                        ferraoArco = 1 + 0.35f * tp;
                        corpoRot.x = -6 * tp;
                        break;
                    case "investida":
                    case "salto":
                        corpoPos.y -= 0.3f * tp;
                        corpoRot.x = -8 * tp;
                        break;
                    default:
                        corpoRot.x = -20 * tp;
                        corpoPos.y += 0.2f * tp;
                        abdRot.x = 15 * tp;
                        break;
                }
            }
            else if (Recuperando(out float tr))
            {
                float s = 1 - Mathf.Pow(1 - Mathf.Clamp01(tr / 0.25f), 3);
                float volta = Mathf.SmoothStep(0, 1, (tr - 0.4f) / 0.6f);
                switch (anim)
                {
                    case "mordida":
                        corpoPos += Vector3.Lerp(new Vector3(0, 0.08f, -0.15f), new Vector3(0, -0.05f, 0.35f), s) * (1 - volta);
                        corpoRot.x = Mathf.Lerp(-10, 12, s) * (1 - volta);
                        break;
                    case "ferroada":
                        ferraoPica = s * (1 - volta);
                        ferraoArco = 1.35f * (1 - s) + 1 * s;
                        corpoRot.x = 10 * s * (1 - volta);
                        break;
                    default:
                        corpoRot.x = -20 * (1 - volta);
                        abdRot.x = 15 * (1 - s);
                        break;
                }
            }
            if (E.Acao == EstadoAcao.Investindo)
            {
                corpoPos.y -= 0.15f;
                corpoRot.x = 8;
            }
            if (Cambaleio > 0) corpoRot.x -= 12 * Cambaleio;
            if (E.Acao == EstadoAcao.Atordoado) corpoRot.z += Mathf.Sin(Tempo * 14) * 8;

            if (!E.Vivo)
            {
                _morte = Mathf.Min(1, _morte + dt * 2.5f);
                corpoPos.y = Mathf.Lerp(corpoPos.y, 0.18f, _morte);
                corpoRot.z += 18 * _morte;
            }
            _corpo.localPosition = corpoPos + Raiz.InverseTransformVector(Empurrao);
            _corpo.localRotation = Quaternion.Euler(corpoRot);
            _abdomen.localRotation = Quaternion.Euler(abdRot);
            _cabeca.localRotation = Quaternion.Euler(Mathf.Sin(Tempo * 2.3f + E.Id) * 4, Mathf.Sin(Tempo * 1.1f + E.Id) * 8, 0);

            for (int i = 0; i < _ferrao.Count; i++)
            {
                // Arco por cima do corpo; na ferroada, a ponta desce para a frente.
                float curva = Mathf.Lerp(-38, 32, ferraoPica) * ferraoArco;
                if (i == 0) curva = Mathf.Lerp(-70, -40, ferraoPica);
                float bal = Mathf.Sin(Tempo * 2 + i * 0.7f) * 3;
                _ferrao[i].localRotation = Quaternion.Euler(curva + bal, 0, 0);
            }

            AnimarPatas(dt, vel);
        }

        private void AnimarPatas(float dt, float vel)
        {
            Vector3 adiante = new Vector3(_velocidade.x, 0, _velocidade.z) * 0.18f;
            float limite = 0.42f * Escala;
            float duracao = Mathf.Lerp(0.2f, 0.09f, Mathf.Clamp01(vel / 6f));
            bool[] grupoAndando = new bool[2];
            foreach (Pata p in _patas)
                if (p.Passo < 1) grupoAndando[p.Grupo] = true;

            foreach (Pata p in _patas)
            {
                Vector3 desejado = PeDesejado(p, adiante);
                if (!E.Vivo)
                {
                    // Patas encolhem para baixo do corpo, como inseto morto.
                    Vector3 sob = Raiz.TransformPoint(new Vector3(p.Lado * 0.35f, 0.3f, p.Repouso.z * 0.3f));
                    p.Pe = Vector3.Lerp(p.Pe, sob, 1 - Mathf.Exp(-6 * dt));
                }
                else if (p.Passo < 1)
                {
                    p.Passo = Mathf.Min(1, p.Passo + dt / duracao);
                    float t = Mathf.SmoothStep(0, 1, p.Passo);
                    p.PeDestino = desejado;
                    p.Pe = Vector3.Lerp(p.PeOrigem, p.PeDestino, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.28f * Escala;
                }
                else if ((desejado - p.Pe).magnitude > limite && !grupoAndando[1 - p.Grupo])
                {
                    p.PeOrigem = p.Pe;
                    p.Passo = 0;
                    grupoAndando[p.Grupo] = true;
                }
                ResolverPerna(p);
            }
        }

        /// <summary>Cinematica inversa de dois ossos: o joelho sobe para fora, como pata de aranha.</summary>
        private void ResolverPerna(Pata p)
        {
            Vector3 quadril = _corpo.TransformPoint(p.Quadril);
            float a = _femur * Escala, b = _tibia * Escala;
            Vector3 alvo = p.Pe;
            Vector3 d = alvo - quadril;
            float dist = Mathf.Clamp(d.magnitude, 0.05f, (a + b) * 0.999f);
            Vector3 dir = d.normalized;
            Vector3 fora = (Raiz.right * p.Lado);
            Vector3 polo = Vector3.up + fora * 0.35f;
            polo = (polo - Vector3.Dot(polo, dir) * dir).normalized;
            float cosA = Mathf.Clamp((a * a + dist * dist - b * b) / (2 * a * dist), -1, 1);
            float sinA = Mathf.Sqrt(1 - cosA * cosA);
            Vector3 joelho = quadril + dir * (a * cosA) + polo * (a * sinA);
            Vector3 pe = quadril + dir * dist;

            Colocar(p.Femur, quadril, joelho, Escala);
            Colocar(p.Tibia, joelho, pe, Escala);
        }

        private static void Colocar(Transform t, Vector3 de, Vector3 ate, float escala)
        {
            Vector3 v = ate - de;
            float len = v.magnitude;
            t.position = de;
            if (len > 1e-4f) t.rotation = Quaternion.LookRotation(v / len, Vector3.up);
            Vector3 escalaPai = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(escala / Mathf.Max(escalaPai.x, 1e-4f), escala / Mathf.Max(escalaPai.y, 1e-4f), len / Mathf.Max(escalaPai.z, 1e-4f));
        }
    }
}
