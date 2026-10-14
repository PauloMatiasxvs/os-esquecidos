using Esquecidos.Jogo.Visual;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo.Cena
{
    /// <summary>
    /// A parte visivel de um objeto de cenario. Le o estado do objeto a cada
    /// quadro e anima ate ele: portao desliza, muro sobe, tampa do bau abre,
    /// alavanca vira. Quem muda o estado e a simulacao.
    /// </summary>
    public sealed class VisualDeObjeto : MonoBehaviour
    {
        public ObjetoDeCena O { get; private set; }
        private Transform _movel, _movel2;
        private float _aberto;
        private float _alturaMuro;
        private Light _luz;
        private Renderer _brilho;
        private float _tempo;
        private Vector3 _eixo;
        private float _curso;
        private ParticleSystem _poeira;
        private bool _ultimoEstado;
        private Tema _tema;

        public static VisualDeObjeto Criar(ObjetoDeCena o, Tema tema, Transform pai)
        {
            if (o.Modelo == "nenhum" || o.Tipo == TipoObjeto.Gatilho) return null;
            var g = new GameObject(o.Nome ?? o.Tipo.ToString());
            g.transform.SetParent(pai, false);
            var v = g.AddComponent<VisualDeObjeto>();
            v.O = o;
            v._tema = tema;
            v.Construir();
            v._aberto = o.Ligado ? 1 : 0;
            v._ultimoEstado = o.Ligado;
            return v;
        }

        private void Construir()
        {
            Vector3 pos = Util.Mundo(O.Pos);
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, -O.Direcao * Mathf.Rad2Deg, 0);
            var c = new ConstrutorDeMalha();
            Material mat = Paleta.Toon;
            switch (O.Tipo)
            {
                case TipoObjeto.Porta: Porta(c); return;
                case TipoObjeto.MuroMovel: MuroMovel(); return;
                case TipoObjeto.Alavanca: Alavanca(c, mat); return;
                case TipoObjeto.Bau:
                case TipoObjeto.Guarda: Bau(c, mat); return;
                case TipoObjeto.Terminal: Terminal(c, mat); return;
                case TipoObjeto.Marco: Marco(c, mat); return;
                case TipoObjeto.Painel: MesaDosMapas(c, mat); return;
                case TipoObjeto.Descanso: Rede(c, mat); return;
                case TipoObjeto.Cartaz: MuroDosNomes(c, mat); return;
                case TipoObjeto.Coletavel: Brilho(); return;
                case TipoObjeto.Saida: Seta(); return;
                case TipoObjeto.Elevador: return;
            }
        }

        // ------------------------------------------------------------------ portas

        private void Porta(ConstrutorDeMalha c)
        {
            // Trabalha no espaco do mundo: o retangulo de bloqueio diz o vao.
            transform.rotation = Quaternion.identity;
            Retangulo r = O.Bloqueio;
            bool emX = r.Largura >= r.Profundidade;
            float vao = emX ? r.Largura : r.Profundidade;
            float esp = Mathf.Max(0.5f, emX ? r.Profundidade : r.Largura);
            float altura = O.Altura > 0 ? O.Altura : 6f;
            _eixo = emX ? Vector3.right : Vector3.forward;
            Vector3 tamanho = emX ? new Vector3(vao / 2, altura, esp) : new Vector3(esp, altura, vao / 2);
            bool grade = O.Modelo == "grade";
            bool patio = O.Modelo == "portao_patio";
            Color metal = _tema == Tema.Laboratorio ? new Color(0.78f, 0.8f, 0.83f) : new Color(0.34f, 0.32f, 0.3f);

            if (grade)
            {
                // Grade levadica: barras que sobem para dentro do muro.
                _eixo = Vector3.up;
                _curso = altura * 0.92f;
                _movel = Util.Filho(transform, "grade").transform;
                c.Cor(metal);
                int barras = Mathf.Max(4, (int)(vao / 0.45f));
                for (int i = 0; i <= barras; i++)
                {
                    float t = -vao / 2 + vao * i / barras;
                    Vector3 b = emX ? new Vector3(t, 0, 0) : new Vector3(0, 0, t);
                    c.Tubo(b, b + Vector3.up * altura * 0.95f, 0.06f, 0.06f, 6);
                    c.Tubo(b + Vector3.up * altura * 0.95f, b + Vector3.down * 0.25f, 0.06f, 0.0f, 6);
                }
                for (int k = 1; k < 4; k++)
                {
                    float y = altura * 0.95f * k / 4;
                    c.Caixa(new Vector3(0, y, 0), emX ? new Vector3(vao, 0.12f, 0.12f) : new Vector3(0.12f, 0.12f, vao), 0.02f);
                }
                Util.ComMalha(_movel.gameObject, c.Gerar("grade"), Paleta.Toon);
                return;
            }

            // Duas folhas que deslizam para dentro do muro, uma para cada lado.
            _curso = vao / 2 + 0.3f;
            for (int lado = -1; lado <= 1; lado += 2)
            {
                c.Limpar();
                Vector3 centro = _eixo * (lado * vao / 4);
                Color folha = patio ? new Color(0.36f, 0.35f, 0.33f) : metal;
                c.Cor(folha).Caixa(centro + Vector3.up * altura / 2, tamanho, 0.05f);
                c.Cor(Util.Escurecer(folha, 0.65f));
                int nervuras = patio ? 8 : 4;
                Vector3 normal = emX ? Vector3.forward : Vector3.right;
                for (int i = 0; i < nervuras; i++)
                {
                    float y = altura * (i + 0.5f) / nervuras;
                    Vector3 t = emX ? new Vector3(vao / 2 * 0.95f, 0.18f, esp + 0.15f) : new Vector3(esp + 0.15f, 0.18f, vao / 2 * 0.95f);
                    c.Caixa(centro + Vector3.up * y, t, 0.02f);
                }
                // Faixa de aviso na borda que se encontra.
                c.Cor(new Color(0.85f, 0.65f, 0.12f));
                Vector3 borda = centro - _eixo * (lado * (vao / 4 - 0.15f));
                c.Caixa(borda + Vector3.up * altura / 2, emX ? new Vector3(0.25f, altura, esp + 0.2f) : new Vector3(esp + 0.2f, altura, 0.25f), 0.01f);
                c.Cor(new Color(1f, 0.5f, 0.1f), 0f).Esfera(borda + Vector3.up * (altura * 0.8f) + normal * (esp / 2 + 0.12f), Vector3.one * 0.12f, 3, 5);
                Transform t2 = Util.Filho(transform, lado < 0 ? "folhaA" : "folhaB").transform;
                Util.ComMalha(t2.gameObject, c.Gerar("folha" + lado), Paleta.Toon);
                if (lado < 0) _movel = t2;
                else _movel2 = t2;
            }
            _poeira = Efeitos.Particulas.Poeira(transform, new Vector3(0, 0.3f, 0), emX ? new Vector3(vao, 0.2f, esp) : new Vector3(esp, 0.2f, vao));
        }

        private void MuroMovel()
        {
            transform.rotation = Quaternion.identity;
            Retangulo r = O.Bloqueio;
            float h = O.Altura > 0 ? O.Altura : 14;
            _alturaMuro = h;
            _movel = Util.Filho(transform, "muro").transform;
            var c = new ConstrutorDeMalha();
            c.Cor(Color.white, Mathf.Clamp01(h / 30f)).Caixa(new Vector3(0, h / 2, 0), new Vector3(r.Largura, h, r.Profundidade), 0.05f);
            string estilo = Paleta.EstiloDoMuro(_tema == Tema.Ferrugem || _tema == Tema.Engrenagens ? "metal" : _tema == Tema.Laboratorio ? "laboratorio" : "hera", _tema);
            MeshRenderer mr = Util.ComMalha(_movel.gameObject, c.Gerar("muro_movel"), Paleta.Muro(estilo));
            mr.receiveShadows = true;
            _poeira = Efeitos.Particulas.Poeira(transform, new Vector3(0, 0.2f, 0), new Vector3(r.Largura + 1, 0.3f, r.Profundidade + 1));
            _movel.localPosition = new Vector3(0, O.Ligado ? 0 : -h - 0.2f, 0);
        }

        // ------------------------------------------------------------------ objetos pequenos

        private void Alavanca(ConstrutorDeMalha c, Material mat)
        {
            Color metal = new Color(0.36f, 0.37f, 0.39f);
            c.Cor(new Color(0.42f, 0.41f, 0.39f)).Caixa(new Vector3(0, 0.6f, 0), new Vector3(0.4f, 1.2f, 0.7f), 0.04f);
            c.Cor(metal).Caixa(new Vector3(0.21f, 0.85f, 0), new Vector3(0.04f, 0.5f, 0.3f), 0.01f);
            Util.ComMalha(gameObject, c.Gerar("alavanca_base"), mat);
            c.Limpar();
            _movel = Util.Filho(transform, "braco", new Vector3(0.25f, 0.85f, 0)).transform;
            c.Cor(metal).Tubo(Vector3.zero, new Vector3(0.05f, 0.55f, 0), 0.035f, 0.03f, 6);
            c.Cor(new Color(0.75f, 0.15f, 0.1f)).Esfera(new Vector3(0.05f, 0.6f, 0), Vector3.one * 0.08f, 5, 7);
            Util.ComMalha(_movel.gameObject, c.Gerar("alavanca_braco"), mat);
            c.Limpar();
            GameObject l = Util.Filho(transform, "lampada", new Vector3(0.21f, 1.15f, 0));
            c.Cor(Color.white, 0f).Esfera(Vector3.zero, Vector3.one * 0.06f, 3, 5);
            _brilho = Util.ComMalha(l, c.Gerar("lampada"), Paleta.Toon, false);
            _luz = Luz(new Vector3(0.6f, 1.2f, 0), new Color(1f, 0.25f, 0.1f), 2.5f, 2f);
        }

        private void Bau(ConstrutorDeMalha c, Material mat)
        {
            bool raro = O.Modelo == "bau_raro";
            bool grande = O.Modelo == "bau_grande";
            float w = grande ? 1.6f : 1.1f, d = grande ? 0.9f : 0.75f, h = 0.6f;
            Color cor = raro ? new Color(0.32f, 0.36f, 0.42f) : grande ? new Color(0.45f, 0.32f, 0.2f) : new Color(0.35f, 0.38f, 0.28f);
            c.Cor(cor).Caixa(new Vector3(0, h / 2, 0), new Vector3(d, h, w), 0.03f);
            c.Cor(Util.Escurecer(cor, 0.6f));
            c.Caixa(new Vector3(0, h / 2, -w / 2 + 0.06f), new Vector3(d + 0.02f, h + 0.02f, 0.06f));
            c.Caixa(new Vector3(0, h / 2, w / 2 - 0.06f), new Vector3(d + 0.02f, h + 0.02f, 0.06f));
            if (!grande)
            {
                c.Cor(new Color(0.9f, 0.85f, 0.75f)).Caixa(new Vector3(d / 2 + 0.005f, h * 0.6f, 0), new Vector3(0.01f, 0.18f, 0.35f));
                c.Cor(new Color(0.7f, 0.15f, 0.1f)).Caixa(new Vector3(d / 2 + 0.01f, h * 0.6f, 0), new Vector3(0.01f, 0.06f, 0.2f));
            }
            Util.ComMalha(gameObject, c.Gerar("bau_corpo" + O.Modelo), mat);
            c.Limpar();
            _movel = Util.Filho(transform, "tampa", new Vector3(-d / 2, h, 0)).transform;
            c.Cor(Util.Escurecer(cor, 1.1f)).Caixa(new Vector3(d / 2, 0.07f, 0), new Vector3(d + 0.04f, 0.14f, w + 0.04f), 0.03f);
            Util.ComMalha(_movel.gameObject, c.Gerar("bau_tampa" + O.Modelo), mat);
            if (raro && !O.Usado) _luz = Luz(new Vector3(0, 1f, 0), new Color(0.4f, 0.65f, 1f), 1.5f, 3f);
        }

        private void Terminal(ConstrutorDeMalha c, Material mat)
        {
            c.Cor(new Color(0.22f, 0.23f, 0.25f)).Caixa(new Vector3(0, 0.55f, 0), new Vector3(0.5f, 1.1f, 0.7f), 0.04f);
            c.Cor(new Color(0.3f, 0.31f, 0.33f)).Caixa(new Vector3(0.1f, 1.25f, 0), new Vector3(0.35f, 0.55f, 0.8f), 0.04f);
            c.Cor(new Color(0.85f, 0.85f, 0.88f)).Caixa(new Vector3(0, 1.0f, 0), new Vector3(0.52f, 0.05f, 0.72f));
            Util.ComMalha(gameObject, c.Gerar("terminal"), mat);
            c.Limpar();
            GameObject tela = Util.Filho(transform, "tela", new Vector3(0.28f, 1.27f, 0));
            c.Cor(new Color(0.3f, 1f, 0.6f), 0f).Caixa(Vector3.zero, new Vector3(0.01f, 0.4f, 0.62f));
            _brilho = Util.ComMalha(tela, c.Gerar("tela"), Paleta.Toon, false);
            _luz = Luz(new Vector3(0.9f, 1.3f, 0), new Color(0.3f, 1f, 0.6f), 1.2f, 3.5f);
        }

        private void Marco(ConstrutorDeMalha c, Material mat)
        {
            Color concreto = _tema == Tema.Laboratorio ? new Color(0.25f, 0.27f, 0.3f) : new Color(0.5f, 0.49f, 0.46f);
            c.Cor(Util.Escurecer(concreto, 0.8f)).Caixa(new Vector3(0, 0.15f, 0), new Vector3(2f, 0.3f, 2f), 0.05f);
            c.Cor(concreto).Tubo(new Vector3(0, 0.3f, 0), new Vector3(0, 4.6f, 0), 0.7f, 0.45f, 4, true, true);
            c.Cor(new Color(0.35f, 0.34f, 0.32f)).Caixa(new Vector3(0, 4.75f, 0), new Vector3(0.9f, 0.3f, 0.9f), 0.04f);
            // Simbolo gravado nas quatro faces.
            Color simbolo = _tema == Tema.Laboratorio ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.8f, 0.4f);
            c.Cor(simbolo, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + Mathf.PI / 4;
                Vector3 n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                c.Toro(n * 0.47f + Vector3.up * 3.2f, 0.18f, 0.02f, 12, 3);
            }
            Util.ComMalha(gameObject, c.Gerar("marco" + _tema), mat);
            _luz = Luz(new Vector3(0, 3.5f, 0), simbolo, 1.5f, 6f);
        }

        private void MesaDosMapas(ConstrutorDeMalha c, Material mat)
        {
            Color m = new Color(0.45f, 0.32f, 0.2f);
            c.Cor(m).Caixa(new Vector3(0, 0.85f, 0), new Vector3(1.3f, 0.08f, 2.3f), 0.02f);
            foreach (float x in new[] { -0.55f, 0.55f })
            foreach (float z in new[] { -1f, 1f })
                c.Caixa(new Vector3(x, 0.42f, z), new Vector3(0.08f, 0.85f, 0.08f));
            c.Cor(new Color(0.88f, 0.82f, 0.66f)).Caixa(new Vector3(0, 0.9f, 0), new Vector3(1.15f, 0.01f, 2.1f));
            c.Cor(new Color(0.3f, 0.25f, 0.2f));
            var rng = new System.Random(3);
            for (int i = 0; i < 18; i++)
                c.Caixa(new Vector3((float)rng.NextDouble() - 0.5f, 0.912f, (float)rng.NextDouble() * 1.8f - 0.9f), rng.NextDouble() < 0.5 ? new Vector3(0.25f, 0.003f, 0.02f) : new Vector3(0.02f, 0.003f, 0.3f));
            c.Cor(new Color(0.7f, 0.15f, 0.1f)).Esfera(new Vector3(0.2f, 0.93f, 0.4f), Vector3.one * 0.03f, 3, 4);
            Util.ComMalha(gameObject, c.Gerar("mesa_mapas"), mat);
        }

        private void Rede(ConstrutorDeMalha c, Material mat)
        {
            Color m = new Color(0.42f, 0.29f, 0.18f);
            c.Cor(m).Tubo(new Vector3(0, 0, -1.4f), new Vector3(0, 1.8f, -1.4f), 0.08f, 0.07f, 6);
            c.Tubo(new Vector3(0, 0, 1.4f), new Vector3(0, 1.8f, 1.4f), 0.08f, 0.07f, 6);
            c.Cor(new Color(0.75f, 0.55f, 0.35f));
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float z = Mathf.Lerp(-1.3f, 1.3f, t);
                float y = 1.5f - Mathf.Sin(t * Mathf.PI) * 0.75f;
                c.Caixa(new Vector3(0, y, z), new Vector3(0.75f, 0.03f, 0.28f), 0.01f);
            }
            Util.ComMalha(gameObject, c.Gerar("rede"), mat);
        }

        private void MuroDosNomes(ConstrutorDeMalha c, Material mat)
        {
            var rng = new System.Random(41);
            c.Cor(new Color(0.12f, 0.11f, 0.1f));
            for (int linha = 0; linha < 9; linha++)
            for (int k = 0; k < 4; k++)
            {
                float y = 1.0f + linha * 0.3f;
                float z = -2f + k * 1.1f + (float)rng.NextDouble() * 0.2f;
                float largura = 0.4f + (float)rng.NextDouble() * 0.45f;
                for (int letra = 0; letra < 5; letra++)
                    c.Caixa(new Vector3(0.02f, y + (float)rng.NextDouble() * 0.04f, z + letra * largura / 5), new Vector3(0.01f, 0.12f, 0.015f));
                if (rng.NextDouble() < 0.4) c.Caixa(new Vector3(0.025f, y + 0.06f, z + largura * 0.5f), new Vector3(0.01f, 0.015f, largura + 0.1f));
            }
            Util.ComMalha(gameObject, c.Gerar("nomes"), Paleta.ToonSemContorno, false);
        }

        private void Brilho()
        {
            var c = new ConstrutorDeMalha();
            c.Cor(new Color(1f, 0.85f, 0.4f), 0f).Esfera(new Vector3(0, 0.25f, 0), Vector3.one * 0.12f, 4, 6);
            _brilho = Util.ComMalha(Util.Filho(transform, "brilho"), c.Gerar("brilho"), Paleta.Toon, false);
            Efeitos.Particulas.Facho(transform, new Color(1f, 0.8f, 0.35f));
            _luz = Luz(new Vector3(0, 0.8f, 0), new Color(1f, 0.8f, 0.4f), 2f, 4f);
        }

        private void Seta()
        {
            // Chevrons no chao apontando para a saida.
            var c = new ConstrutorDeMalha();
            for (int i = 0; i < 3; i++)
            {
                float z = -0.6f + i * 0.6f;
                c.Cor(new Color(1f, 0.85f, 0.5f, 0.35f));
                c.TrianguloChapado(new Vector3(-0.5f, 0.03f, z), new Vector3(0, 0.03f, z + 0.4f), new Vector3(0.5f, 0.03f, z), Vector3.up);
            }
            transform.rotation = Quaternion.identity;
            GameObject g = Util.Filho(transform, "seta");
            MeshRenderer r = Util.ComMalha(g, c.Gerar("seta"), Paleta.Aditiva("seta", 3, 1.2f), false);
            r.receiveShadows = false;
            g.SetActive(false);
            _movel = g.transform;
        }

        private Light Luz(Vector3 local, Color cor, float intensidade, float alcance)
        {
            GameObject g = Util.Filho(transform, "luz", local);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = cor;
            l.intensity = intensidade;
            l.range = alcance;
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ animacao

        private void Update()
        {
            if (O == null) return;
            float dt = Time.deltaTime;
            _tempo += dt;
            float alvo = O.Ligado || O.Usado ? 1 : 0;
            bool mudou = O.Ligado != _ultimoEstado;
            _ultimoEstado = O.Ligado;
            switch (O.Tipo)
            {
                case TipoObjeto.Porta:
                {
                    float vel = O.Modelo == "grade" ? 0.35f : 0.28f;
                    _aberto = Mathf.MoveTowards(_aberto, alvo, dt * vel);
                    float t = Mathf.SmoothStep(0, 1, _aberto);
                    if (O.Modelo == "grade") _movel.localPosition = Vector3.up * _curso * t;
                    else
                    {
                        _movel.localPosition = -_eixo * _curso * t;
                        _movel2.localPosition = _eixo * _curso * t;
                    }
                    if (mudou && _poeira != null) _poeira.Play();
                    if (_poeira != null && Mathf.Abs(_aberto - alvo) < 0.001f && _poeira.isPlaying) _poeira.Stop();
                    break;
                }
                case TipoObjeto.MuroMovel:
                {
                    _aberto = Mathf.MoveTowards(_aberto, alvo, dt * 0.25f);
                    float t = Mathf.SmoothStep(0, 1, _aberto);
                    // Treme um pouco enquanto se move: e concreto, nao elevador.
                    float tremor = (_aberto > 0.001f && _aberto < 0.999f) ? Mathf.Sin(_tempo * 40) * 0.03f : 0;
                    _movel.localPosition = new Vector3(tremor, Mathf.Lerp(-_alturaMuro - 0.2f, 0, t), 0);
                    if (mudou && _poeira != null) _poeira.Play();
                    if (_poeira != null && Mathf.Abs(_aberto - alvo) < 0.001f && _poeira.isPlaying) _poeira.Stop();
                    break;
                }
                case TipoObjeto.Alavanca:
                {
                    _aberto = Mathf.MoveTowards(_aberto, alvo, dt * 4);
                    _movel.localRotation = Quaternion.Euler(Mathf.Lerp(-35, 35, Mathf.SmoothStep(0, 1, _aberto)), 0, 0);
                    Color cor = O.Ligado ? new Color(0.3f, 1f, 0.35f) : new Color(1f, 0.25f, 0.1f);
                    if (_luz != null) _luz.color = cor;
                    if (_brilho != null) Pintar(_brilho, cor);
                    break;
                }
                case TipoObjeto.Bau:
                case TipoObjeto.Guarda:
                {
                    _aberto = Mathf.MoveTowards(_aberto, O.Tipo == TipoObjeto.Bau ? alvo : 0, dt * 3);
                    _movel.localRotation = Quaternion.Euler(0, 0, -100 * Mathf.SmoothStep(0, 1, _aberto));
                    if (_luz != null) _luz.enabled = !O.Usado;
                    break;
                }
                case TipoObjeto.Terminal:
                    if (_luz != null) _luz.intensity = 1.0f + Mathf.Sin(_tempo * 3) * 0.2f + (Random.value < 0.02f ? -0.6f : 0);
                    break;
                case TipoObjeto.Coletavel:
                {
                    bool vivo = !O.Usado;
                    if (_brilho != null)
                    {
                        _brilho.gameObject.SetActive(vivo);
                        _brilho.transform.localPosition = Vector3.up * (0.1f + Mathf.Sin(_tempo * 2) * 0.08f);
                    }
                    if (_luz != null) _luz.enabled = vivo;
                    if (!vivo) foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop();
                    break;
                }
                case TipoObjeto.Saida:
                    if (_movel != null)
                    {
                        _movel.gameObject.SetActive(true);
                        _movel.localScale = Vector3.one * (1 + Mathf.Sin(_tempo * 3) * 0.06f);
                    }
                    break;
            }
        }

        private static MaterialPropertyBlock _mpb;

        private static void Pintar(Renderer r, Color cor)
        {
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Cor", cor);
            r.SetPropertyBlock(_mpb);
        }
    }
}
