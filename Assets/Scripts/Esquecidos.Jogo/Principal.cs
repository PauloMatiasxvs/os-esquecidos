using System;
using System.Collections.Generic;
using System.IO;
using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Audio;
using Esquecidos.Jogo.Cena;
using Esquecidos.Jogo.Efeitos;
using Esquecidos.Jogo.Interface;
using Esquecidos.Motor;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Esquecidos.Jogo
{
    public enum EstadoDoJogo
    {
        Menu,
        Jogando,
        Morto,
        Creditos,
    }

    /// <summary>
    /// O objeto raiz do jogo, como a instancia principal do Pal3.Unity: monta
    /// os servicos (camera, ambiente, som, interface), cria a Partida do
    /// nucleo e a faz andar em passos fixos de 60 Hz. O desenho le a
    /// simulacao entre um passo e outro com interpolacao.
    /// </summary>
    public sealed class Principal : MonoBehaviour, IExecutor<NotMapaCarregado>, IExecutor<NotJogadorMorreu>, IExecutor<NotTransicao>
    {
        public const float Passo = 1f / 60f;
        public static Principal Instancia { get; private set; }

        public Catalogo Catalogo { get; private set; }
        public Partida Partida { get; private set; }
        public CameraDoJogo Camera { get; private set; }
        public Ambiente Ambiente { get; private set; }
        public Sons Sons { get; private set; }
        public Visuais Visuais { get; private set; }
        public GerenciadorDeEfeitos Efeitos { get; private set; }
        public Interface.Interface UI { get; private set; }
        public Controle Controle { get; private set; }
        public Cenario Cena { get; private set; }
        public EstadoDoJogo Estado { get; private set; } = EstadoDoJogo.Menu;
        public bool Demonstracao { get; private set; }
        /// <summary>Rodando o roteiro de teste: nada de gravar por cima do jogo salvo de verdade.</summary>
        public bool ModoTeste { get; set; }

        private Transform _mundo;
        private float _acumulador;
        private float _congelado;
        private float _lento;
        private float _orbita;

        public static string CaminhoDoSalvo => Path.Combine(Application.persistentDataPath, "salvo.txt");
        public static string CaminhoDasOpcoes => Path.Combine(Application.persistentDataPath, "opcoes.txt");

        private void Awake()
        {
            Instancia = this;
            Application.targetFrameRate = 144;
            Registro.Info("iniciando Os Esquecidos");
            Catalogo = Recursos.Catalogo();
            _mundo = new GameObject("Mundo").transform;
            _mundo.SetParent(transform, false);
            Camera = CameraDoJogo.Criar(transform);
            Ambiente = Ambiente.Criar(transform);
            Sons = Sons.Criar(transform);
            Particulas.Iniciar(transform);
            Visuais = new Visuais(this);
            Efeitos = new GerenciadorDeEfeitos(this);
            Controle = new Controle(this);
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            UI = Interface.Interface.Criar(this, transform);
            Opcoes.Carregar(this);

            var erros = Catalogo.Validar(nome => true);
            foreach (string e in erros) Registro.Erro("dados: " + e);

            if (Teste.Pedido(out string modo))
            {
                Teste.Iniciar(this, modo);
                return;
            }
            AbrirMenu();
        }

        /// <summary>Partida nova ligada a todos os ouvintes da camada de jogo.</summary>
        private Partida NovaPartidaVazia()
        {
            var p = new Partida(Catalogo, () => Recursos.Textos("Roteiros"));
            RegistroDeExecutores r = p.Registro;
            r.Registrar(this);
            r.Registrar(Visuais);
            r.Registrar(Efeitos);
            r.Registrar(Sons);
            r.Registrar(UI);
            p.Roteiros.Erro += m => Registro.Erro("roteiro: " + m);
            var erros = p.Roteiros.Validar();
            foreach (string e in erros) Registro.Erro("roteiro: " + e);
            return p;
        }

        // ------------------------------------------------------------------ fluxo

        /// <summary>Menu com o Patio ao fundo, ao entardecer, a camera girando devagar.</summary>
        public void AbrirMenu()
        {
            Estado = EstadoDoJogo.Menu;
            Demonstracao = true;
            Partida = NovaPartidaVazia();
            Partida.NovaPartida("corredor", "", null, 777);
            Partida.Roteiros.PararTodos();
            Partida.Ciclo.Definir(1, 18.6f);
            Partida.Ciclo.Parado = true;
            if (Partida.Jogador != null)
            {
                VisualDeEntidade v = Visuais.De(Partida.Jogador);
                if (v != null) v.gameObject.SetActive(false);
            }
            Camera.Alvo = null;
            Camera.FocarCena(new Vector3(48, 0, 50), 1);
            Camera.Aproximar(-10);
            Sons.TocarTema("menu");
            UI.MostrarMenuPrincipal();
        }

        public void ComecarNovoJogo(string classe, string nome, Aparencia aparencia)
        {
            Demonstracao = false;
            Partida = NovaPartidaVazia();
            Camera.FocarCena(null);
            Camera.DefinirDistancia(CameraDoJogo.DistanciaDeJogo);
            Estado = EstadoDoJogo.Jogando;
            UI.EsconderMenus();
            Partida.NovaPartida(classe, nome, aparencia);
        }

        public bool TemSalvo => File.Exists(CaminhoDoSalvo);

        public bool Continuar()
        {
            if (!TemSalvo) return false;
            try
            {
                string texto = File.ReadAllText(CaminhoDoSalvo);
                Demonstracao = false;
                Partida = NovaPartidaVazia();
                Camera.FocarCena(null);
                Camera.DefinirDistancia(CameraDoJogo.DistanciaDeJogo);
                Estado = EstadoDoJogo.Jogando;
                UI.EsconderMenus();
                Partida.Carregar(texto);
                UI.Mensagem("Jogo carregado.", "#c8e0ff");
                return true;
            }
            catch (Exception e)
            {
                Registro.Erro("carregar: " + e);
                UI.Mensagem("Não foi possível carregar o jogo salvo.", "#ff8060");
                return false;
            }
        }

        public void Salvar()
        {
            if (Demonstracao || ModoTeste || Partida == null || Partida.JogadorMorto) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CaminhoDoSalvo));
                File.WriteAllText(CaminhoDoSalvo, Partida.Salvar());
                UI.Mensagem("Jogo salvo.", "#c8e0ff");
            }
            catch (Exception e)
            {
                Registro.Erro("salvar: " + e);
            }
        }

        public void Renascer()
        {
            Estado = EstadoDoJogo.Jogando;
            Partida.Renascer();
        }

        public void MostrarCreditos(string final)
        {
            Estado = EstadoDoJogo.Creditos;
            Salvar();
            UI.MostrarCreditos(final);
        }

        /// <summary>Pausa curtinha no golpe forte: o impacto "pesa".</summary>
        public void Congelar(float segundos) => _congelado = Mathf.Max(_congelado, segundos);

        public void CameraLenta(float segundos) => _lento = Mathf.Max(_lento, segundos);

        // ------------------------------------------------------------------ notificacoes

        public void Executar(NotMapaCarregado n)
        {
            if (Cena != null) Destroy(Cena.gameObject);
            Cena = ConstrutorDeCena.Construir(n.Mapa, n.Mundo, _mundo);
            Efeitos.Limpar(_mundo);
            Visuais.Reconstruir(n.Mundo, _mundo);
            Camera.Teleportar();
            if (!Demonstracao)
            {
                Sons.TocarTema(n.Mapa.Musica);
                UI.MostrarLegenda(n.Mapa.Nome, 3.5f);
                if (n.Mapa.Id == "patio") Salvar();
            }
            UI.MapaMudou(n.Mapa);
        }

        public void Executar(NotTransicao n) => UI.Piscar(0.35f);

        public void Executar(NotJogadorMorreu n)
        {
            Estado = EstadoDoJogo.Morto;
            CameraLenta(1.5f);
            UI.MostrarMorte(n.SucataPerdida);
        }

        // ------------------------------------------------------------------ laco

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.1f) dt = 0.1f;
            if (_congelado > 0)
            {
                _congelado -= dt;
                Time.timeScale = 0.05f;
            }
            else if (_lento > 0)
            {
                _lento -= dt;
                Time.timeScale = 0.35f;
            }
            else
            {
                Time.timeScale = 1;
            }
            if (Partida == null) return;

            bool janela = UI.JanelaAberta;
            bool pausado = UI.Pausado;
            if (Estado == EstadoDoJogo.Jogando && !Demonstracao) Controle.Atualizar(dt, janela || pausado);

            // Menus de tela inteira pausam a simulacao: jogo de um jogador so, ninguem precisa correr.
            bool rodar = !pausado && !(janela && UI.JanelaPausa) && Estado != EstadoDoJogo.Creditos;
            if (rodar)
            {
                _acumulador += dt * Time.timeScale;
                int passos = 0;
                while (_acumulador >= Passo && passos < 5)
                {
                    Partida.Passo(Passo);
                    _acumulador -= Passo;
                    passos++;
                }
                if (passos == 5) _acumulador = 0;
            }
            VisualDeEntidade.Alfa = Mathf.Clamp01(_acumulador / Passo);
            Efeitos.Atualizar();

            Vector3 posJogador = Partida.Jogador != null ? Visuais.De(Partida.Jogador)?.PosicaoVisual ?? Util.Mundo(Partida.Jogador.Pos) : Vector3.zero;
            bool corte = Partida.Mapa != null && !Demonstracao && Partida.Jogador != null;
            Ambiente.Atualizar(Partida.Ciclo, Partida.Mapa, posJogador, corte, Cena);
            if (Demonstracao)
            {
                _orbita += dt * 4;
                Camera.GirarLivre(dt * 4);
            }
            AtualizarIntensidadeDaMusica();
        }

        /// <summary>Quanto mais bicho acordado perto, mais a percussao entra.</summary>
        private void AtualizarIntensidadeDaMusica()
        {
            Entidade j = Partida.Jogador;
            float intensidade = 0;
            if (j != null && !Demonstracao)
            {
                foreach (Entidade e in Partida.Mundo.Entidades)
                {
                    if (!e.Vivo || e.Faccao != Faccao.Inimigo || e.Def == null || e.Tipo == "boneco") continue;
                    float d = System.Numerics.Vector2.Distance(e.Pos, j.Pos);
                    if (d > 16) continue;
                    bool acordado = e.Cerebro is IAComum ia && ia.Acordado;
                    if (acordado) intensidade += e.Chefe ? 1f : 0.3f;
                }
            }
            Sons.Musica.IntensidadeAlvo = Mathf.Clamp01(intensidade);
            Sons.Musica.NoiteAlvo = Partida.Ciclo.Noite ? 1 : 0;
        }
    }
}
