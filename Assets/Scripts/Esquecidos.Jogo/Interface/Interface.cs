using System.Collections.Generic;
using Esquecidos.Motor.Entrada;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Roteiro;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>
    /// Toda a interface: HUD, dialogo, janelas e menus, num canvas so. Recebe
    /// as notificacoes da partida e os comandos de tela dos roteiros (fala,
    /// escolha, letreiro, escurecer), e devolve as escolhas do jogador.
    /// </summary>
    public sealed class Interface : MonoBehaviour,
        IExecutor<Fala>, IExecutor<Narrar>, IExecutor<Legenda>, IExecutor<Dica>,
        IExecutor<Escurecer>, IExecutor<Clarear>, IExecutor<CameraFoca>, IExecutor<CameraVolta>, IExecutor<Tremer>,
        IExecutor<MostrarEscolha>, IExecutor<AbrirLoja>, IExecutor<AbrirOficina>, IExecutor<AbrirGuarda>,
        IExecutor<AbrirMapa>, IExecutor<AbrirRegistro>, IExecutor<Creditos>, IExecutor<SalvarJogo>,
        IExecutor<NotMensagem>, IExecutor<NotMostrarTopicos>, IExecutor<NotMissao>, IExecutor<NotNivelSubiu>,
        IExecutor<NotItemRecebido>, IExecutor<NotRegistroNovo>, IExecutor<RoteiroTerminou>
    {
        public Principal P { get; private set; }
        public Canvas Canvas { get; private set; }
        public Estudio Estudio { get; private set; }
        public Hud Hud { get; private set; }
        public Dialogo Dialogo { get; private set; }
        public Janelas Janelas { get; private set; }
        public Menus Menus { get; private set; }
        public CaixaDeDica Dicas { get; private set; }

        private RectTransform _camadaMundo, _camadaHud, _camadaJanelas, _camadaEscuro, _camadaDialogo, _camadaMenus, _camadaTopo;
        private Image _escuro;
        private float _escuroAlvo, _escuroVel = 2;
        private Text _legenda;
        private float _legendaT, _legendaDur;
        private readonly List<(Text texto, float t)> _mensagens = new List<(Text, float)>();
        private RectTransform _caixaMensagens;

        private sealed class TextoFlutuante
        {
            public Text Texto;
            public Vector3 Mundo;
            public float T, Vida, Escala;
            public Vector2 Desvio;
            public bool Grande;
        }

        private readonly List<TextoFlutuante> _flutuantes = new List<TextoFlutuante>();
        private readonly Stack<Text> _poolFlutuantes = new Stack<Text>();

        /// <summary>Tamanho do texto e de toda a interface (1 = normal): a resolucao de referencia encolhe e tudo cresce.</summary>
        public void EscalaDoTexto(float escala)
        {
            var cs = GetComponent<CanvasScaler>();
            if (cs != null) cs.referenceResolution = new Vector2(1920, 1080) / Mathf.Clamp(escala, 0.7f, 1.6f);
        }

        /// <summary>Esconde toda a interface do jogo (HUD, dicas, mensagens, nomes): para fotos.</summary>
        public void MostrarCamadas(bool sim)
        {
            _camadaHud.gameObject.SetActive(sim);
            _camadaMundo.gameObject.SetActive(sim);
        }

        public bool JanelaAberta => Janelas != null && Janelas.Alguma;
        public bool JanelaPausa => JanelaAberta;
        public bool Pausado => Menus != null && Menus.Pausado;

        public static Interface Criar(Principal p, Transform pai)
        {
            var g = new GameObject("Interface");
            g.transform.SetParent(pai, false);
            var c = g.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            var escala = g.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920, 1080);
            escala.matchWidthOrHeight = 0.5f;
            g.AddComponent<GraphicRaycaster>();
            var i = g.AddComponent<Interface>();
            i.P = p;
            i.Canvas = c;
            i.Montar();
            return i;
        }

        private void Montar()
        {
            Estudio = Estudio.Criar(transform.parent);
            _camadaMundo = IU.Novo(transform, "mundo");
            _camadaMundo.Esticar();
            _camadaHud = IU.Novo(transform, "hud");
            _camadaHud.Esticar();
            _camadaJanelas = IU.Novo(transform, "janelas");
            _camadaJanelas.Esticar();
            // O escuro das cenas cobre o mundo e a HUD, mas a narracao aparece por cima dele.
            _camadaEscuro = IU.Novo(transform, "escuro");
            _camadaEscuro.Esticar();
            _camadaDialogo = IU.Novo(transform, "dialogo");
            _camadaDialogo.Esticar();
            _camadaMenus = IU.Novo(transform, "menus");
            _camadaMenus.Esticar();
            _camadaTopo = IU.Novo(transform, "topo");
            _camadaTopo.Esticar();

            Hud = new Hud(this, _camadaHud, _camadaMundo);
            Dialogo = new Dialogo(this, _camadaDialogo);
            Janelas = new Janelas(this, _camadaJanelas);
            Menus = new Menus(this, _camadaMenus);
            Dicas = new CaixaDeDica(this, _camadaHud);

            _caixaMensagens = IU.Novo(_camadaHud, "mensagens").Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 190), new Vector2(620, 220));
            _legenda = IU.Texto(_camadaTopo, "legenda", "", 54, IU.CorTexto, TextAnchor.MiddleCenter);
            _legenda.rectTransform.Lugar(new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 120));
            _legenda.color = new Color(1, 1, 1, 0);
            _escuro = IU.Imagem(_camadaEscuro, "escuro", Color.black);
            _escuro.rectTransform.Esticar();
            _escuro.raycastTarget = false;
            _escuro.color = new Color(0, 0, 0, 1);
            _escuroAlvo = 0;
        }

        private bool Demo => P.Demonstracao;
        private Partida Partida => P.Partida;

        // ------------------------------------------------------------------ laco

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Color e = _escuro.color;
            e.a = Mathf.MoveTowards(e.a, _escuroAlvo, dt * _escuroVel);
            _escuro.color = e;
            _escuro.enabled = e.a > 0.001f;

            if (_legendaDur > 0)
            {
                _legendaT += dt;
                float a = Mathf.Clamp01(_legendaT / 0.6f) * Mathf.Clamp01((_legendaDur - _legendaT) / 0.8f);
                _legenda.color = new Color(1, 0.95f, 0.85f, a);
                if (_legendaT > _legendaDur) _legendaDur = 0;
            }

            for (int i = _mensagens.Count - 1; i >= 0; i--)
            {
                (Text t, float idade) = _mensagens[i];
                idade += dt;
                _mensagens[i] = (t, idade);
                float a = Mathf.Clamp01((9 - idade) / 1.5f);
                Color c = t.color;
                t.color = new Color(c.r, c.g, c.b, a);
                if (idade > 9)
                {
                    Destroy(t.gameObject);
                    _mensagens.RemoveAt(i);
                }
            }
            Hud.Atualizar(dt);
            Dialogo.Atualizar(dt);
            Janelas.Atualizar(dt);
            Menus.Atualizar(dt);
            Dicas.Atualizar(dt);
            Teclas();
        }

        private void LateUpdate()
        {
            Camera cam = P.Camera.Cam;
            float dt = Time.unscaledDeltaTime;
            float escala = Canvas.scaleFactor;
            for (int i = _flutuantes.Count - 1; i >= 0; i--)
            {
                TextoFlutuante f = _flutuantes[i];
                f.T += dt;
                if (f.T > f.Vida)
                {
                    f.Texto.gameObject.SetActive(false);
                    _poolFlutuantes.Push(f.Texto);
                    _flutuantes.RemoveAt(i);
                    continue;
                }
                Vector3 tela = cam.WorldToScreenPoint(f.Mundo);
                if (tela.z < 0)
                {
                    f.Texto.enabled = false;
                    continue;
                }
                f.Texto.enabled = true;
                float u = f.T / f.Vida;
                Vector2 pos = (Vector2)tela / escala + f.Desvio + new Vector2(0, 40 * Mathf.Sqrt(u) + (f.Grande ? 20 : 0));
                f.Texto.rectTransform.anchoredPosition = pos;
                float pulo = f.Grande ? 1 + Mathf.Max(0, 0.6f - f.T * 4) : 1;
                f.Texto.rectTransform.localScale = Vector3.one * f.Escala * pulo;
                Color c = f.Texto.color;
                f.Texto.color = new Color(c.r, c.g, c.b, Mathf.Clamp01((1 - u) * 3));
            }
            Hud.Rotulos(cam, escala);
        }

        private void Teclas()
        {
            if (P.Estado == EstadoDoJogo.Menu || P.Estado == EstadoDoJogo.Creditos || Menus.CapturandoTecla) return;
            if (Controles.Desceu(Key.Escape) || (Controles.Controle?.startButton.wasPressedThisFrame ?? false))
            {
                if (Dialogo.Aberto && Dialogo.PodeFechar) Dialogo.Fechar();
                else if (JanelaAberta) Janelas.FecharTodas();
                else if (Menus.Pausado) Menus.Despausar();
                else if (P.Estado == EstadoDoJogo.Jogando) Menus.Pausar();
                return;
            }
            if (P.Estado != EstadoDoJogo.Jogando || Menus.Pausado || Partida.EmCena) return;
            if (Controles.Desceu(Acao.Mochila) || (Controles.Controle?.selectButton.wasPressedThisFrame ?? false)) Janelas.Alternar("inventario");
            if (Controles.Desceu(Acao.Personagem)) Janelas.Alternar("personagem");
            if (Controles.Desceu(Acao.Poderes)) Janelas.Alternar("poderes");
            if (Controles.Desceu(Acao.Diario)) Janelas.Alternar("diario");
            if (Controles.Desceu(Acao.Mapa) || Controles.Desceu(Key.Tab)) Janelas.Alternar("mapa");
            if (Controles.Desceu(Acao.Salvar)) P.Salvar();
            if (Controles.Desceu(Acao.Carregar)) P.CarregarEspacoAtual();
        }

        // ------------------------------------------------------------------ servicos

        public void Mensagem(string texto, string cor = "#e8e2d0")
        {
            Text t = IU.Texto(_caixaMensagens, "msg", texto, 21, Util.Cor(cor, IU.CorTexto));
            t.rectTransform.Lugar(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(620, 28));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            _mensagens.Add((t, 0));
            while (_mensagens.Count > 7)
            {
                Destroy(_mensagens[0].texto.gameObject);
                _mensagens.RemoveAt(0);
            }
            for (int i = 0; i < _mensagens.Count; i++)
                _mensagens[i].texto.rectTransform.anchoredPosition = new Vector2(0, (_mensagens.Count - 1 - i) * 28);
        }

        public void Flutuante(Vector3 mundo, string texto, Color cor, float escala = 1f, bool grande = false)
        {
            Text t = _poolFlutuantes.Count > 0 ? _poolFlutuantes.Pop() : null;
            if (t == null)
            {
                t = IU.Texto(_camadaMundo, "flutuante", "", 30, Color.white, TextAnchor.MiddleCenter);
                t.rectTransform.Lugar(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 60));
                t.fontStyle = FontStyle.Bold;
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.9f);
                o.effectDistance = new Vector2(2, -2);
            }
            t.gameObject.SetActive(true);
            t.text = texto;
            t.color = cor;
            _flutuantes.Add(new TextoFlutuante
            {
                Texto = t,
                Mundo = mundo,
                Vida = grande ? 1.3f : 0.95f,
                Escala = escala,
                Grande = grande,
                Desvio = new Vector2(Random.Range(-26f, 26f), Random.Range(-8f, 8f)),
            });
        }

        public void MostrarLegenda(string texto, float segundos)
        {
            _legenda.text = texto;
            _legendaT = 0;
            _legendaDur = segundos;
        }

        private bool _emTransicao;

        /// <summary>Troca de mapa pedida: escurece a tela antes do mapa novo ser montado.</summary>
        public void EscurecerParaTrocar(float segundos)
        {
            _emTransicao = true;
            _escuroAlvo = 1;
            _escuroVel = 1 / Mathf.Max(0.05f, segundos);
        }

        public void MapaMudou(Mapa mapa)
        {
            Hud.MapaMudou(mapa);
            if (_emTransicao)
            {
                // Mapa montado no escuro: agora clareia devagar.
                _emTransicao = false;
                _escuroAlvo = 0;
                _escuroVel = 1 / 0.45f;
            }
        }

        public void MostrarMenuPrincipal()
        {
            _escuroAlvo = 0;
            _escuroVel = 0.8f;
            Janelas.FecharTodas();
            Dialogo.Fechar(true);
            Hud.Mostrar(false);
            Menus.Principal();
        }

        public void EsconderMenus()
        {
            Menus.EsconderTudo();
            Hud.Mostrar(true);
        }

        public void MostrarMorte(int perda) => Menus.Morte(perda);

        public void MostrarCreditos(string final)
        {
            Janelas.FecharTodas();
            Dialogo.Fechar(true);
            Hud.Mostrar(false);
            Menus.Creditos(final);
        }

        // ------------------------------------------------------------------ comandos de roteiro

        private void Esperar(System.Func<bool> condicao, float limite = -1)
        {
            Partida.Despachante.Despachar(new PedidoDeEspera(new EsperaAte(condicao, limite)));
        }

        public void Executar(Fala c)
        {
            if (Demo) return;
            Dialogo.Fala fala = Dialogo.Falar(c.Quem, c.Texto);
            Esperar(() => fala.Concluida);
        }

        public void Executar(Narrar c)
        {
            if (Demo) return;
            Dialogo.Fala fala = Dialogo.Falar(null, c.Texto);
            Esperar(() => fala.Concluida);
        }

        public void Executar(Legenda c)
        {
            if (!Demo) MostrarLegenda(c.Texto, c.Segundos);
        }

        public void Executar(Dica c)
        {
            if (!Demo) Dicas.Mostrar(c.Texto);
        }

        public void Executar(Escurecer c)
        {
            if (Demo) return;
            _escuroAlvo = 1;
            _escuroVel = 1 / Mathf.Max(0.01f, c.Segundos);
            if (c.Segundos <= 0.01f)
            {
                _escuro.color = Color.black;
                return;
            }
            Partida.Despachante.Despachar(new PedidoDeEspera(new EsperaDeTempo(c.Segundos)));
        }

        public void Executar(Clarear c)
        {
            if (Demo) return;
            _escuroAlvo = 0;
            _escuroVel = 1 / Mathf.Max(0.01f, c.Segundos);
            Partida.Despachante.Despachar(new PedidoDeEspera(new EsperaDeTempo(c.Segundos * 0.6f)));
        }

        public void Executar(CameraFoca c)
        {
            Entidade e = Partida.Ator(c.Ator);
            if (e == null) return;
            P.Camera.FocarCena(Util.Mundo(e.Pos), 1.5f / Mathf.Max(0.2f, c.Segundos));
            Partida.Despachante.Despachar(new PedidoDeEspera(new EsperaDeTempo(c.Segundos)));
        }

        public void Executar(CameraVolta c)
        {
            P.Camera.FocarCena(null);
        }

        public void Executar(Tremer c) => P.Camera.Tremer(c.Forca);

        public void Executar(MostrarEscolha c)
        {
            if (Demo)
            {
                Partida.Roteiros.Escolher(0);
                return;
            }
            Dialogo.Escolhas(c.Opcoes, i => Partida.Roteiros.Escolher(i));
        }

        public void Executar(AbrirLoja c)
        {
            if (Demo) return;
            Janelas.AbrirLoja(c.Loja);
        }

        public void Executar(AbrirOficina c)
        {
            if (!Demo) Janelas.AbrirOficina(c.Estacao);
        }

        public void Executar(AbrirGuarda c)
        {
            if (!Demo) Janelas.AbrirGuarda();
        }

        public void Executar(AbrirMapa c)
        {
            if (!Demo) Janelas.AbrirMapa(true);
        }

        public void Executar(AbrirRegistro c)
        {
            if (!Demo) Janelas.AbrirRegistro(c.Registro);
        }

        public void Executar(Creditos c)
        {
            if (!Demo) P.MostrarCreditos(c.Final);
        }

        public void Executar(SalvarJogo c) => P.Salvar();

        public void Executar(RoteiroTerminou c)
        {
            if (!Partida.EmCena)
            {
                P.Camera.FocarCena(null);
                if (!Dialogo.MostrandoTopicos) Dialogo.FecharQuandoAcabar();
            }
        }

        // ------------------------------------------------------------------ notificacoes

        public void Executar(NotMensagem n)
        {
            if (!Demo) Mensagem(n.Texto, n.Cor);
        }

        public void Executar(NotMostrarTopicos n)
        {
            if (!Demo) Dialogo.Topicos(n.Npc, n.Topicos);
        }

        public void Executar(NotMissao n)
        {
            if (Demo) return;
            if (n.Nova) Mensagem(F("Nova missão: {0}", n.Missao.Nome), "#ffd25a");
            else if (n.Concluida) Mensagem(F("Missão concluída: {0}", n.Missao.Nome), "#ffd25a");
            else Mensagem($"{n.Missao.Nome}: {n.Texto}", "#e0c890");
            Hud.MissaoMudou();
        }

        public void Executar(NotNivelSubiu n)
        {
            if (Demo) return;
            MostrarLegenda(F("Nível {0}", n.Nivel), 2.5f);
            if (P.Partida.Jogador != null)
                Efeitos.Particulas.Brilhos(Util.Mundo(P.Partida.Jogador.Pos, 1), 40, new Color(1f, 0.85f, 0.4f), 0.8f, 3);
        }

        public void Executar(NotItemRecebido n)
        {
            if (Demo) return;
            string nome = DescricaoDeItem.Nome(n.Item, Partida.Catalogo);
            string qtd = n.Item.Quantidade > 1 ? $" ×{n.Item.Quantidade}" : "";
            Mensagem($"+ {nome}{qtd}", DescricaoDeItem.CorDaRaridade(n.Item.Raridade));
        }

        public void Executar(NotRegistroNovo n)
        {
            if (!Demo) Mensagem(F("Registro guardado no diário: {0}", n.Registro.Titulo), "#9fd0ff");
        }
    }
}
