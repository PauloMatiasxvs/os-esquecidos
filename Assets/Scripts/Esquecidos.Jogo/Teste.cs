using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Esquecidos.Motor;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using Vec2 = System.Numerics.Vector2;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Modo de teste do executavel: "Os Esquecidos.exe --captura" joga um
    /// roteiro fixo (menu, criacao, abertura, Patio, setor, combate, noite,
    /// laboratorio), tira fotos da tela e escreve um relatorio com os erros
    /// do log. Serve para conferir o jogo inteiro sem ninguem no teclado.
    /// </summary>
    public static class Teste
    {
        public static bool Pedido(out string modo)
        {
            modo = null;
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (a == "--captura" || a == "--autoteste") modo = a.Substring(2);
            }
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
            var modulo = UnityEngine.EventSystems.EventSystem.current?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (modulo != null) modulo.enabled = false;
            var r = p.gameObject.AddComponent<RoteiroDeTeste>();
            r.Comecar(p, modo);
        }
    }

    internal sealed class RoteiroDeTeste : MonoBehaviour
    {
        private Principal _p;
        private string _pasta;
        private readonly List<string> _erros = new List<string>();
        private readonly StringBuilder _relatorio = new StringBuilder();
        private int _avisos;
        private float _quadros, _tempo, _piorQuadro, _ultimaNota;

        public void Comecar(Principal p, string modo)
        {
            _p = p;
            _pasta = Teste.Argumento("saida") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "capturas"));
            Directory.CreateDirectory(_pasta);
            Application.logMessageReceived += AoLogar;
            _relatorio.AppendLine($"Os Esquecidos — teste automatico ({modo}) {DateTime.Now:yyyy-MM-dd HH:mm}");
            _relatorio.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), tela {Screen.width}x{Screen.height}");
            var dispositivos = new List<string>();
            foreach (UnityEngine.InputSystem.InputDevice d in UnityEngine.InputSystem.InputSystem.devices) dispositivos.Add($"{d.displayName} ({d.GetType().Name})");
            _relatorio.AppendLine("Dispositivos: " + string.Join(", ", dispositivos));
            StartCoroutine(Rodar());
        }

        private void AoLogar(string texto, string pilha, LogType tipo)
        {
            if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
            {
                if (_erros.Count < 60) _erros.Add(texto + "\n" + pilha);
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
            _relatorio.AppendLine($"[{_tempo,6:0.0}s] foto {nome}");
        }

        /// <summary>Clica em todas as falas e escolhas ate a cena acabar.</summary>
        private IEnumerator PularCenas(float limite)
        {
            float fim = Time.unscaledTime + limite;
            while (Time.unscaledTime < fim)
            {
                if (!_p.Partida.EmCena && !_p.UI.Dialogo.Aberto) yield break;
                _p.UI.Dialogo.PularParaTeste();
                if (_p.UI.Dialogo.MostrandoTopicos) _p.UI.Dialogo.Fechar();
                yield return Esperar(0.12f);
            }
            _relatorio.AppendLine("  ! cena nao terminou no tempo limite");
        }

        private void ManterVivo()
        {
            Entidade j = _p.Partida.Jogador;
            if (j != null && j.Vivo && j.Vida < j.VidaMax * 0.5f) j.Vida = j.VidaMax;
        }

        private void Anotar(string texto) => _relatorio.AppendLine($"[{_tempo,6:0.0}s] {texto}");

        private IEnumerator Rodar()
        {
            _p.AbrirMenu();
            yield return Esperar(4);
            yield return Capturar("01_menu");

            _p.UI.Menus.Criacao();
            yield return Esperar(2.5f);
            yield return Capturar("02_criacao");
            _p.UI.Menus.EsconderTudo();

            _p.ComecarNovoJogo("corredor", "Teste", null);
            yield return Esperar(5);
            yield return Capturar("03_abertura");
            yield return PularCenas(90);
            Anotar($"abertura terminou; mapa {_p.Partida.Mapa?.Id}, hora {_p.Partida.Ciclo.Hora:0.0}");
            yield return Esperar(2);
            yield return Capturar("04_patio");

            _p.Partida.MoverPara(new Vec2(46, 44));
            yield return Esperar(5);
            yield return Capturar("05_patio_andando");

            Entidade iara = null;
            foreach (Entidade e in _p.Partida.Mundo.Entidades)
                if (e.NpcId == "iara") iara = e;
            if (iara != null)
            {
                _p.Partida.FalarCom(iara.Id);
                float limite = Time.unscaledTime + 12;
                while (!_p.UI.Dialogo.Aberto && Time.unscaledTime < limite) yield return null;
                yield return Esperar(2.5f);
                yield return Capturar("05b_conversa");
                yield return PularCenas(60);
                if (_p.UI.Dialogo.Aberto) _p.UI.Dialogo.Fechar(true);
                Anotar($"conversa com iara; missao m01 = {_p.Partida.Diario.Etapa("m01_primeiro_dia")}");
            }
            else Anotar("  ! iara nao encontrada no Patio");

            _p.UI.Janelas.Abrir("inventario");
            yield return Esperar(1);
            yield return Capturar("06_inventario");
            foreach (string id in new[] { "camisa_rasgada", "faca_cozinha", "tenis_gasto" })
            {
                Texture2D icone = _p.UI.Estudio.Icone(_p.Catalogo.Item(id)).texture;
                File.WriteAllBytes(Path.Combine(_pasta, "icone_" + id + ".png"), icone.EncodeToPNG());
                Color c = icone.GetPixel(64, 64);
                Anotar($"icone {id}: centro {ColorUtility.ToHtmlStringRGB(c)}");
            }
            _p.UI.Janelas.FecharTodas();
            _p.UI.Janelas.Abrir("personagem");
            yield return Esperar(1);
            yield return Capturar("07_personagem");
            _p.UI.Janelas.FecharTodas();
            _p.UI.Janelas.Abrir("poderes");
            yield return Esperar(1);
            Anotar("janelas: " + _p.UI.Janelas.Depurar());
            yield return Capturar("08_poderes");
            _p.UI.Janelas.FecharTodas();

            _p.Partida.Ciclo.Definir(_p.Partida.Ciclo.Dia, 9.5f);
            _p.Partida.IrPara("setor1", "patio");
            yield return Esperar(3);
            yield return PularCenas(30);
            Anotar($"entrou em {_p.Partida.Mapa?.Id}; {_p.Partida.Mundo.Entidades.Count} entidades");
            yield return Capturar("09_setor1");

            yield return Combater(25);

            _p.UI.Janelas.Abrir("mapa");
            yield return Esperar(1);
            yield return Capturar("12_mapa");
            _p.UI.Janelas.FecharTodas();

            _p.Partida.Ciclo.Definir(_p.Partida.Ciclo.Dia, 21.5f);
            yield return Esperar(4);
            ManterVivo();
            yield return Capturar("13_setor1_noite");

            _p.Partida.IrPara("patio", "inicio");
            yield return Esperar(3);
            yield return PularCenas(30);
            yield return Esperar(2);
            yield return Capturar("14_patio_noite");

            _p.Partida.Ciclo.Definir(_p.Partida.Ciclo.Dia + 1, 11f);
            _p.Partida.IrPara("laboratorio", "patio");
            yield return Esperar(3);
            yield return PularCenas(30);
            Anotar($"entrou em {_p.Partida.Mapa?.Id}");
            yield return Esperar(1);
            yield return Capturar("15_laboratorio");

            Entidade j = _p.Partida.Jogador;
            _p.Partida.Mundo.Matar(j, null);
            yield return Esperar(3.5f);
            yield return Capturar("15b_morte");
            Anotar($"morte: estado {_p.Estado}");
            _p.UI.Menus.EsconderTudo();
            _p.Renascer();
            yield return Esperar(3);
            yield return PularCenas(30);
            yield return Capturar("15c_renascer");
            Anotar($"renasceu em {_p.Partida.Mapa?.Id}, vida {_p.Partida.Jogador.Vida:0}");

            _p.UI.Menus.Pausar();
            yield return Esperar(1);
            yield return Capturar("16_pausa");
            _p.UI.Menus.Despausar();

            Terminar();
        }

        /// <summary>Anda ate o bicho mais perto e bate; fotografa no meio da briga.</summary>
        private IEnumerator Combater(float segundos)
        {
            float inicio = Time.unscaledTime;
            bool fotografou = false;
            int mortos = 0;
            int alvo = 0;
            while (Time.unscaledTime - inicio < segundos)
            {
                ManterVivo();
                Entidade j = _p.Partida.Jogador;
                Entidade e = _p.Partida.Mundo.Buscar(alvo);
                if (e == null || !e.Vivo)
                {
                    if (alvo != 0) mortos++;
                    e = MaisProximo(j);
                    alvo = e?.Id ?? 0;
                    if (e != null) _p.Partida.Atacar(e.Id, true);
                }
                if (e == null) break;
                if (Time.unscaledTime - _ultimaNota > 2)
                {
                    _ultimaNota = Time.unscaledTime;
                    Anotar($"  alvo {e.Tipo}#{e.Id} vida {e.Vida:0}/{e.VidaMax:0} dist {Vec2.Distance(e.Pos, j.Pos):0.0}; jogador ordem={j.Ordem.Tipo} acao={j.Acao} vel={j.VelocidadeAtual:0.0}");
                }
                if (!fotografou && Vec2.Distance(e.Pos, j.Pos) < 4f)
                {
                    yield return Esperar(0.8f);
                    yield return Capturar("10_combate");
                    fotografou = true;
                }
                yield return Esperar(0.25f);
            }
            if (!fotografou) yield return Capturar("10_combate");
            yield return Esperar(0.5f);
            yield return Capturar("11_depois_do_combate");
            Anotar($"combate: {mortos} inimigos derrotados; vida {_p.Partida.Jogador.Vida:0}/{_p.Partida.Jogador.VidaMax:0}; nivel {_p.Partida.Heroi.Nivel}, xp {_p.Partida.Heroi.Xp}");
        }

        private Entidade MaisProximo(Entidade j)
        {
            Entidade melhor = null;
            float d = float.MaxValue;
            foreach (Entidade e in _p.Partida.Mundo.Entidades)
            {
                if (!e.Vivo || e.Faccao != Faccao.Inimigo || !e.Atacavel || e.Chefe) continue;
                float x = Vec2.Distance(e.Pos, j.Pos);
                if (x < d)
                {
                    d = x;
                    melhor = e;
                }
            }
            return melhor;
        }

        private void Terminar()
        {
            float fps = _quadros / Mathf.Max(1, _tempo - 2);
            Anotar($"media {fps:0} qps, pior quadro {_piorQuadro * 1000:0} ms, {_avisos} avisos, {_erros.Count} erros");
            foreach (string e in _erros) _relatorio.AppendLine("ERRO: " + e);
            File.WriteAllText(Path.Combine(_pasta, "relatorio.txt"), _relatorio.ToString());
            Registro.Info("teste terminado: " + _pasta);
            Application.logMessageReceived -= AoLogar;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
