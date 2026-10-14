using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo.Atores
{
    /// <summary>
    /// Humano animado por codigo. O esqueleto e uma arvore de pivos; cada
    /// quadro a pose sai de duas camadas: locomocao (parado, andando,
    /// correndo) e acao (o golpe da vez, lido da fase da simulacao), misturadas
    /// pelo peso da acao. Sem clipe de animacao, sem arquivo.
    /// </summary>
    public sealed class Humanoide : VisualDeEntidade
    {
        public DadosDeHumano Dados = new DadosDeHumano();

        private Transform _corpo, _quadril, _tronco, _cabeca, _ombroE, _ombroD, _cotoveloE, _cotoveloD, _maoD, _maoE, _coxaE, _coxaD, _joelhoE, _joelhoD;
        private MeshFilter _mfQuadril, _mfTronco, _mfCabeca, _mfBracoE, _mfBracoD, _mfAntE, _mfAntD, _mfCoxaE, _mfCoxaD, _mfCanE, _mfCanD;
        private MeshFilter _arma, _escudo;
        private float _fase;
        private Vector3 _ultimaPos;
        private float _velocidade;
        private float _pesoAcao;
        private float _morte;
        private float _contracao;
        private float _semente;
        private float _altura = 1f;

        private struct Pose
        {
            public Vector3 Tronco, Cabeca, OmbroE, OmbroD, CotoveloE, CotoveloD, CoxaE, CoxaD, JoelhoE, JoelhoD;
            public float Abaixa;
            public float Giro;
            public float Cambalhota;

            public static Pose Misturar(Pose a, Pose b, float t)
            {
                return new Pose
                {
                    Tronco = Vector3.Lerp(a.Tronco, b.Tronco, t),
                    Cabeca = Vector3.Lerp(a.Cabeca, b.Cabeca, t),
                    OmbroE = Vector3.Lerp(a.OmbroE, b.OmbroE, t),
                    OmbroD = Vector3.Lerp(a.OmbroD, b.OmbroD, t),
                    CotoveloE = Vector3.Lerp(a.CotoveloE, b.CotoveloE, t),
                    CotoveloD = Vector3.Lerp(a.CotoveloD, b.CotoveloD, t),
                    CoxaE = Vector3.Lerp(a.CoxaE, b.CoxaE, t),
                    CoxaD = Vector3.Lerp(a.CoxaD, b.CoxaD, t),
                    JoelhoE = Vector3.Lerp(a.JoelhoE, b.JoelhoE, t),
                    JoelhoD = Vector3.Lerp(a.JoelhoD, b.JoelhoD, t),
                    Abaixa = Mathf.Lerp(a.Abaixa, b.Abaixa, t),
                    Giro = Mathf.Lerp(a.Giro, b.Giro, t),
                    Cambalhota = Mathf.Lerp(a.Cambalhota, b.Cambalhota, t),
                };
            }
        }

        protected override void Construir()
        {
            _semente = (E?.Id ?? 0) * 0.37f;
            _altura = Dados.Ap.Altura;
            float w = Dados.Ap.Porte;
            _corpo = Util.Filho(Raiz, "corpo").transform;
            _quadril = Util.Filho(_corpo, "quadril", new Vector3(0, ModeloHumano.AlturaQuadril, 0)).transform;
            _tronco = Util.Filho(_quadril, "tronco", new Vector3(0, 0.06f, 0)).transform;
            _cabeca = Util.Filho(_tronco, "cabeca", new Vector3(0, 0.58f, 0.01f)).transform;
            _ombroE = Util.Filho(_tronco, "ombroE", new Vector3(-ModeloHumano.Ombro * w, 0.47f, 0)).transform;
            _ombroD = Util.Filho(_tronco, "ombroD", new Vector3(ModeloHumano.Ombro * w, 0.47f, 0)).transform;
            _cotoveloE = Util.Filho(_ombroE, "cotoveloE", new Vector3(0, -ModeloHumano.Braco, 0)).transform;
            _cotoveloD = Util.Filho(_ombroD, "cotoveloD", new Vector3(0, -ModeloHumano.Braco, 0)).transform;
            _maoD = Util.Filho(_cotoveloD, "maoD", new Vector3(0, -ModeloHumano.Antebraco - 0.045f, 0.01f)).transform;
            _maoE = Util.Filho(_cotoveloE, "maoE", new Vector3(0, -ModeloHumano.Antebraco * 0.55f, 0)).transform;
            _coxaE = Util.Filho(_quadril, "coxaE", new Vector3(-ModeloHumano.LarguraQuadril * w, -0.06f, 0)).transform;
            _coxaD = Util.Filho(_quadril, "coxaD", new Vector3(ModeloHumano.LarguraQuadril * w, -0.06f, 0)).transform;
            _joelhoE = Util.Filho(_coxaE, "joelhoE", new Vector3(0, -ModeloHumano.Coxa, 0)).transform;
            _joelhoD = Util.Filho(_coxaD, "joelhoD", new Vector3(0, -ModeloHumano.Coxa, 0)).transform;
            _corpo.localScale = new Vector3(1, _altura, 1);

            Material mat = MaterialDoCorpo();
            _mfQuadril = Malha(_quadril, mat);
            _mfTronco = Malha(_tronco, mat);
            _mfCabeca = Malha(_cabeca, mat);
            _mfBracoE = Malha(_ombroE, mat);
            _mfBracoD = Malha(_ombroD, mat);
            _mfAntE = Malha(_cotoveloE, mat);
            _mfAntD = Malha(_cotoveloD, mat);
            _mfCoxaE = Malha(_coxaE, mat);
            _mfCoxaD = Malha(_coxaD, mat);
            _mfCanE = Malha(_joelhoE, mat);
            _mfCanD = Malha(_joelhoD, mat);

            GameObject ga = Util.Filho(_maoD, "arma");
            _arma = ga.AddComponent<MeshFilter>();
            ga.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObject ge = Util.Filho(_maoE, "escudo");
            _escudo = ge.AddComponent<MeshFilter>();
            ge.AddComponent<MeshRenderer>().sharedMaterial = mat;
            AlturaDaCabeca = 1.95f * _altura;
            Aplicar();
            _ultimaPos = transform.position;
        }

        private Material MaterialDoCorpo() => E != null && E.Chefe ? Visual.Paleta.ToonChefe : Visual.Paleta.Toon;

        private static MeshFilter Malha(Transform t, Material mat)
        {
            GameObject g = Util.Filho(t, "malha");
            MeshFilter mf = g.AddComponent<MeshFilter>();
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return mf;
        }

        /// <summary>Troca as malhas pelo que os dados dizem agora (equipamento mudou).</summary>
        public void Aplicar()
        {
            PecasDoHumano p = ModeloHumano.Pecas(Dados);
            _mfQuadril.sharedMesh = p.Quadril;
            _mfTronco.sharedMesh = p.Tronco;
            _mfCabeca.sharedMesh = p.Cabeca;
            _mfBracoE.sharedMesh = p.BracoE;
            _mfBracoD.sharedMesh = p.BracoD;
            _mfAntE.sharedMesh = p.AntebracoE;
            _mfAntD.sharedMesh = p.AntebracoD;
            _mfCoxaE.sharedMesh = p.CoxaE;
            _mfCoxaD.sharedMesh = p.CoxaD;
            _mfCanE.sharedMesh = p.CanelaE;
            _mfCanD.sharedMesh = p.CanelaD;
            _arma.sharedMesh = string.IsNullOrEmpty(Dados.Arma) ? null : ModelosDeArma.Arma(Dados.Arma, Dados.CorArma);
            _arma.transform.localRotation = Dados.Arma == "estilingue" || Dados.Arma == "lanca_dardos" ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(-12, 0, 0);
            if (string.IsNullOrEmpty(Dados.Escudo))
            {
                _escudo.sharedMesh = null;
            }
            else
            {
                _escudo.sharedMesh = ModelosDeArma.Escudo(Dados.Escudo, Dados.CorEscudo);
                if (Dados.Escudo == "aljava")
                {
                    // Aljava vai nas costas, nao no braco.
                    _escudo.transform.SetParent(_tronco, false);
                    _escudo.transform.localPosition = new Vector3(0.1f, 0.15f, -0.17f);
                    _escudo.transform.localRotation = Quaternion.Euler(-15, 0, -20);
                }
                else
                {
                    _escudo.transform.SetParent(_maoE, false);
                    _escudo.transform.localPosition = new Vector3(-0.07f, 0, 0.02f);
                    _escudo.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }
        }

        // ------------------------------------------------------------------ animacao

        protected override void Animar(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 mov = pos - _ultimaPos;
            mov.y = 0;
            _ultimaPos = pos;
            float v = dt > 0 ? mov.magnitude / dt : 0;
            if (v > 20) v = 0;
            _velocidade = Util.Suavizar(_velocidade, v, 12, dt);
            float corrida = Mathf.Clamp01((_velocidade - 2.2f) / 2.5f);
            float passo = Mathf.Lerp(1.1f, 1.7f, corrida);
            _fase += mov.magnitude / passo * Mathf.PI * 2 * 0.5f;

            Pose loc = Locomocao(_velocidade, corrida);
            bool acao = E.Acao == EstadoAcao.Conjurando || E.Acao == EstadoAcao.Recuperando || E.Acao == EstadoAcao.Esquivando
                        || E.Acao == EstadoAcao.Investindo || E.Acao == EstadoAcao.Saltando;
            _pesoAcao = Util.Suavizar(_pesoAcao, acao ? 1 : 0, acao ? 30 : 9, dt);
            Pose final = loc;
            if (_pesoAcao > 0.01f) final = Pose.Misturar(loc, Acao(loc), _pesoAcao);

            if (E.Acao == EstadoAcao.Atordoado || E.TemEfeito("atordoado"))
            {
                float o = Tempo * 5;
                final.Cabeca += new Vector3(Mathf.Sin(o) * 12, Mathf.Cos(o) * 15, 0);
                final.Tronco += new Vector3(8, 0, Mathf.Sin(o * 0.7f) * 6);
                final.OmbroE = new Vector3(5, 0, -8);
                final.OmbroD = new Vector3(5, 0, 8);
            }
            if (Cambaleio > 0)
            {
                final.Tronco += new Vector3(-14, 0, 0) * Cambaleio;
                final.Cabeca += new Vector3(-12, 0, 0) * Cambaleio;
            }
            if (Dados.Tipo == "cinzento" || Dados.Tipo == "bruto")
            {
                // Corcunda, e os espasmos de quem nao manda mais no proprio corpo.
                final.Tronco += new Vector3(24, 0, 0);
                final.Cabeca += new Vector3(-22, 0, 0);
                _contracao = Mathf.Max(0, _contracao - dt * 3);
                if (Random.value < dt * 0.8f) _contracao = 1;
                final.Cabeca += new Vector3(0, Mathf.Sin(Tempo * 31 + _semente) * 25, Mathf.Sin(Tempo * 23) * 18) * _contracao;
                final.OmbroE += new Vector3(-25, 0, -5);
                final.OmbroD += new Vector3(-25, 0, 5);
            }

            if (!E.Vivo)
            {
                _morte = Mathf.Min(1, _morte + dt * 2.2f);
                float q = 1 - Mathf.Pow(1 - _morte, 3);
                final = Pose.Misturar(final, Caido(), q);
                _corpo.localRotation = Quaternion.Euler(-88 * q, 0, 0);
                _corpo.localPosition = new Vector3(0, 0.12f * q, -0.2f * q);
            }
            else
            {
                _morte = 0;
                float bob = (1 - Mathf.Cos(_fase * 2)) * 0.02f * Mathf.Clamp01(_velocidade);
                _corpo.localPosition = new Vector3(0, bob - final.Abaixa, 0) + Raiz.InverseTransformVector(Empurrao);
                Quaternion cambalhota = Quaternion.identity;
                if (final.Cambalhota != 0)
                {
                    // Gira em volta do centro do corpo, nao dos pes.
                    cambalhota = Quaternion.Euler(final.Cambalhota, 0, 0);
                    Vector3 centro = new Vector3(0, 0.55f, 0);
                    _corpo.localPosition += centro - cambalhota * centro;
                }
                _corpo.localRotation = Quaternion.Euler(0, final.Giro, 0) * cambalhota;
            }

            _tronco.localRotation = Quaternion.Euler(final.Tronco);
            _cabeca.localRotation = Quaternion.Euler(final.Cabeca);
            _ombroE.localRotation = Quaternion.Euler(final.OmbroE);
            _ombroD.localRotation = Quaternion.Euler(final.OmbroD);
            _cotoveloE.localRotation = Quaternion.Euler(final.CotoveloE);
            _cotoveloD.localRotation = Quaternion.Euler(final.CotoveloD);
            _coxaE.localRotation = Quaternion.Euler(final.CoxaE);
            _coxaD.localRotation = Quaternion.Euler(final.CoxaD);
            _joelhoE.localRotation = Quaternion.Euler(final.JoelhoE);
            _joelhoD.localRotation = Quaternion.Euler(final.JoelhoD);
        }

        private Pose Locomocao(float vel, float corrida)
        {
            float anda = Mathf.Clamp01(vel / 1.2f);
            float s = Mathf.Sin(_fase), c = Mathf.Cos(_fase);
            float amp = Mathf.Lerp(28, 44, corrida) * anda;
            float respira = Mathf.Sin(Tempo * 1.7f + _semente) * (1 - anda);
            var p = new Pose
            {
                Tronco = new Vector3(Mathf.Lerp(2, 13, corrida) * anda + respira * 1.5f, s * 6 * anda, 0),
                Cabeca = new Vector3(-Mathf.Lerp(2, 9, corrida) * anda + Mathf.Sin(Tempo * 0.4f + _semente) * 3 * (1 - anda), Mathf.Sin(Tempo * 0.3f + _semente * 3) * 10 * (1 - anda), 0),
                OmbroE = new Vector3(s * amp * 0.8f, 0, -6 - respira),
                OmbroD = new Vector3(-s * amp * 0.8f, 0, 6 + respira),
                CotoveloE = new Vector3(-Mathf.Lerp(12, 65, corrida) * anda - 8, 0, 0),
                CotoveloD = new Vector3(-Mathf.Lerp(12, 65, corrida) * anda - 8, 0, 0),
                CoxaE = new Vector3(-s * amp, 0, -1.5f),
                CoxaD = new Vector3(s * amp, 0, 1.5f),
                JoelhoE = new Vector3((8 + Mathf.Lerp(40, 75, corrida) * Mathf.Max(0, c)) * anda + 3, 0, 0),
                JoelhoD = new Vector3((8 + Mathf.Lerp(40, 75, corrida) * Mathf.Max(0, -c)) * anda + 3, 0, 0),
            };
            // Arma pesada na mao: o braco direito segura mais baixo e mais firme.
            if (Dados.Arma == "cano" || Dados.Arma == "marreta" || Dados.Arma == "viga" || Dados.Arma == "martelo" || Dados.Arma == "taco")
            {
                p.OmbroD = new Vector3(-25 - s * amp * 0.2f, 0, 12);
                p.CotoveloD = new Vector3(-40, 0, 0);
            }
            else if (Dados.Arma == "lanca" || Dados.Arma == "forcado" || Dados.Arma == "arpao")
            {
                p.OmbroD = new Vector3(-30, 0, 10);
                p.CotoveloD = new Vector3(-70, 0, 0);
                p.OmbroE = new Vector3(-35, 0, -10);
                p.CotoveloE = new Vector3(-60, 0, 0);
            }
            return p;
        }

        private static float Rapido(float t, float fim) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t / fim), 3);

        /// <summary>A pose da acao em curso, montada por cima da locomocao.</summary>
        private Pose Acao(Pose baseP)
        {
            Pose p = baseP;
            string anim = AnimacaoAtual;
            bool prep = Preparando(out float tp);
            bool rec = Recuperando(out float tr);

            if (E.Acao == EstadoAcao.Esquivando)
            {
                float t = Mathf.Clamp01(E.TempoAcao / Mathf.Max(0.01f, E.DuracaoAcao));
                p.Cambalhota = 360 * Mathf.SmoothStep(0, 1, t);
                p.Abaixa = Mathf.Sin(t * Mathf.PI) * 0.35f;
                p.CoxaE = p.CoxaD = new Vector3(-95, 0, 0);
                p.JoelhoE = p.JoelhoD = new Vector3(110, 0, 0);
                p.OmbroE = new Vector3(-80, 0, -20);
                p.OmbroD = new Vector3(-80, 0, 20);
                p.CotoveloE = p.CotoveloD = new Vector3(-90, 0, 0);
                p.Tronco = new Vector3(40, 0, 0);
                return p;
            }
            if (E.Acao == EstadoAcao.Investindo)
            {
                p.Tronco = new Vector3(32, 0, 0);
                p.OmbroE = new Vector3(45, 0, -15);
                p.OmbroD = new Vector3(-60, 0, 25);
                p.CotoveloD = new Vector3(-30, 0, 0);
                return p;
            }
            if (E.Acao == EstadoAcao.Saltando)
            {
                p.CoxaE = p.CoxaD = new Vector3(-60, 0, 0);
                p.JoelhoE = p.JoelhoD = new Vector3(80, 0, 0);
                p.OmbroE = new Vector3(-150, 0, -20);
                p.OmbroD = new Vector3(-150, 0, 20);
                p.Tronco = new Vector3(-10, 0, 0);
                return p;
            }

            switch (anim)
            {
                case "golpe":
                case "corte_duplo":
                {
                    if (prep)
                    {
                        p.OmbroD = new Vector3(-115 * tp, 0, 40 * tp);
                        p.CotoveloD = new Vector3(-70 * tp, 0, 0);
                        p.Tronco = new Vector3(5, 32 * tp, 0);
                        p.OmbroE = new Vector3(-20 * tp, 0, -25 * tp);
                    }
                    else if (rec)
                    {
                        bool duplo = anim == "corte_duplo";
                        float s = Rapido(tr, duplo ? 0.25f : 0.3f);
                        float volta = Mathf.SmoothStep(0, 1, (tr - 0.55f) / 0.45f);
                        Vector3 fimOmbro = new Vector3(-55, 0, -35);
                        p.OmbroD = Vector3.Lerp(Vector3.Lerp(new Vector3(-115, 0, 40), fimOmbro, s), baseP.OmbroD, volta);
                        p.CotoveloD = Vector3.Lerp(Vector3.Lerp(new Vector3(-70, 0, 0), new Vector3(-10, 0, 0), s), baseP.CotoveloD, volta);
                        p.Tronco = Vector3.Lerp(new Vector3(5, Mathf.Lerp(32, -28, s), 0), baseP.Tronco, volta);
                        if (duplo && tr > 0.3f && tr < 0.7f)
                        {
                            // Segundo corte, de volta, com as costas da lamina.
                            float s2 = Rapido(tr - 0.3f, 0.2f);
                            p.OmbroD = Vector3.Lerp(fimOmbro, new Vector3(-95, 0, 45), s2);
                            p.Tronco = new Vector3(5, Mathf.Lerp(-28, 25, s2), 0);
                        }
                    }
                    break;
                }
                case "golpe_pesado":
                {
                    if (prep)
                    {
                        p.OmbroD = new Vector3(-165 * tp, 0, 15);
                        p.OmbroE = new Vector3(-150 * tp, 0, -10);
                        p.CotoveloD = p.CotoveloE = new Vector3(-45 * tp, 0, 0);
                        p.Tronco = new Vector3(-12 * tp, 10 * tp, 0);
                    }
                    else if (rec)
                    {
                        float s = Rapido(tr, 0.28f);
                        float volta = Mathf.SmoothStep(0, 1, (tr - 0.55f) / 0.45f);
                        p.OmbroD = Vector3.Lerp(Vector3.Lerp(new Vector3(-165, 0, 15), new Vector3(-55, 0, 5), s), baseP.OmbroD, volta);
                        p.OmbroE = Vector3.Lerp(Vector3.Lerp(new Vector3(-150, 0, -10), new Vector3(-50, 0, -5), s), baseP.OmbroE, volta);
                        p.CotoveloD = p.CotoveloE = Vector3.Lerp(new Vector3(-10, 0, 0), baseP.CotoveloD, volta);
                        p.Tronco = Vector3.Lerp(new Vector3(Mathf.Lerp(-12, 28, s), 0, 0), baseP.Tronco, volta);
                        p.Abaixa = 0.09f * s * (1 - volta);
                        p.JoelhoE = p.JoelhoD = new Vector3(25 * s * (1 - volta), 0, 0);
                    }
                    break;
                }
                case "estocada":
                case "soco":
                {
                    if (prep)
                    {
                        p.OmbroD = new Vector3(-35 * tp, 0, 10);
                        p.CotoveloD = new Vector3(-105 * tp, 0, 0);
                        p.Tronco = new Vector3(0, 25 * tp, 0);
                    }
                    else if (rec)
                    {
                        float s = Rapido(tr, 0.22f);
                        float volta = Mathf.SmoothStep(0, 1, (tr - 0.45f) / 0.55f);
                        p.OmbroD = Vector3.Lerp(Vector3.Lerp(new Vector3(-35, 0, 10), new Vector3(-88, 0, 2), s), baseP.OmbroD, volta);
                        p.CotoveloD = Vector3.Lerp(Vector3.Lerp(new Vector3(-105, 0, 0), new Vector3(-4, 0, 0), s), baseP.CotoveloD, volta);
                        p.Tronco = Vector3.Lerp(new Vector3(12 * s, Mathf.Lerp(25, -15, s), 0), baseP.Tronco, volta);
                        p.CoxaE = Vector3.Lerp(new Vector3(-30 * s, 0, 0), baseP.CoxaE, volta);
                        p.JoelhoE = Vector3.Lerp(new Vector3(25 * s, 0, 0), baseP.JoelhoE, volta);
                    }
                    break;
                }
                case "giro":
                {
                    p.OmbroE = new Vector3(-80, 0, -75);
                    p.OmbroD = new Vector3(-80, 0, 75);
                    p.CotoveloE = p.CotoveloD = new Vector3(-10, 0, 0);
                    if (prep)
                    {
                        p.Giro = -40 * tp;
                        p.Abaixa = 0.08f * tp;
                    }
                    else if (rec)
                    {
                        p.Giro = Mathf.Lerp(-40, 360, Rapido(tr, 0.45f));
                        p.Abaixa = 0.08f * (1 - tr);
                    }
                    break;
                }
                case "disparo":
                {
                    float puxa = prep ? tp : rec ? 1 - Rapido(tr, 0.15f) : 0;
                    float mira = prep ? Mathf.Min(1, tp * 2) : rec ? 1 - Mathf.SmoothStep(0, 1, (tr - 0.4f) / 0.6f) : 0;
                    p.OmbroE = Vector3.Lerp(baseP.OmbroE, new Vector3(-88, -10, -5), mira);
                    p.CotoveloE = Vector3.Lerp(baseP.CotoveloE, new Vector3(-5, 0, 0), mira);
                    p.OmbroD = Vector3.Lerp(baseP.OmbroD, new Vector3(-80, 30, 20), mira);
                    p.CotoveloD = Vector3.Lerp(baseP.CotoveloD, new Vector3(-120 * puxa - 20, 0, 0), mira);
                    p.Tronco = Vector3.Lerp(baseP.Tronco, new Vector3(0, 30, 0), mira);
                    p.Cabeca = Vector3.Lerp(baseP.Cabeca, new Vector3(0, -25, 0), mira);
                    break;
                }
                case "arremesso":
                {
                    if (prep)
                    {
                        p.OmbroD = new Vector3(-165 * tp, 0, 30 * tp);
                        p.CotoveloD = new Vector3(-85 * tp, 0, 0);
                        p.Tronco = new Vector3(-10 * tp, 35 * tp, 0);
                        p.OmbroE = new Vector3(-60 * tp, 0, -15);
                    }
                    else if (rec)
                    {
                        float s = Rapido(tr, 0.25f);
                        float volta = Mathf.SmoothStep(0, 1, (tr - 0.45f) / 0.55f);
                        p.OmbroD = Vector3.Lerp(Vector3.Lerp(new Vector3(-165, 0, 30), new Vector3(-50, 0, -10), s), baseP.OmbroD, volta);
                        p.CotoveloD = Vector3.Lerp(new Vector3(-85 * (1 - s), 0, 0), baseP.CotoveloD, volta);
                        p.Tronco = Vector3.Lerp(new Vector3(Mathf.Lerp(-10, 18, s), Mathf.Lerp(35, -20, s), 0), baseP.Tronco, volta);
                    }
                    break;
                }
                case "conjurar":
                case "invocar":
                {
                    float t = prep ? tp : rec ? 1 - Mathf.SmoothStep(0, 1, tr) : 0;
                    float empurra = rec ? Rapido(tr, 0.2f) * (1 - tr) : 0;
                    p.OmbroE = new Vector3(-75 * t - 25 * empurra, 15 * t, -10);
                    p.OmbroD = new Vector3(-75 * t - 25 * empurra, -15 * t, 10);
                    p.CotoveloE = p.CotoveloD = new Vector3(-60 * t * (1 - empurra), 0, 0);
                    p.Tronco = new Vector3(-6 * t + 10 * empurra, 0, 0);
                    p.Cabeca = new Vector3(8 * t, 0, 0);
                    break;
                }
                case "grito":
                {
                    float t = prep ? tp : rec ? 1 - Mathf.SmoothStep(0, 1, tr) : 0;
                    p.OmbroE = new Vector3(-30 * t, 0, -75 * t);
                    p.OmbroD = new Vector3(-30 * t, 0, 75 * t);
                    p.CotoveloE = p.CotoveloD = new Vector3(-50 * t, 0, 0);
                    p.Tronco = new Vector3(-18 * t, 0, 0);
                    p.Cabeca = new Vector3(-25 * t, 0, 0);
                    break;
                }
                case "pisao":
                {
                    if (prep)
                    {
                        p.CoxaD = new Vector3(-75 * tp, 0, 0);
                        p.JoelhoD = new Vector3(95 * tp, 0, 0);
                        p.OmbroE = new Vector3(-30 * tp, 0, -50 * tp);
                        p.OmbroD = new Vector3(-30 * tp, 0, 50 * tp);
                        p.Tronco = new Vector3(-8 * tp, 0, 0);
                    }
                    else if (rec)
                    {
                        float volta = Mathf.SmoothStep(0, 1, tr);
                        p.Abaixa = 0.14f * (1 - volta);
                        p.JoelhoE = p.JoelhoD = new Vector3(35 * (1 - volta), 0, 0);
                        p.CoxaE = p.CoxaD = new Vector3(-15 * (1 - volta), 0, 0);
                        p.Tronco = new Vector3(18 * (1 - volta), 0, 0);
                    }
                    break;
                }
                case "investida":
                case "salto":
                {
                    // Preparacao do bote: agacha e arma o corpo.
                    float t = prep ? tp : rec ? 1 - tr : 0;
                    p.Abaixa = 0.16f * t;
                    p.JoelhoE = p.JoelhoD = new Vector3(55 * t, 0, 0);
                    p.CoxaE = p.CoxaD = new Vector3(-35 * t, 0, 0);
                    p.Tronco = new Vector3(25 * t, 0, 0);
                    p.OmbroE = new Vector3(35 * t, 0, -15);
                    p.OmbroD = new Vector3(35 * t, 0, 15);
                    break;
                }
            }
            return p;
        }

        private static Pose Caido()
        {
            return new Pose
            {
                OmbroE = new Vector3(-10, 0, -70),
                OmbroD = new Vector3(-30, 0, 80),
                CotoveloE = new Vector3(-20, 0, 0),
                CotoveloD = new Vector3(-50, 0, 0),
                CoxaE = new Vector3(-20, 0, -8),
                CoxaD = new Vector3(5, 0, 6),
                JoelhoE = new Vector3(35, 0, 0),
                JoelhoD = new Vector3(10, 0, 0),
                Cabeca = new Vector3(0, 25, 0),
            };
        }

        public Transform Cabeca => _cabeca;
        public Transform MaoDireita => _maoD;
    }
}
