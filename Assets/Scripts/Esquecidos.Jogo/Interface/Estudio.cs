using System;
using System.Collections.Generic;
using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Visual;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Regras;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>
    /// Um canto escondido do mundo (bem abaixo do chao) com camera propria.
    /// Fotografa o modelo 3D de cada item para virar icone, e filma o rosto de
    /// quem esta falando para o retrato do dialogo. A luz e sempre a mesma,
    /// qualquer que seja a hora do dia la em cima.
    /// </summary>
    public sealed class Estudio : MonoBehaviour
    {
        public const int Camada = 30;
        private static readonly Vector3 Centro = new Vector3(0, -1000, 0);
        private Camera _cam;
        private RenderTexture _rtIcone;
        private readonly Dictionary<string, Sprite> _icones = new Dictionary<string, Sprite>();
        private GameObject _retrato;
        private RenderTexture _rtRetrato;
        private Transform _cabecaDoRetrato;

        public static Estudio Criar(Transform pai)
        {
            var g = new GameObject("Estudio");
            g.transform.SetParent(pai, false);
            var e = g.AddComponent<Estudio>();
            e.Montar();
            return e;
        }

        private void Montar()
        {
            var gc = new GameObject("camera");
            gc.transform.SetParent(transform, false);
            _cam = gc.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0, 0, 0, 0);
            _cam.cullingMask = 1 << Camada;
            _cam.fieldOfView = 22;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 20;
            UniversalAdditionalCameraData d = _cam.GetUniversalAdditionalCameraData();
            d.renderPostProcessing = false;
            d.renderShadows = false;
            _rtIcone = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            _rtRetrato = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        }

        public RenderTexture TexturaDoRetrato => _rtRetrato;

        private static void Camadas(GameObject g)
        {
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Camada;
        }

        /// <summary>Renderiza com uma luz de estudio fixa: guarda e devolve o sol e o ambiente.</summary>
        private void Fotografar(RenderTexture alvo)
        {
            Light sol = RenderSettings.sun;
            Quaternion rot = default;
            Color cor = default;
            float intensidade = 0;
            if (sol != null)
            {
                rot = sol.transform.rotation;
                cor = sol.color;
                intensidade = sol.intensity;
                sol.transform.rotation = Quaternion.Euler(40, -35, 0);
                sol.color = new Color(1f, 0.95f, 0.88f);
                sol.intensity = 1.15f;
            }
            Vector4 ceu = Shader.GetGlobalVector("_Esq_AmbienteCeu");
            Vector4 chao = Shader.GetGlobalVector("_Esq_AmbienteChao");
            Vector4 sombra = Shader.GetGlobalVector("_Esq_CorSombra");
            Vector4 jogador = Shader.GetGlobalVector("_Esq_PosJogador");
            // Como cor (em sRGB, igual ao Ambiente): como vetor iria cru para o shader e estouraria o branco.
            Shader.SetGlobalColor("_Esq_AmbienteCeu", new Color(0.42f, 0.45f, 0.52f));
            Shader.SetGlobalColor("_Esq_AmbienteChao", new Color(0.2f, 0.19f, 0.18f));
            Shader.SetGlobalColor("_Esq_CorSombra", new Color(0.25f, 0.3f, 0.45f));
            Shader.SetGlobalVector("_Esq_PosJogador", Vector4.zero);
            bool neblina = RenderSettings.fog;
            RenderSettings.fog = false;
            _cam.targetTexture = alvo;
            _cam.Render();
            RenderSettings.fog = neblina;
            Shader.SetGlobalVector("_Esq_AmbienteCeu", ceu);
            Shader.SetGlobalVector("_Esq_AmbienteChao", chao);
            Shader.SetGlobalVector("_Esq_CorSombra", sombra);
            Shader.SetGlobalVector("_Esq_PosJogador", jogador);
            if (sol != null)
            {
                sol.transform.rotation = rot;
                sol.color = cor;
                sol.intensity = intensidade;
            }
        }

        // ------------------------------------------------------------------ icones

        public Sprite Icone(ItemBase b)
        {
            string chave = b.Id;
            if (_icones.TryGetValue(chave, out Sprite s)) return s;
            Mesh m = MalhaDoIcone(b);
            var g = new GameObject("modelo");
            g.transform.SetParent(transform, false);
            g.transform.position = Centro;
            Util.ComMalha(g, m, Paleta.Toon, false);
            Camadas(g);
            Bounds bb = m.bounds;
            float raio = Mathf.Max(0.05f, bb.extents.magnitude);
            // Visto de cima e de lado, como item na mao.
            Quaternion vista = Quaternion.Euler(28, -40, 0);
            Vector3 alvo = Centro + bb.center;
            float dist = raio / Mathf.Sin(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            _cam.transform.SetPositionAndRotation(alvo - vista * Vector3.forward * dist, vista);
            Fotografar(_rtIcone);
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            RenderTexture anterior = RenderTexture.active;
            RenderTexture.active = _rtIcone;
            tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            tex.Apply();
            RenderTexture.active = anterior;
            DestroyImmediate(g);
            s = Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            _icones[chave] = s;
            return s;
        }

        /// <summary>O modelo de cada tipo de item, do jeito que ele apareceria na mao ou no chao.</summary>
        private static Mesh MalhaDoIcone(ItemBase b)
        {
            Color cor = Util.Cor(b.Cor, Color.gray);
            var c = new ConstrutorDeMalha();
            switch (b.Tipo)
            {
                case TipoItem.Arma:
                    c.Juntar(ModelosDeArma.Arma(b.Modelo, cor), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-35, 90, 0), Vector3.one), true);
                    break;
                case TipoItem.Secundaria:
                    c.Juntar(ModelosDeArma.Escudo(b.Modelo, cor), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-70, 0, 0), Vector3.one), true);
                    break;
                case TipoItem.Cabeca:
                    c.Cor(new Color(0.75f, 0.6f, 0.5f)).Esfera(Vector3.zero, new Vector3(0.1f, 0.12f, 0.11f), 8, 10);
                    c.Cor(cor);
                    if (b.Modelo == "bandana") c.Tubo(new Vector3(0, 0.03f, 0), new Vector3(0, 0.07f, 0), 0.112f, 0.108f, 12);
                    else if (b.Modelo == "mascara") c.Esfera(new Vector3(0, -0.03f, 0.06f), new Vector3(0.09f, 0.08f, 0.06f), 6, 8);
                    else if (b.Modelo == "coroa")
                        for (int i = 0; i < 7; i++)
                        {
                            float a = i / 7f * Mathf.PI * 2;
                            c.Tubo(new Vector3(Mathf.Cos(a) * 0.1f, 0.07f, Mathf.Sin(a) * 0.1f), new Vector3(Mathf.Cos(a) * 0.13f, 0.18f, Mathf.Sin(a) * 0.13f), 0.022f, 0, 5);
                        }
                    else c.Esfera(new Vector3(0, 0.03f, 0), new Vector3(0.13f, 0.12f, 0.135f), 8, 10);
                    break;
                case TipoItem.Corpo:
                    c.Cor(cor).Caixa(new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.26f, 0.24f), 0.08f);
                    c.Caixa(new Vector3(0, -0.13f, 0), new Vector3(0.32f, 0.22f, 0.2f), 0.06f);
                    c.Esfera(new Vector3(-0.21f, 0.2f, 0), Vector3.one * 0.075f, 5, 7);
                    c.Esfera(new Vector3(0.21f, 0.2f, 0), Vector3.one * 0.075f, 5, 7);
                    if (b.Modelo == "peitoral")
                    {
                        c.Cor(Util.Escurecer(cor, 0.6f));
                        for (int i = 0; i < 3; i++) c.Caixa(new Vector3(0, -0.05f + i * 0.09f, 0.125f), new Vector3(0.34f, 0.015f, 0.01f));
                    }
                    break;
                case TipoItem.Maos:
                    c.Cor(cor).Esfera(new Vector3(-0.06f, 0, 0), new Vector3(0.05f, 0.07f, 0.045f), 5, 7);
                    c.Esfera(new Vector3(0.06f, 0, 0), new Vector3(0.05f, 0.07f, 0.045f), 5, 7);
                    c.Capsula(new Vector3(-0.06f, 0.05f, 0), new Vector3(-0.06f, 0.14f, 0), 0.045f, 6);
                    c.Capsula(new Vector3(0.06f, 0.05f, 0), new Vector3(0.06f, 0.14f, 0), 0.045f, 6);
                    break;
                case TipoItem.Pes:
                    c.Cor(cor).Caixa(new Vector3(-0.06f, 0, 0.04f), new Vector3(0.09f, 0.07f, 0.22f), 0.025f);
                    c.Caixa(new Vector3(0.06f, 0, 0.04f), new Vector3(0.09f, 0.07f, 0.22f), 0.025f);
                    c.Tubo(new Vector3(-0.06f, 0, 0), new Vector3(-0.06f, 0.14f, 0), 0.05f, 0.05f, 8);
                    c.Tubo(new Vector3(0.06f, 0, 0), new Vector3(0.06f, 0.14f, 0), 0.05f, 0.05f, 8);
                    break;
                case TipoItem.Pingente:
                    c.Cor(new Color(0.35f, 0.28f, 0.2f)).Toro(new Vector3(0, 0.08f, 0), 0.09f, 0.006f, 16, 4);
                    c.Cor(cor, b.Unico ? 0.3f : 1f).Esfera(new Vector3(0, -0.02f, 0), new Vector3(0.045f, 0.06f, 0.025f), 5, 7);
                    break;
                case TipoItem.Bugiganga:
                    if (b.Modelo == "anel") c.Cor(cor).Toro(Vector3.zero, 0.06f, 0.015f, 16, 6);
                    else if (b.Modelo == "relogio" || b.Modelo == "bussola")
                    {
                        c.Cor(cor).Cilindro(Vector3.zero, 0.07f, 0.025f, 14);
                        c.Cor(new Color(0.9f, 0.88f, 0.8f)).Cilindro(new Vector3(0, 0.025f, 0), 0.06f, 0.003f, 14);
                        c.Cor(new Color(0.15f, 0.1f, 0.08f)).Caixa(new Vector3(0.015f, 0.03f, 0), new Vector3(0.035f, 0.004f, 0.005f));
                    }
                    else c.Cor(cor, b.Unico ? 0.3f : 1f).Caixa(Vector3.zero, new Vector3(0.12f, 0.02f, 0.08f), 0.005f);
                    break;
                case TipoItem.Consumivel:
                    switch (b.Modelo)
                    {
                        case "bandagem":
                            c.Cor(cor).Tubo(new Vector3(-0.05f, 0, 0), new Vector3(0.05f, 0, 0), 0.05f, 0.05f, 12);
                            break;
                        case "pao":
                            c.Cor(cor).Esfera(Vector3.zero, new Vector3(0.09f, 0.05f, 0.06f), 6, 8);
                            break;
                        case "seringa":
                            c.Cor(new Color(0.85f, 0.87f, 0.9f)).Tubo(new Vector3(0, -0.06f, 0), new Vector3(0, 0.06f, 0), 0.02f, 0.02f, 8);
                            c.Cor(cor, 0.4f).Tubo(new Vector3(0, -0.04f, 0), new Vector3(0, 0.03f, 0), 0.016f, 0.016f, 8);
                            c.Cor(Color.gray).Tubo(new Vector3(0, 0.06f, 0), new Vector3(0, 0.11f, 0), 0.003f, 0.001f, 4);
                            break;
                        case "sinalizador":
                            c.Cor(cor).Tubo(new Vector3(0, -0.07f, 0), new Vector3(0, 0.07f, 0), 0.02f, 0.02f, 8);
                            c.Cor(new Color(1f, 0.85f, 0.4f), 0.2f).Esfera(new Vector3(0, 0.08f, 0), Vector3.one * 0.02f, 3, 4);
                            break;
                        case "papel":
                            c.Cor(cor).Caixa(Vector3.zero, new Vector3(0.12f, 0.004f, 0.16f));
                            c.Cor(new Color(0.3f, 0.25f, 0.2f)).Caixa(new Vector3(0, 0.003f, 0), new Vector3(0.08f, 0.002f, 0.01f));
                            break;
                        default:
                            c.Cor(new Color(0.75f, 0.85f, 0.85f, 1)).Tubo(new Vector3(0, -0.05f, 0), new Vector3(0, 0.04f, 0), 0.04f, 0.035f, 10);
                            c.Cor(cor, 0.4f).Tubo(new Vector3(0, -0.045f, 0), new Vector3(0, 0.02f, 0), 0.036f, 0.033f, 10);
                            c.Cor(new Color(0.5f, 0.35f, 0.2f)).Tubo(new Vector3(0, 0.04f, 0), new Vector3(0, 0.07f, 0), 0.015f, 0.015f, 6);
                            break;
                    }
                    break;
                default:
                    switch (b.Modelo)
                    {
                        case "engrenagem":
                            var contorno = new List<Vector2>();
                            for (int i = 0; i < 40; i++)
                            {
                                float a = i / 40f * Mathf.PI * 2;
                                float r = i % 4 == 1 || i % 4 == 2 ? 0.08f : 0.065f;
                                contorno.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                            }
                            c.Cor(cor).Extrusao(contorno, 0.02f);
                            break;
                        case "nucleo":
                            c.Cor(cor, 0.3f).Esfera(Vector3.zero, Vector3.one * 0.07f, 6, 8);
                            c.Cor(new Color(0.3f, 0.3f, 0.32f)).Toro(Vector3.zero, 0.07f, 0.01f, 14, 4);
                            break;
                        case "fragmento":
                            c.Cor(new Color(0.55f, 0.53f, 0.5f)).Caixa(Vector3.zero, new Vector3(0.16f, 0.03f, 0.16f), 0.01f);
                            c.Cor(cor, 0.3f).Toro(new Vector3(0, 0.017f, 0), 0.045f, 0.006f, 14, 3);
                            break;
                        case "cristal":
                            c.Cor(cor, 0.3f).Tubo(Vector3.zero, new Vector3(0.02f, 0.14f, 0), 0.04f, 0, 5, true, true);
                            break;
                        case "fio":
                            c.Cor(cor).Toro(Vector3.zero, 0.05f, 0.012f, 14, 4);
                            c.Toro(new Vector3(0, 0.02f, 0), 0.045f, 0.012f, 14, 4);
                            break;
                        default:
                            c.Cor(cor).Caixa(Vector3.zero, new Vector3(0.12f, 0.06f, 0.1f), 0.015f);
                            break;
                    }
                    break;
            }
            return c.Gerar("icone:" + b.Id);
        }

        // ------------------------------------------------------------------ retrato

        /// <summary>Monta (ou troca) o boneco do retrato. null tira o retrato.</summary>
        public void Retrato(DadosDeHumano dados)
        {
            if (_retrato != null) Destroy(_retrato);
            _retrato = null;
            _cabecaDoRetrato = null;
            if (dados == null) return;
            _retrato = new GameObject("retrato");
            _retrato.transform.SetParent(transform, false);
            _retrato.transform.position = Centro + new Vector3(10, 0, 0);
            var h = _retrato.AddComponent<RetratoAnimado>();
            h.Montar(dados);
            Camadas(_retrato);
            _cabecaDoRetrato = h.Cabeca;
        }

        public bool TemRetrato => _retrato != null;

        private void LateUpdate()
        {
            if (_retrato == null || _cabecaDoRetrato == null) return;
            Vector3 alvo = _cabecaDoRetrato.position + new Vector3(0, 0.12f, 0);
            Quaternion vista = Quaternion.Euler(4, 180 + 18, 0);
            _cam.transform.SetPositionAndRotation(alvo - vista * Vector3.forward * 1.25f, vista);
            Fotografar(_rtRetrato);
        }
    }

    /// <summary>Boneco do retrato: so cabeca e ombros, respirando, olhando um pouco em volta.</summary>
    public sealed class RetratoAnimado : MonoBehaviour
    {
        public Transform Cabeca { get; private set; }
        private Transform _tronco;
        private float _t;

        public void Montar(DadosDeHumano d)
        {
            PecasDoHumano p = ModeloHumano.Pecas(d);
            Material mat = Paleta.Toon;
            _tronco = Util.Filho(transform, "tronco", new Vector3(0, 0.99f, 0)).transform;
            Util.ComMalha(Util.Filho(_tronco, "m"), p.Tronco, mat, false);
            Cabeca = Util.Filho(_tronco, "cabeca", new Vector3(0, 0.58f, 0.01f)).transform;
            Util.ComMalha(Util.Filho(Cabeca, "m"), p.Cabeca, mat, false);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            _tronco.localRotation = Quaternion.Euler(Mathf.Sin(_t * 1.6f) * 1.2f, 0, 0);
            Cabeca.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.9f) * 3, Mathf.Sin(_t * 0.5f) * 9, Mathf.Sin(_t * 0.7f) * 2);
        }
    }

    /// <summary>Icone de poder: circulo da cor da arvore com um simbolo.</summary>
    public static class IconesDePoder
    {
        private static readonly Dictionary<string, string> _simbolos = new Dictionary<string, string>
        {
            ["ataque"] = "⚔", ["soco"] = "✊", ["rolamento"] = "⟳",
            ["corte_duplo"] = "⚔", ["disparada"] = "➤", ["faca_arremessada"] = "✦", ["passo_fantasma"] = "≋",
            ["ferida_aberta"] = "✂", ["olho_de_corredor"] = "◉", ["tempestade_de_laminas"] = "✵", ["salto_do_corredor"] = "⤴",
            ["golpe_pesado"] = "⚒", ["grito_de_guerra"] = "‼", ["investida_de_ombro"] = "⇛", ["pele_de_ferro"] = "⛨",
            ["giro_brutal"] = "↻", ["furia_cega"] = "♨", ["salto_esmagador"] = "⇓", ["terremoto"] = "≈",
            ["frasco_acido"] = "⚗", ["bandagem_rapida"] = "✚", ["bomba_de_pregos"] = "✹", ["armadilha_de_mola"] = "⌘",
            ["drone_remendado"] = "◎", ["dardo_sonifero"] = "➹", ["chuva_de_pedras"] = "⁘", ["sinalizador"] = "✺",
        };

        public static string Simbolo(string poder) => _simbolos.TryGetValue(poder, out string s) ? s : "•";

        public static Color CorDaArvore(string arvore)
        {
            switch (arvore)
            {
                case "corredor": return new Color(0.3f, 0.55f, 0.32f);
                case "guardiao": return new Color(0.7f, 0.42f, 0.18f);
                case "remendeiro": return new Color(0.28f, 0.42f, 0.7f);
                default: return new Color(0.42f, 0.42f, 0.44f);
            }
        }
    }
}
