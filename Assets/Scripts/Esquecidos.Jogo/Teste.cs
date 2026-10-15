using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Esquecidos.Motor;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using Vec2 = System.Numerics.Vector2;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Modos de teste do executavel, que jogam sozinhos e tiram fotos da tela:
    ///   --captura   passeio pelas telas (menu, Patio, janelas, setor, noite, morte)
    ///   --campanha  a historia principal inteira, setor por setor, e os dois finais
    ///   --fotos     fotos bonitas, sem interface, para a pagina do jogo
    /// As fotos e um relatorio com os erros do log vao para "capturas" ao lado
    /// do jogo (ou --saida=pasta). A janela vai para fora da tela, sem som e
    /// sem roubar o teclado de quem estiver usando o PC.
    /// </summary>
    public static class Teste
    {
        private static readonly string[] Modos = { "captura", "campanha", "fotos", "video", "autoteste" };

        public static bool Pedido(out string modo)
        {
            modo = null;
            foreach (string a in Environment.GetCommandLineArgs())
                foreach (string m in Modos)
                    if (a == "--" + m) modo = m;
            return modo != null;
        }

        public static string Argumento(string nome)
        {
            string prefixo = "--" + nome + "=";
            foreach (string a in Environment.GetCommandLineArgs())
                if (a.StartsWith(prefixo, StringComparison.Ordinal)) return a.Substring(prefixo.Length);
            return null;
        }

        public static void Iniciar(Principal p, string modo)
        {
            p.ModoTeste = true;
            Motor.Entrada.Controles.Ignorar = true;
            // Mudo de verdade: quem testa nao precisa ouvir o jogo rodando escondido.
            AudioListener.volume = 0;
            AudioListener.pause = true;
            p.Sons.VolumeEfeitos = 0;
            p.Sons.Musica.Volume = 0;
            p.Sons.Musica.VolumeAmbiente = 0;
            foreach (AudioSource a in p.GetComponentsInChildren<AudioSource>(true)) a.mute = true;
            var modulo = UnityEngine.EventSystems.EventSystem.current?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (modulo != null) modulo.enabled = false;
            var r = p.gameObject.AddComponent<RoteiroDeTeste>();
            r.Comecar(p, modo);
        }
    }

    /// <summary>Tira a janela do jogo da frente: fora da tela e devolvendo o foco a quem estava antes.</summary>
    internal static class JanelaDiscreta
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private delegate bool AoAchar(IntPtr h, IntPtr param);
        [DllImport("user32.dll")] private static extern bool EnumWindows(AoAchar cb, IntPtr param);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int indice);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr depois, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int indice);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int indice, int valor);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int comando);

        private static IntPtr _minha;

        private static bool Achou(IntPtr h, IntPtr param)
        {
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id && IsWindowVisible(h))
            {
                _minha = h;
                return false;
            }
            return true;
        }

        public static string Esconder()
        {
            try
            {
                _minha = IntPtr.Zero;
                EnumWindows(Achou, IntPtr.Zero);
                IntPtr eu = _minha;
                if (eu == IntPtr.Zero) return "janela do jogo nao encontrada";
                // Janela de ferramenta nao aparece na barra de tarefas nem no Alt+Tab.
                const int EstiloEstendido = -20, Ferramenta = 0x80, DeAplicativo = 0x40000;
                ShowWindow(eu, 0); // SW_HIDE
                SetWindowLong(eu, EstiloEstendido, (GetWindowLong(eu, EstiloEstendido) | Ferramenta) & ~DeAplicativo);
                ShowWindow(eu, 4); // SW_SHOWNOACTIVATE
                int esquerda = GetSystemMetrics(76); // SM_XVIRTUALSCREEN
                const uint SemTamanho = 0x0001, SemAtivar = 0x0010;
                bool moveu = SetWindowPos(eu, new IntPtr(1), esquerda - 5000, 40, 0, 0, SemTamanho | SemAtivar); // HWND_BOTTOM
                string foco = "";
                if (GetForegroundWindow() == eu)
                {
                    // Devolve o foco para a proxima janela visivel com titulo (quem estava sendo usado).
                    IntPtr h = eu;
                    for (int i = 0; i < 400; i++)
                    {
                        h = GetWindow(h, 2); // GW_HWNDNEXT
                        if (h == IntPtr.Zero) break;
                        if (h != eu && IsWindowVisible(h) && GetWindowTextLength(h) > 0)
                        {
                            foco = SetForegroundWindow(h) ? ", foco devolvido" : ", foco nao devolvido";
                            break;
                        }
                    }
                }
                return $"janela movida para fora da tela: {moveu}{foco}";
            }
            catch (Exception e)
            {
                return "janela discreta falhou: " + e.Message;
            }
        }
#else
        public static string Esconder() => "janela discreta so no Windows";
#endif
    }

    internal sealed partial class RoteiroDeTeste : MonoBehaviour
    {
        private Principal _p;
        private string _pasta;
        private string _modo;
        private readonly List<string> _erros = new List<string>();
        private readonly StringBuilder _relatorio = new StringBuilder();
        private int _avisos, _problemas;
        private float _quadros, _tempo, _piorQuadro, _ultimaNota;

        private Nucleo.Partidas.Partida Partida => _p.Partida;

        public void Comecar(Principal p, string modo)
        {
            _p = p;
            _modo = modo;
            _pasta = Teste.Argumento("saida") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "capturas"));
            Directory.CreateDirectory(_pasta);
            Application.logMessageReceived += AoLogar;
            // Se o jogo fechar antes do fim, o relatorio diz quem fechou.
            Application.wantsToQuit += () =>
            {
                if (!_terminou) Linha($"[{_tempo,6:0.0}s] ! JOGO FECHANDO ANTES DO FIM. Pilha: {Environment.StackTrace}");
                return true;
            };
            File.WriteAllText(Arquivo, "");
            Linha($"Os Esquecidos — teste automatico ({modo}) {DateTime.Now:yyyy-MM-dd HH:mm}");
            Linha($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), tela {Screen.width}x{Screen.height}");
            StartCoroutine(Executar());
        }

        private IEnumerator Executar()
        {
            // A janela so existe de verdade depois do primeiro quadro.
            yield return null;
            yield return null;
            if (Teste.Argumento("janela") != "visivel") Anotar(JanelaDiscreta.Esconder());
            switch (_modo)
            {
                case "campanha": yield return Campanha(); break;
                case "fotos": yield return Fotos(); break;
                case "video": yield return Video(); break;
                default: yield return Passeio(); break;
            }
            Terminar();
        }

        private void AoLogar(string texto, string pilha, LogType tipo)
        {
            if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
            {
                if (_erros.Count < 60)
                {
                    _erros.Add(texto);
                    Linha($"[{_tempo,6:0.0}s] ERRO: {texto}\n{pilha}");
                }
            }
            else if (tipo == LogType.Warning)
            {
                _avisos++;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_tempo > 2)
            {
                _quadros++;
                _piorQuadro = Mathf.Max(_piorQuadro, dt);
            }
            _tempo += dt;
        }

        // ------------------------------------------------------------------ apoio

        private static IEnumerator Esperar(float segundos)
        {
            float fim = Time.unscaledTime + segundos;
            while (Time.unscaledTime < fim) yield return null;
        }

        private IEnumerator Capturar(string nome)
        {
            yield return new WaitForEndOfFrame();
            Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_pasta, nome + ".png"), t.EncodeToPNG());
            Destroy(t);
            Linha($"[{_tempo,6:0.0}s] foto {nome}");
        }

        /// <summary>Clica em todas as falas e escolhas ate a cena acabar.</summary>
        private IEnumerator PularCenas(float limite, int escolha = 0)
        {
            float fim = Time.unscaledTime + limite;
            while (Time.unscaledTime < fim)
            {
                if (!Partida.EmCena && !_p.UI.Dialogo.Aberto && !Partida.TransicaoPendente) yield break;
                _p.UI.Dialogo.PularParaTeste(escolha);
                if (_p.UI.Dialogo.MostrandoTopicos) _p.UI.Dialogo.Fechar();
                yield return Esperar(0.12f);
            }
            Problema("cena nao terminou no tempo limite");
        }

        private void ManterVivo()
        {
            Entidade j = Partida.Jogador;
            if (j != null && j.Vivo && j.Vida < j.VidaMax * 0.5f) j.Vida = j.VidaMax;
        }

        private string Arquivo => Path.Combine(_pasta, "relatorio.txt");

        /// <summary>Cada linha vai para o arquivo na hora: se o jogo fechar no meio, o relatorio fica.</summary>
        private void Linha(string texto)
        {
            _relatorio.AppendLine(texto);
            try
            {
                File.AppendAllText(Arquivo, texto + "\n");
            }
            catch (IOException)
            {
            }
        }

        private void Anotar(string texto) => Linha($"[{_tempo,6:0.0}s] {texto}");

        private void Problema(string texto)
        {
            _problemas++;
            Anotar("  ! " + texto);
        }

        /// <summary>Espera a troca de mapa pedida terminar e anota quanto ela custou.</summary>
        private IEnumerator EsperarMapa(string esperado, float limite = 8)
        {
            float fim = Time.unscaledTime + limite;
            while (Time.unscaledTime < fim && (Partida.TransicaoPendente || Partida.Mapa?.Id != esperado)) yield return null;
            if (Partida.Mapa?.Id != esperado)
            {
                Problema($"esperava o mapa {esperado} e esta em {Partida.Mapa?.Id}");
                yield break;
            }
            float pior = 0;
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                pior = Mathf.Max(pior, Time.unscaledDeltaTime);
            }
            Anotar($"entrou em {esperado}: {_p.TempoDaUltimaCarga}; pior quadro depois {pior * 1000:0} ms");
            yield return PularCenas(40);
        }

        private Entidade MaisProximo(Entidade j, bool chefe = false)
        {
            Entidade melhor = null;
            float d = float.MaxValue;
            foreach (Entidade e in Partida.Mundo.Entidades)
            {
                if (!e.Vivo || e.Faccao != Faccao.Inimigo || !e.Atacavel || e.Chefe != chefe || e.Tipo == "boneco") continue;
                float x = Vec2.Distance(e.Pos, j.Pos);
                if (x < d)
                {
                    d = x;
                    melhor = e;
                }
            }
            return melhor;
        }

        private bool _terminou;

        private void Terminar()
        {
            _terminou = true;
            float fps = _quadros / Mathf.Max(1, _tempo - 2);
            Anotar($"media {fps:0} qps, pior quadro {_piorQuadro * 1000:0} ms, {_avisos} avisos, {_erros.Count} erros, {_problemas} problemas do roteiro");
            Registro.Info("teste terminado: " + _pasta);
            Application.logMessageReceived -= AoLogar;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(_erros.Count + _problemas > 0 ? 1 : 0);
#endif
        }
    }
}
