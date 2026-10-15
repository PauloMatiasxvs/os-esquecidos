using Esquecidos.Jogo.Cena;
using Esquecidos.Jogo.Visual;
using Esquecidos.Motor.Texturas;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Progresso;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Luz, ceu, neblina e pos-processamento, tudo puxado pela hora do dia.
    /// O sol nasce laranja, fica branco, se poe vermelho; de noite a lua azul e
    /// a tocha na mao do jogador. O laboratorio tem luz fria e constante; o
    /// Nucleo, escuro e ciano.
    /// </summary>
    public sealed class Ambiente : MonoBehaviour
    {
        public Light Sol { get; private set; }
        public Light Tocha { get; private set; }
        private Volume _volume;
        private Bloom _bloom;
        private ColorAdjustments _cor;
        private Vignette _vinheta;
        private LiftGammaGain _lgg;
        private Material _ceu;
        private float _noite;
        private float _alerta;
        private float _dano;

        private static readonly int PosJogador = Shader.PropertyToID("_Esq_PosJogador");
        private static readonly int AmbCeu = Shader.PropertyToID("_Esq_AmbienteCeu");
        private static readonly int AmbChao = Shader.PropertyToID("_Esq_AmbienteChao");
        private static readonly int CorSombra = Shader.PropertyToID("_Esq_CorSombra");
        private static readonly int Noite = Shader.PropertyToID("_Esq_Noite");
        private static readonly int Vento = Shader.PropertyToID("_Esq_Vento");

        public static Ambiente Criar(Transform pai)
        {
            var g = new GameObject("Ambiente");
            g.transform.SetParent(pai, false);
            var a = g.AddComponent<Ambiente>();
            a.Montar();
            return a;
        }

        private void Montar()
        {
            Shader.SetGlobalTexture("_Esq_Ruido", TexturaDeRuido.Gerar());
            var gs = new GameObject("Sol");
            gs.transform.SetParent(transform, false);
            Sol = gs.AddComponent<Light>();
            Sol.type = LightType.Directional;
            Sol.shadows = LightShadows.Soft;
            Sol.shadowStrength = 0.85f;
            Sol.shadowBias = 0.06f;
            Sol.shadowNormalBias = 0.5f;
            RenderSettings.sun = Sol;

            var gt = new GameObject("Tocha");
            gt.transform.SetParent(transform, false);
            Tocha = gt.AddComponent<Light>();
            Tocha.type = LightType.Point;
            Tocha.color = new Color(1f, 0.62f, 0.32f);
            Tocha.range = 11;
            Tocha.intensity = 0;
            Tocha.shadows = LightShadows.Soft;
            Tocha.shadowStrength = 0.75f;

            _ceu = Paleta.Ceu;
            RenderSettings.skybox = _ceu;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            var gv = new GameObject("Volume");
            gv.transform.SetParent(transform, false);
            _volume = gv.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10;
            VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            Tonemapping tm = perfil.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            _bloom = perfil.Add<Bloom>(true);
            _bloom.threshold.Override(1.0f);
            _bloom.intensity.Override(0.7f);
            _bloom.scatter.Override(0.68f);
            _vinheta = perfil.Add<Vignette>(true);
            _vinheta.intensity.Override(0.3f);
            _vinheta.smoothness.Override(0.45f);
            _cor = perfil.Add<ColorAdjustments>(true);
            _cor.postExposure.Override(0.1f);
            _cor.contrast.Override(12f);
            _cor.saturation.Override(-4f);
            _lgg = perfil.Add<LiftGammaGain>(true);
            _volume.sharedProfile = perfil;
        }

        /// <summary>Pulso de tela: vermelho quando o jogador apanha, escuro quando o setor fica perigoso.</summary>
        public void PulsoDeDano(float forca) => _dano = Mathf.Max(_dano, forca);

        public void Alerta(float forca) => _alerta = Mathf.Max(_alerta, forca);

        private static Color Chave(float h, float[] horas, Color[] cores)
        {
            for (int i = 0; i < horas.Length - 1; i++)
            {
                if (h >= horas[i] && h <= horas[i + 1])
                    return Color.Lerp(cores[i], cores[i + 1], Mathf.InverseLerp(horas[i], horas[i + 1], h));
            }
            return cores[cores.Length - 1];
        }

        private static readonly float[] Horas = { 0, 4.8f, 6f, 8f, 12f, 16.5f, 18.5f, 19.6f, 20.4f, 24 };

        private static readonly Color[] LuzSol =
        {
            new Color(0.38f, 0.48f, 0.82f), new Color(0.38f, 0.48f, 0.82f), new Color(1f, 0.55f, 0.32f), new Color(1f, 0.85f, 0.68f), new Color(1f, 0.96f, 0.88f),
            new Color(1f, 0.9f, 0.75f), new Color(1f, 0.62f, 0.32f), new Color(0.95f, 0.38f, 0.22f), new Color(0.38f, 0.48f, 0.82f), new Color(0.38f, 0.48f, 0.82f),
        };

        private static readonly Color[] CeuZenite =
        {
            new Color(0.01f, 0.015f, 0.04f), new Color(0.02f, 0.03f, 0.07f), new Color(0.25f, 0.3f, 0.5f), new Color(0.3f, 0.48f, 0.75f), new Color(0.26f, 0.46f, 0.78f),
            new Color(0.28f, 0.45f, 0.72f), new Color(0.25f, 0.28f, 0.5f), new Color(0.12f, 0.1f, 0.22f), new Color(0.02f, 0.025f, 0.06f), new Color(0.01f, 0.015f, 0.04f),
        };

        private static readonly Color[] CeuHorizonte =
        {
            new Color(0.03f, 0.04f, 0.08f), new Color(0.05f, 0.06f, 0.1f), new Color(0.95f, 0.55f, 0.35f), new Color(0.78f, 0.78f, 0.78f), new Color(0.72f, 0.78f, 0.84f),
            new Color(0.78f, 0.76f, 0.7f), new Color(0.98f, 0.55f, 0.3f), new Color(0.6f, 0.25f, 0.2f), new Color(0.06f, 0.06f, 0.1f), new Color(0.03f, 0.04f, 0.08f),
        };

        /// <summary>Atualiza tudo pela hora e pelo tema do mapa atual.</summary>
        public void Atualizar(CicloDoDia ciclo, Mapa mapa, Vector3 jogador, bool corteDeMuro, Cenario cena)
        {
            float h = ciclo.Hora;
            Tema tema = mapa?.Tema ?? Tema.Patio;
            float noiteAlvo = ciclo.Noite ? 1 : (h > 18.5f ? Mathf.InverseLerp(18.5f, 20f, h) * 0.7f : h < 6.5f ? Mathf.InverseLerp(6.5f, 5f, h) : 0);
            if (tema == Tema.Laboratorio) noiteAlvo = 0;
            if (tema == Tema.Nucleo) noiteAlvo = 0.75f;
            _noite = Mathf.MoveTowards(_noite, noiteAlvo, Time.deltaTime * 0.5f);

            // Sol: arco de leste a oeste; de noite vira lua do lado oposto.
            float dia = Mathf.InverseLerp(6f, 20f, h);
            float elevacao, azimute;
            if (!ciclo.Noite)
            {
                elevacao = Mathf.Lerp(8, 62, Mathf.Sin(dia * Mathf.PI));
                azimute = Mathf.Lerp(-70, 250, dia);
            }
            else
            {
                float n = (h >= 20 ? h - 20 : h + 4) / 10f;
                elevacao = Mathf.Lerp(25, 55, Mathf.Sin(n * Mathf.PI));
                azimute = Mathf.Lerp(110, 250, n);
            }
            Color corSol = Chave(h, Horas, LuzSol);
            float intensidade = ciclo.Noite ? 0.32f : Mathf.Lerp(0.55f, 1.55f, Mathf.Sin(dia * Mathf.PI));
            Color ambCeu = Color.Lerp(new Color(0.5f, 0.56f, 0.66f), new Color(0.06f, 0.08f, 0.15f), _noite);
            Color ambChao = Color.Lerp(new Color(0.27f, 0.25f, 0.22f), new Color(0.025f, 0.025f, 0.035f), _noite);
            Color sombra = Color.Lerp(new Color(0.22f, 0.3f, 0.48f), new Color(0.04f, 0.06f, 0.15f), _noite);
            Color neblina = Color.Lerp(Chave(h, Horas, CeuHorizonte) * 0.85f, new Color(0.02f, 0.025f, 0.05f), _noite * 0.7f);
            float nebInicio = Mathf.Lerp(34, 16, _noite), nebFim = Mathf.Lerp(150, 62, _noite);

            switch (tema)
            {
                case Tema.Laboratorio:
                    corSol = new Color(0.82f, 0.9f, 1f);
                    intensidade = 0.75f;
                    elevacao = 64;
                    azimute = 30;
                    ambCeu = new Color(0.36f, 0.4f, 0.47f);
                    ambChao = new Color(0.2f, 0.21f, 0.24f);
                    sombra = new Color(0.25f, 0.3f, 0.42f);
                    neblina = new Color(0.08f, 0.09f, 0.11f);
                    nebInicio = 30;
                    nebFim = 110;
                    break;
                case Tema.Nucleo:
                    corSol = new Color(0.35f, 0.75f, 0.85f);
                    intensidade = 0.45f;
                    elevacao = 65;
                    azimute = 80;
                    ambCeu = new Color(0.06f, 0.14f, 0.18f);
                    ambChao = new Color(0.02f, 0.04f, 0.05f);
                    sombra = new Color(0.02f, 0.08f, 0.12f);
                    neblina = new Color(0.02f, 0.05f, 0.07f);
                    nebInicio = 18;
                    nebFim = 70;
                    break;
                case Tema.Ferrugem:
                    ambChao *= new Color(1.15f, 0.95f, 0.8f);
                    neblina = Color.Lerp(neblina, new Color(0.45f, 0.32f, 0.22f), 0.25f * (1 - _noite));
                    break;
                case Tema.Fosso:
                    ambCeu *= 0.85f;
                    neblina = Color.Lerp(neblina, new Color(0.25f, 0.22f, 0.3f), 0.3f);
                    nebFim *= 0.8f;
                    break;
            }

            Sol.transform.rotation = Quaternion.Euler(elevacao, azimute, 0);
            Sol.color = corSol;
            Sol.intensity = intensidade;
            RenderSettings.ambientSkyColor = ambCeu;
            RenderSettings.ambientEquatorColor = Color.Lerp(ambCeu, ambChao, 0.5f);
            RenderSettings.ambientGroundColor = ambChao;
            RenderSettings.fogColor = neblina;
            RenderSettings.fogStartDistance = nebInicio;
            RenderSettings.fogEndDistance = nebFim;
            Shader.SetGlobalColor(AmbCeu, ambCeu);
            Shader.SetGlobalColor(AmbChao, ambChao);
            Shader.SetGlobalColor(CorSombra, sombra);
            Shader.SetGlobalFloat(Noite, _noite);
            Shader.SetGlobalFloat(Vento, tema == Tema.Patio ? 1f : 0.6f);
            Shader.SetGlobalVector(PosJogador, new Vector4(jogador.x, jogador.y, jogador.z, corteDeMuro ? 1 : 0));

            _ceu.SetColor("_CorZenite", Chave(h, Horas, CeuZenite));
            _ceu.SetColor("_CorHorizonte", Chave(h, Horas, CeuHorizonte));
            _ceu.SetColor("_CorSol", corSol);
            _ceu.SetVector("_DirSol", -Sol.transform.forward);
            _ceu.SetFloat("_Noite", _noite);

            // Tocha: acende ao entardecer, mais forte no labirinto.
            float tocha = tema == Tema.Laboratorio ? 0 : Mathf.Max(_noite, tema == Tema.Nucleo ? 0.8f : 0) * (mapa != null && mapa.Seguro ? 0.7f : 1f);
            Tocha.intensity = Mathf.Lerp(0, 3.2f, tocha) * (0.92f + Mathf.PerlinNoise(Time.time * 4, 1) * 0.16f);
            Tocha.transform.position = jogador + new Vector3(0.35f, 2.1f, 0.2f);
            Tocha.enabled = Tocha.intensity > 0.05f;

            if (cena != null)
                foreach (Light l in cena.LuzesDaNoite)
                    if (l != null) l.enabled = _noite > 0.25f;

            _dano = Mathf.Max(0, _dano - Time.deltaTime * 2.5f);
            _alerta = Mathf.Max(0, _alerta - Time.deltaTime * 0.5f);
            _vinheta.intensity.Override(0.3f + _noite * 0.12f + _dano * 0.25f + _alerta * 0.1f);
            _vinheta.color.Override(Color.Lerp(Color.black, new Color(0.55f, 0.02f, 0.02f), _dano));
            _cor.saturation.Override(Mathf.Lerp(-2f, -22f, _noite));
            _cor.postExposure.Override(Mathf.Lerp(0.15f, 0.35f, _noite));
            _lgg.lift.Override(new Vector4(Mathf.Lerp(0f, -0.02f, _noite), Mathf.Lerp(0f, 0f, _noite), Mathf.Lerp(0f, 0.04f, _noite), 0));
            _bloom.intensity.Override(Mathf.Lerp(0.6f, 1.0f, _noite));
        }
    }
}
