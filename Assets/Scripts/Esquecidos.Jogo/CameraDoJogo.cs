using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Camera de tres quartos que segue o jogador, como a do Pal3: gira em
    /// passos com Q e E, aproxima com a roda, treme com impacto e, nas cenas,
    /// pode focar outro ator. Tambem converte o mouse em ponto no chao.
    /// </summary>
    public sealed class CameraDoJogo : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Transform Alvo;
        public float Inclinacao = 52f;
        public float DistanciaMin = 9f, DistanciaMax = 26f;

        private float _yaw, _yawAlvo;
        private float _dist = 16f, _distAlvo = 16f;
        private Vector3 _foco;
        private float _tremor;
        private Vector3? _focoDaCena;
        private float _velocidadeDaCena = 2f;
        private bool _primeiro = true;

        public static CameraDoJogo Criar(Transform pai)
        {
            var g = new GameObject("Camera");
            g.transform.SetParent(pai, false);
            var cam = g.AddComponent<Camera>();
            cam.fieldOfView = 38;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 320;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.tag = "MainCamera";
            UniversalAdditionalCameraData dados = cam.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = true;
            dados.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dados.antialiasingQuality = AntialiasingQuality.High;
            g.AddComponent<AudioListener>();
            var c = g.AddComponent<CameraDoJogo>();
            c.Cam = cam;
            return c;
        }

        public float Yaw => _yaw;

        /// <summary>Frente da camera no plano do chao: o "W" do jogador.</summary>
        public Vector3 Frente => Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
        public Vector3 Direita => Quaternion.Euler(0, _yaw, 0) * Vector3.right;

        public void Girar(int sentido) => _yawAlvo += 45 * sentido;

        public void GirarLivre(float graus) => _yawAlvo += graus;

        public void Aproximar(float roda) => _distAlvo = Mathf.Clamp(_distAlvo - roda * 1.6f, DistanciaMin, DistanciaMax);

        public const float DistanciaDeJogo = 18f;

        public void DefinirDistancia(float d) => _distAlvo = Mathf.Clamp(d, DistanciaMin, DistanciaMax);

        public void Tremer(float forca) => _tremor = Mathf.Min(1.2f, Mathf.Max(_tremor, forca));

        public void FocarCena(Vector3? ponto, float velocidade = 2f)
        {
            _focoDaCena = ponto;
            _velocidadeDaCena = velocidade;
        }

        public void Teleportar()
        {
            _primeiro = true;
        }

        public void DefinirYaw(float graus)
        {
            _yaw = _yawAlvo = graus;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 alvo = _focoDaCena ?? (Alvo != null ? Alvo.position : _foco);
            alvo += Vector3.up * 1.0f;
            if (_primeiro)
            {
                _foco = alvo;
                _primeiro = false;
            }
            float vel = _focoDaCena.HasValue ? _velocidadeDaCena : 9f;
            _foco = Util.Suavizar(_foco, alvo, vel, dt);
            _yaw = Mathf.LerpAngle(_yaw, _yawAlvo, 1 - Mathf.Exp(-10 * dt));
            _dist = Util.Suavizar(_dist, _distAlvo, 8, dt);

            Quaternion rot = Quaternion.Euler(Inclinacao, _yaw, 0);
            Vector3 pos = _foco - rot * Vector3.forward * _dist;
            if (_tremor > 0.001f)
            {
                float t = Time.unscaledTime * 38;
                Vector3 sacode = new Vector3(Mathf.PerlinNoise(t, 0) - 0.5f, Mathf.PerlinNoise(0, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f);
                pos += sacode * _tremor * 0.6f;
                _tremor = Mathf.Max(0, _tremor - dt * 2.8f);
            }
            transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Onde o raio do cursor encosta no chao (y = altura).</summary>
        public bool PontoNoChao(Vector2 tela, out Vector3 ponto, float altura = 0)
        {
            Ray r = Cam.ScreenPointToRay(tela);
            var plano = new Plane(Vector3.up, new Vector3(0, altura, 0));
            if (plano.Raycast(r, out float d))
            {
                ponto = r.GetPoint(d);
                return true;
            }
            ponto = default;
            return false;
        }
    }
}
