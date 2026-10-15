using System;
using System.Collections.Generic;
using System.IO;
using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Audio;
using Esquecidos.Jogo.Cena;
using Esquecidos.Jogo.Efeitos;
using Esquecidos.Jogo.Interface;
using Esquecidos.Motor;
using Esquecidos.Nucleo;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using static Esquecidos.Nucleo.Idioma;

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
        /// <summary>Tempo da tela escurecendo antes de trocar de mapa.</summary>
        public const float AtrasoDeTransicao = 0.3f;
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

        public const int Espacos = 3;

        /// <summary>O espaco de salvamento em uso (1 a 3): o salvamento automatico e o F5 vao para ele.</summary>
        public int Espaco { get; private set; } = 1;

        public static string CaminhoDoEspaco(int espaco) => Path.Combine(Application.persistentDataPath, $"salvo{espaco}.txt");
        public string CaminhoDoSalvo => CaminhoDoEspaco(Espaco);
        public static string CaminhoDasOpcoes => Path.Combine(Application.persistentDataPath, "opcoes.txt");

        private void Awake()
        {
            Instancia = this;
            Application.targetFrameRate = 144;
            Registro.Info("iniciando Os Esquecidos");
            bool teste = Teste.Pedido(out string modo);
            // O idioma vem antes de tudo: o catalogo e os roteiros sao lidos ja traduzidos.
            Opcoes.Ler();
            Recursos.AplicarIdioma(teste ? Teste.Argumento("idioma") ?? Opcoes.Idioma : Opcoes.Idioma);
            MigrarSalvoAntigo();
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
            if (teste) ModoTeste = true;
            Opcoes.Aplicar(this);

            var erros = Catalogo.Validar(nome => true);
            foreach (string e in erros) Registro.Erro("dados: " + e);

            if (teste)
            {
                Teste.Iniciar(this, modo);
                return;
            }
            AbrirMenu();
        }

        /// <summary>Partida nova ligada a todos os ouvintes da camada de jogo.</summary>
        private Partida NovaPartidaVazia()
        {
            var p = new Partida(Catalogo, () => Recursos.Textos("Roteiros")) { AtrasoDeTransicao = AtrasoDeTransicao };
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

        public void ComecarNovoJogo(string classe, string nome, Aparencia aparencia, int espaco = 0)
        {
            if (espaco >= 1 && espaco <= Espacos) Espaco = espaco;
            Demonstracao = false;
            Partida = NovaPartidaVazia();
            Camera.FocarCena(null);
            Camera.DefinirDistancia(CameraDoJogo.DistanciaDeJogo);
            Estado = EstadoDoJogo.Jogando;
            UI.EsconderMenus();
            Partida.NovaPartida(classe, nome, aparencia);
        }

        public bool TemSalvo => EspacoMaisRecente() > 0;

        public static bool EspacoUsado(int espaco) => File.Exists(CaminhoDoEspaco(espaco));

        /// <summary>O espaco salvo por ultimo, ou 0 se nao ha nenhum.</summary>
        public static int EspacoMaisRecente()
        {
            int melhor = 0;
            DateTime quando = DateTime.MinValue;
            for (int i = 1; i <= Espacos; i++)
            {
                if (!EspacoUsado(i)) continue;
                DateTime t = File.GetLastWriteTime(CaminhoDoEspaco(i));
                if (t > quando)
                {
                    quando = t;
                    melhor = i;
                }
            }
            return melhor;
        }

        /// <summary>Uma linha para a lista de espacos: quem, classe, nivel, dia e quando foi salvo.</summary>
        public string ResumoDoEspaco(int espaco)
        {
            if (!EspacoUsado(espaco)) return null;
            try
            {
                string texto = File.ReadAllText(CaminhoDoEspaco(espaco));
                List<Secao> secoes = LeitorIni.Ler(texto, "salvo");
                Secao jogo = secoes.Find(s => s.Nome == "jogo");
                Secao pers = secoes.Find(s => s.Nome == "personagem");
                string classe = pers?.Texto("classe");
                string nomeDaClasse = classe != null && Catalogo.Classes.TryGetValue(classe, out Nucleo.Regras.Classe c) ? c.Nome : classe;
                DateTime quando = File.GetLastWriteTime(CaminhoDoEspaco(espaco));
                return Idioma.F("{0} · {1} nível {2} · Dia {3} · {4:dd/MM HH:mm}", pers?.Texto("nome") ?? "?", nomeDaClasse, pers?.Inteiro("nivel", 1) ?? 1, jogo?.Inteiro("dia", 1) ?? 1, quando);
            }
            catch (Exception e)
            {
                Registro.Aviso("resumo do salvo: " + e.Message);
                return Idioma.T("(arquivo danificado)");
            }
        }

        /// <summary>O salvo de antes dos espacos ("salvo.txt") vira o espaco 1.</summary>
        private static void MigrarSalvoAntigo()
        {
            try
            {
                string antigo = Path.Combine(Application.persistentDataPath, "salvo.txt");
                if (File.Exists(antigo) && !EspacoUsado(1)) File.Move(antigo, CaminhoDoEspaco(1));
            }
            catch (Exception e)
            {
                Registro.Aviso("migrar salvo: " + e.Message);
            }
        }

        public bool Continuar() => CarregarEspaco(EspacoMaisRecente());

        public bool CarregarEspacoAtual() => CarregarEspaco(Espaco);

        public bool CarregarEspaco(int espaco)
        {
            if (espaco < 1 || !EspacoUsado(espaco)) return false;
            Espaco = espaco;
            try
            {
                return CarregarTexto(File.ReadAllText(CaminhoDoSalvo));
            }
            catch (Exception e)
            {
                Registro.Erro("carregar: " + e);
                UI.Mensagem(Idioma.T("Não foi possível carregar o jogo salvo."), "#ff8060");
                return false;
            }
        }

        /// <summary>Troca o idioma: guarda nas opcoes e remonta o jogo inteiro, ja traduzido.</summary>
        public void TrocarIdioma(string idioma)
        {
            if (idioma == Opcoes.Idioma) return;
            Opcoes.Idioma = idioma;
            Opcoes.Salvar(this);
            Instancia = null;
            // Desligar antes de destruir: o EventSystem e a camera antigos saem de cena ja neste quadro.
            gameObject.SetActive(false);
            Destroy(gameObject);
            Inicio.Recomecar();
        }

        /// <summary>Monta a partida a partir do texto de um jogo salvo.</summary>
        public bool CarregarTexto(string texto)
        {
            try
            {
                Demonstracao = false;
                Partida = NovaPartidaVazia();
                Camera.FocarCena(null);
                Camera.DefinirDistancia(CameraDoJogo.DistanciaDeJogo);
                Estado = EstadoDoJogo.Jogando;
                UI.EsconderMenus();
                Partida.Carregar(texto);
                UI.Mensagem(Idioma.T("Jogo carregado."), "#c8e0ff");
                return true;
            }
            catch (Exception e)
            {
                Registro.Erro("carregar: " + e);
                UI.Mensagem(Idioma.T("Não foi possível carregar o jogo salvo."), "#ff8060");
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
                UI.Mensagem(Idioma.F("Jogo salvo (espaço {0}).", Espaco), "#c8e0ff");
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

        /// <summary>Quanto custou montar o ultimo mapa, para o relatorio do modo de teste.</summary>
        public string TempoDaUltimaCarga { get; private set; } = "";

        public void Executar(NotMapaCarregado n)
        {
            var relogio = System.Diagnostics.Stopwatch.StartNew();
            if (Cena != null) Destroy(Cena.gameObject);
            Cena = ConstrutorDeCena.Construir(n.Mapa, n.Mundo, _mundo);
            long cena = relogio.ElapsedMilliseconds;
            Efeitos.Limpar(_mundo);
            Visuais.Reconstruir(n.Mundo, _mundo);
            TempoDaUltimaCarga = $"cena {cena} ms, atores {relogio.ElapsedMilliseconds - cena} ms";
            Camera.Teleportar();
            if (!Demonstracao)
            {
                Sons.TocarTema(n.Mapa.Musica);
                UI.MostrarLegenda(n.Mapa.Nome, 3.5f);
                if (n.Mapa.Id == "patio") Salvar();
            }
            UI.MapaMudou(n.Mapa);
        }

        public void Executar(NotTransicao n) => UI.EscurecerParaTrocar(AtrasoDeTransicao * 0.85f);

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
                Nucleo.Mapas.Mapa antes = Partida.Mapa;
                var relogio = System.Diagnostics.Stopwatch.StartNew();
                while (_acumulador >= Passo && passos < 5)
                {
                    Partida.Passo(Passo);
                    _acumulador -= Passo;
                    passos++;
                }
                if (Partida.Mapa != antes)
                {
                    TempoDaUltimaCarga += $", total {relogio.ElapsedMilliseconds} ms";
                    // O passo que montou o mapa foi longo: nao tenta recuperar o atraso de uma vez.
                    _acumulador = 0;
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
