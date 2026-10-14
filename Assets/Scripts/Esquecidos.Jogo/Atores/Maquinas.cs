using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo.Atores
{
    /// <summary>
    /// Sentinela da ARCA: olho flutuante com aneis que giram. O Olho-de-Vidro
    /// e o mesmo desenho maior, com tres aneis e a lente enorme; o drone do
    /// Remendeiro e uma sentinela consertada com fita e chapa.
    /// </summary>
    public sealed class Drone : VisualDeEntidade
    {
        public string Variante = "";
        public Color CorCorpo = new Color(0.23f, 0.24f, 0.27f);
        public Color CorOlho = new Color(1f, 0.15f, 0.08f);

        private Transform _nucleo, _anel1, _anel2, _anel3, _feixe;
        private float _giro;
        private float _morte;
        private float _queda;

        protected override void Construir()
        {
            Material mat = E != null && E.Chefe ? Visual.Paleta.ToonChefe : Visual.Paleta.Toon;
            bool olho = Variante == "olho";
            bool remendado = Variante == "remendado";
            _nucleo = Util.Filho(Raiz, "nucleo").transform;
            var c = new ConstrutorDeMalha();
            c.Cor(CorCorpo).Esfera(Vector3.zero, Vector3.one * 0.32f, 10, 14);
            c.Cor(Util.Escurecer(CorCorpo, 0.6f)).Tubo(new Vector3(0, 0, 0.22f), new Vector3(0, 0, 0.32f), 0.2f, 0.18f, 14);
            c.Cor(CorOlho, 0f).Esfera(new Vector3(0, 0, 0.3f), new Vector3(0.14f, 0.14f, 0.06f), 6, 10);
            c.Cor(new Color(1f, 1f, 1f), 0f).Esfera(new Vector3(0.04f, 0.04f, 0.34f), Vector3.one * 0.025f, 3, 4);
            c.Cor(Util.Escurecer(CorCorpo, 0.8f));
            for (int l = -1; l <= 1; l += 2) c.Caixa(new Vector3(l * 0.33f, 0, -0.05f), new Vector3(0.08f, 0.04f, 0.28f), 0.015f);
            c.Tubo(new Vector3(0, 0.3f, -0.05f), new Vector3(0.05f, 0.5f, -0.12f), 0.012f, 0.006f, 4);
            if (remendado)
            {
                c.Cor(new Color(0.85f, 0.8f, 0.6f)).Caixa(new Vector3(0.1f, 0.25f, 0.05f), new Vector3(0.2f, 0.05f, 0.12f), 0.01f);
                c.Cor(new Color(0.6f, 0.45f, 0.25f)).Caixa(new Vector3(-0.2f, -0.1f, 0.15f), new Vector3(0.12f, 0.12f, 0.02f));
            }
            Util.ComMalha(_nucleo.gameObject, c.Gerar("drone_nucleo" + Variante + Util.Hex(CorOlho)), mat);

            _anel1 = Anel("anel1", 0.48f, 0.035f, mat);
            _anel2 = Anel("anel2", 0.55f, 0.025f, mat);
            if (olho) _anel3 = Anel("anel3", 0.65f, 0.03f, mat);

            // Cone de varredura: a luz vermelha que procura no chao.
            if (!remendado)
            {
                _feixe = Util.Filho(Raiz, "feixe").transform;
                var f = new ConstrutorDeMalha();
                f.Cor(new Color(CorOlho.r, CorOlho.g, CorOlho.b, 0.12f)).Tubo(Vector3.zero, new Vector3(0, 0, 1), 0.08f, 0.9f, 16, false);
                Util.ComMalha(_feixe.gameObject, f.Gerar("feixe"), Visual.Paleta.Aditiva("feixe", 6, 1.2f), false);
                _feixe.localPosition = new Vector3(0, 0, 0.3f);
            }
            AlturaDaCabeca = 0.9f;
        }

        private Transform Anel(string nome, float raio, float esp, Material mat)
        {
            Transform t = Util.Filho(Raiz, nome).transform;
            var c = new ConstrutorDeMalha();
            c.Cor(Util.Escurecer(CorCorpo, 1.3f)).Toro(Vector3.zero, raio, esp, 22, 5);
            c.Cor(CorOlho, 0f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2;
                c.Esfera(new Vector3(Mathf.Cos(a) * raio, 0, Mathf.Sin(a) * raio), Vector3.one * esp * 1.3f, 3, 4);
            }
            Util.ComMalha(t.gameObject, c.Gerar("anel" + raio), mat);
            return t;
        }

        protected override float AlturaDeVoo()
        {
            float h = 1.7f + Mathf.Sin(Tempo * 2.1f + E.Id) * 0.15f;
            if (!E.Vivo) h = Mathf.Max(0.3f, h - _queda);
            return h;
        }

        protected override void Animar(float dt)
        {
            _giro += dt * (E.Acao == EstadoAcao.Conjurando ? 600 : 140);
            _anel1.localRotation = Quaternion.Euler(70, _giro, 0);
            _anel2.localRotation = Quaternion.Euler(-60, -_giro * 0.7f, 20);
            if (_anel3 != null) _anel3.localRotation = Quaternion.Euler(10, _giro * 0.4f, 80);
            Vector3 rot = new Vector3(Mathf.Sin(Tempo * 1.7f) * 6, 0, Mathf.Sin(Tempo * 1.3f) * 8);
            if (Preparando(out float tp)) rot.x = -10 * tp;
            if (Cambaleio > 0) rot += new Vector3(-20, 0, 15) * Cambaleio;
            if (!E.Vivo)
            {
                _morte += dt;
                _queda += dt * dt * 6 + dt * 1.5f;
                rot += new Vector3(_morte * 300, _morte * 200, 0);
            }
            _nucleo.localRotation = Quaternion.Euler(rot);
            _nucleo.localPosition = Raiz.InverseTransformVector(Empurrao);
            if (_feixe != null)
            {
                // O feixe varre o chao na frente; quando mira, fica fixo e forte.
                bool mira = E.Acao == EstadoAcao.Conjurando;
                float varre = mira ? 0 : Mathf.Sin(Tempo * 1.4f + E.Id) * 25;
                _feixe.localRotation = Quaternion.Euler(mira ? 12 : 35, varre, 0);
                _feixe.localScale = new Vector3(1, 1, mira ? 9 : 2.6f);
                _feixe.gameObject.SetActive(E.Vivo);
            }
        }
    }

    /// <summary>
    /// Maquina bipede da ARCA: pernas de pistao dobradas para tras, tronco
    /// blindado, visor. Engrenado tem martelo no braco; o vigia, canhao;
    /// o Ceifador gira laminas; o Executor carrega misseis nos ombros.
    /// </summary>
    public sealed class Mech : VisualDeEntidade
    {
        public string Variante = "";
        public Color CorCorpo = new Color(0.55f, 0.48f, 0.38f);
        public Color CorOlho = new Color(1f, 0.6f, 0.1f);

        private Transform _quadril, _tronco, _bracoE, _bracoD, _coxaE, _coxaD, _canelaE, _canelaD, _laminaE, _laminaD;
        private Vector3 _ultimaPos;
        private float _fase, _vel, _morte, _giroLamina;

        protected override void Construir()
        {
            Material mat = E != null && E.Chefe ? Visual.Paleta.ToonChefe : Visual.Paleta.Toon;
            Color escuro = Util.Escurecer(CorCorpo, 0.55f);
            _quadril = Util.Filho(Raiz, "quadril", new Vector3(0, 1.1f, 0)).transform;
            _tronco = Util.Filho(_quadril, "tronco", new Vector3(0, 0.1f, 0)).transform;
            var c = new ConstrutorDeMalha();
            c.Cor(CorCorpo).Caixa(new Vector3(0, 0.45f, 0), new Vector3(0.8f, 0.7f, 0.6f), 0.08f);
            c.Cor(escuro).Caixa(new Vector3(0, 0.12f, 0), new Vector3(0.5f, 0.2f, 0.45f), 0.04f);
            c.Cor(Util.Escurecer(CorCorpo, 0.8f)).Caixa(new Vector3(0, 0.88f, 0.12f), new Vector3(0.38f, 0.26f, 0.36f), 0.05f);
            c.Cor(CorOlho, 0f).Caixa(new Vector3(0, 0.9f, 0.31f), new Vector3(0.3f, 0.06f, 0.02f), 0.01f);
            c.Cor(escuro);
            for (int i = 0; i < 3; i++) c.Caixa(new Vector3(0, 0.3f + i * 0.17f, 0.305f), new Vector3(0.6f, 0.03f, 0.02f));
            c.Tubo(new Vector3(-0.25f, 0.6f, -0.3f), new Vector3(-0.25f, 1.05f, -0.35f), 0.06f, 0.05f, 6);
            c.Tubo(new Vector3(0.25f, 0.6f, -0.3f), new Vector3(0.25f, 0.95f, -0.35f), 0.06f, 0.05f, 6);
            if (Variante == "executor")
            {
                c.Cor(CorCorpo);
                for (int l = -1; l <= 1; l += 2)
                {
                    c.Caixa(new Vector3(l * 0.62f, 0.85f, -0.05f), new Vector3(0.36f, 0.3f, 0.5f), 0.04f);
                    c.Cor(new Color(1f, 0.3f, 0.1f), 0.2f);
                    for (int k = 0; k < 4; k++) c.Esfera(new Vector3(l * 0.62f + (k % 2 - 0.5f) * 0.14f, 0.85f + (k / 2 - 0.5f) * 0.12f, 0.2f), Vector3.one * 0.045f, 3, 4);
                    c.Cor(CorCorpo);
                }
            }
            Util.ComMalha(_tronco.gameObject, c.Gerar("mech_tronco" + Variante), mat);

            _bracoE = Braco(-1, mat, escuro);
            _bracoD = Braco(1, mat, escuro);

            Mesh coxa = Perna(true, escuro), canela = Perna(false, escuro);
            _coxaE = Util.Filho(_quadril, "coxaE", new Vector3(-0.28f, 0, 0)).transform;
            _coxaD = Util.Filho(_quadril, "coxaD", new Vector3(0.28f, 0, 0)).transform;
            Util.ComMalha(Util.Filho(_coxaE, "m"), coxa, mat);
            Util.ComMalha(Util.Filho(_coxaD, "m"), coxa, mat);
            _canelaE = Util.Filho(_coxaE, "canelaE", new Vector3(0, -0.55f, 0.12f)).transform;
            _canelaD = Util.Filho(_coxaD, "canelaD", new Vector3(0, -0.55f, 0.12f)).transform;
            Util.ComMalha(Util.Filho(_canelaE, "m"), canela, mat);
            Util.ComMalha(Util.Filho(_canelaD, "m"), canela, mat);
            AlturaDaCabeca = 2.4f;
            _ultimaPos = transform.position;
        }

        private Transform Braco(int lado, Material mat, Color escuro)
        {
            Transform ombro = Util.Filho(_tronco, lado < 0 ? "bracoE" : "bracoD", new Vector3(lado * 0.52f, 0.68f, 0)).transform;
            var c = new ConstrutorDeMalha();
            c.Cor(escuro).Esfera(Vector3.zero, Vector3.one * 0.16f, 6, 8);
            c.Cor(CorCorpo).Caixa(new Vector3(lado * 0.05f, -0.32f, 0), new Vector3(0.2f, 0.5f, 0.22f), 0.04f);
            bool martelo = Variante == "" && lado > 0;
            bool canhao = Variante == "atirador" || (Variante == "executor" && lado < 0);
            if (martelo)
            {
                c.Cor(escuro).Tubo(new Vector3(lado * 0.05f, -0.55f, 0), new Vector3(lado * 0.05f, -0.85f, 0.1f), 0.06f, 0.06f, 6);
                c.Cor(Util.Escurecer(CorCorpo, 0.75f)).Caixa(new Vector3(lado * 0.05f, -0.95f, 0.12f), new Vector3(0.36f, 0.26f, 0.3f), 0.05f);
            }
            else if (canhao)
            {
                c.Cor(escuro).Tubo(new Vector3(lado * 0.05f, -0.55f, 0), new Vector3(lado * 0.05f, -0.6f, 0.55f), 0.09f, 0.07f, 8);
                c.Cor(CorOlho, 0f).Esfera(new Vector3(lado * 0.05f, -0.6f, 0.56f), Vector3.one * 0.05f, 3, 5);
            }
            else
            {
                c.Cor(escuro).Caixa(new Vector3(lado * 0.05f, -0.65f, 0.05f), new Vector3(0.16f, 0.22f, 0.2f), 0.03f);
            }
            Util.ComMalha(Util.Filho(ombro, "m"), c.Gerar("mech_braco" + Variante + lado), mat);
            if (Variante == "ceifador" || (Variante == "executor" && lado > 0))
            {
                Transform lamina = Util.Filho(ombro, "lamina", new Vector3(lado * 0.05f, -0.68f, 0.15f)).transform;
                var l = new ConstrutorDeMalha();
                l.Cor(new Color(0.75f, 0.76f, 0.78f));
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2 / 3;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    l.Caixa(d * 0.45f, new Vector3(0.9f, 0.03f, 0.14f), 0.01f);
                }
                l.Cor(escuro).Cilindro(new Vector3(0, -0.04f, 0), 0.14f, 0.08f, 10);
                Mesh ml = l.Gerar("lamina");
                GameObject gl = Util.Filho(lamina, "m");
                gl.transform.localRotation = Quaternion.identity;
                Util.ComMalha(gl, ml, mat);
                if (lado < 0) _laminaE = lamina;
                else _laminaD = lamina;
            }
            return ombro;
        }

        private Mesh Perna(bool coxa, Color escuro)
        {
            var c = new ConstrutorDeMalha();
            if (coxa)
            {
                c.Cor(escuro).Esfera(Vector3.zero, Vector3.one * 0.15f, 6, 8);
                c.Cor(CorCorpo).Caixa(new Vector3(0, -0.28f, 0.06f), new Vector3(0.2f, 0.5f, 0.24f), 0.04f);
            }
            else
            {
                c.Cor(escuro).Tubo(Vector3.zero, new Vector3(0, -0.5f, -0.2f), 0.07f, 0.06f, 6);
                c.Cor(new Color(0.75f, 0.75f, 0.72f)).Tubo(new Vector3(0, -0.1f, -0.04f), new Vector3(0, -0.4f, -0.16f), 0.04f, 0.04f, 6);
                c.Cor(CorCorpo).Caixa(new Vector3(0, -0.56f, -0.1f), new Vector3(0.26f, 0.1f, 0.44f), 0.03f);
            }
            return c.Gerar("mech_perna" + coxa);
        }

        protected override void Animar(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 mov = pos - _ultimaPos;
            mov.y = 0;
            _ultimaPos = pos;
            if (dt > 0 && mov.magnitude < 3) _vel = Util.Suavizar(_vel, mov.magnitude / dt, 10, dt);
            _fase += mov.magnitude / (1.6f * Escala) * Mathf.PI;
            float anda = Mathf.Clamp01(_vel / 1.5f);
            float s = Mathf.Sin(_fase);
            _coxaE.localRotation = Quaternion.Euler(-s * 25 * anda - 10, 0, 0);
            _coxaD.localRotation = Quaternion.Euler(s * 25 * anda - 10, 0, 0);
            _canelaE.localRotation = Quaternion.Euler(20 + Mathf.Max(0, Mathf.Cos(_fase)) * 30 * anda, 0, 0);
            _canelaD.localRotation = Quaternion.Euler(20 + Mathf.Max(0, -Mathf.Cos(_fase)) * 30 * anda, 0, 0);
            float bob = Mathf.Abs(Mathf.Cos(_fase)) * 0.06f * anda;
            _quadril.localPosition = new Vector3(0, 1.1f - bob, 0) + Raiz.InverseTransformVector(Empurrao);

            Vector3 tronco = new Vector3(4 * anda, s * 4 * anda, 0);
            Vector3 bE = new Vector3(s * 15 * anda, 0, -8), bD = new Vector3(-s * 15 * anda, 0, 8);
            string anim = AnimacaoAtual;
            float giro = 0;
            if (Preparando(out float tp))
            {
                if (anim == "golpe_pesado")
                {
                    bD = new Vector3(-150 * tp, 0, 10);
                    tronco.x = -10 * tp;
                }
                else if (anim == "giro")
                {
                    bE = new Vector3(-80, 0, -80 * tp);
                    bD = new Vector3(-80, 0, 80 * tp);
                    giro = -30 * tp;
                }
                else if (anim == "disparo" || anim == "laser")
                {
                    bE = new Vector3(-85 * tp, 0, 0);
                    bD = new Vector3(-85 * tp, 0, 0);
                }
                else if (anim == "investida")
                {
                    tronco.x = 20 * tp;
                    _quadril.localPosition -= new Vector3(0, 0.15f * tp, 0);
                }
                else
                {
                    tronco.x = -12 * tp;
                    bE.z = -40 * tp;
                    bD.z = 40 * tp;
                }
            }
            else if (Recuperando(out float tr))
            {
                float sr = 1 - Mathf.Pow(1 - Mathf.Clamp01(tr / 0.25f), 3);
                float volta = Mathf.SmoothStep(0, 1, (tr - 0.5f) / 0.5f);
                if (anim == "golpe_pesado")
                {
                    bD = Vector3.Lerp(new Vector3(Mathf.Lerp(-150, -40, sr), 0, 10), bD, volta);
                    tronco.x = Mathf.Lerp(-10, 20, sr) * (1 - volta);
                }
                else if (anim == "giro")
                {
                    bE = new Vector3(-80, 0, -80);
                    bD = new Vector3(-80, 0, 80);
                    giro = Mathf.Lerp(-30, 360, sr);
                }
                else if (anim == "disparo" || anim == "laser")
                {
                    bE = Vector3.Lerp(new Vector3(-85, 0, 0), bE, volta);
                    bD = Vector3.Lerp(new Vector3(-85, 0, 0), bD, volta);
                    tronco.x = -6 * (1 - sr);
                }
            }
            if (E.Acao == EstadoAcao.Investindo) tronco.x = 25;
            if (Cambaleio > 0) tronco.x -= 10 * Cambaleio;
            if (!E.Vivo)
            {
                _morte = Mathf.Min(1, _morte + Time.deltaTime * 1.6f);
                tronco.x = Mathf.Lerp(tronco.x, 70, _morte);
                _quadril.localPosition = Vector3.Lerp(_quadril.localPosition, new Vector3(0, 0.5f, 0), _morte);
            }
            _tronco.localRotation = Quaternion.Euler(tronco.x, tronco.y + giro, 0);
            _bracoE.localRotation = Quaternion.Euler(bE);
            _bracoD.localRotation = Quaternion.Euler(bD);
            _giroLamina += Time.deltaTime * (E.Acao == EstadoAcao.Conjurando || E.Acao == EstadoAcao.Recuperando ? 1400 : 220);
            if (_laminaE != null) _laminaE.localRotation = Quaternion.Euler(0, _giroLamina, 0);
            if (_laminaD != null) _laminaD.localRotation = Quaternion.Euler(0, -_giroLamina, 0);
        }
    }

    /// <summary>Boneco de treino: saco de areia num poste, balanca quando apanha.</summary>
    public sealed class Boneco : VisualDeEntidade
    {
        private Transform _saco;
        private float _balanco, _velBalanco;
        private float _morte;

        protected override void Construir()
        {
            Material mat = Visual.Paleta.Toon;
            Transform poste = Util.Filho(Raiz, "poste").transform;
            var c = new ConstrutorDeMalha();
            c.Cor(new Color(0.42f, 0.3f, 0.18f)).Cilindro(Vector3.zero, 0.06f, 1.0f, 8);
            c.Cor(new Color(0.35f, 0.27f, 0.18f)).Caixa(new Vector3(0, 0.03f, 0), new Vector3(0.6f, 0.06f, 0.12f), 0.01f);
            c.Caixa(new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.06f, 0.6f), 0.01f);
            Util.ComMalha(poste.gameObject, c.Gerar("boneco_poste"), mat);
            _saco = Util.Filho(Raiz, "saco", new Vector3(0, 0.95f, 0)).transform;
            c.Limpar();
            Color pano = new Color(0.72f, 0.64f, 0.48f);
            c.Cor(pano).Capsula(new Vector3(0, 0.05f, 0), new Vector3(0, 0.6f, 0), 0.24f, 8);
            c.Cor(new Color(0.42f, 0.3f, 0.18f)).Tubo(new Vector3(-0.45f, 0.5f, 0), new Vector3(0.45f, 0.5f, 0), 0.04f, 0.04f, 6);
            c.Cor(pano).Esfera(new Vector3(0, 0.95f, 0), Vector3.one * 0.18f, 6, 10);
            c.Cor(new Color(0.15f, 0.1f, 0.08f));
            c.Caixa(new Vector3(-0.06f, 0.99f, 0.165f), new Vector3(0.04f, 0.04f, 0.01f));
            c.Caixa(new Vector3(0.06f, 0.99f, 0.165f), new Vector3(0.04f, 0.04f, 0.01f));
            c.Caixa(new Vector3(0, 0.9f, 0.17f), new Vector3(0.1f, 0.015f, 0.01f));
            c.Cor(new Color(0.6f, 0.15f, 0.1f)).Caixa(new Vector3(0, 0.3f, 0.23f), new Vector3(0.18f, 0.18f, 0.02f));
            Util.ComMalha(_saco.gameObject, c.Gerar("boneco_saco"), mat);
            AlturaDaCabeca = 2.1f;
        }

        public override void Golpeado(float dano, bool critico, Vector3 deOnde, string elemento)
        {
            base.Golpeado(dano, critico, deOnde, elemento);
            _velBalanco += (critico ? 260 : 160);
        }

        protected override void Animar(float dt)
        {
            _velBalanco += -_balanco * 60 * dt - _velBalanco * 4 * dt;
            _balanco += _velBalanco * dt;
            if (!E.Vivo) _morte = Mathf.Min(1, _morte + dt * 2);
            _saco.localRotation = Quaternion.Euler(-_balanco * 0.25f - 80 * _morte, 0, 0);
        }
    }
}
