using System;
using System.Collections.Generic;
using Esquecidos.Jogo.Atores;
using Esquecidos.Motor.Entrada;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>
    /// Caixa de conversa no estilo do Pal3: retrato de quem fala (o rosto
    /// 3D dele, filmado ao vivo), nome, texto aparecendo letra por letra,
    /// e as escolhas. As falas entram numa fila: dois roteiros falando ao
    /// mesmo tempo nao atropelam um ao outro.
    /// </summary>
    public sealed class Dialogo
    {
        public sealed class Fala
        {
            public string Quem;
            public string Texto;
            public bool Concluida;
        }

        private readonly Interface _ui;
        private readonly RectTransform _caixa;
        private readonly Text _nome, _texto, _continuar;
        private readonly RawImage _retrato;
        private readonly RectTransform _moldura;
        private readonly RectTransform _opcoes;
        private readonly Queue<Fala> _fila = new Queue<Fala>();
        private Fala _atual;
        private float _letras;
        private string _quemNoRetrato;
        private Action<int> _aoEscolher;
        private bool _fecharQuandoAcabar;
        private float _fechaEm;

        public bool Aberto => _caixa.gameObject.activeSelf;
        public bool PodeFechar => MostrandoTopicos;
        public bool MostrandoTopicos { get; private set; }

        public Dialogo(Interface ui, Transform pai)
        {
            _ui = ui;
            Image fundo = IU.Painel(pai, "dialogo", new Color(0.04f, 0.045f, 0.042f, 0.94f));
            _caixa = fundo.rectTransform.Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1180, 230));
            Image moldura = IU.Painel(_caixa, "moldura", new Color(0.08f, 0.09f, 0.1f, 1));
            _moldura = moldura.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(196, 196));
            _retrato = IU.Novo(_moldura, "retrato").gameObject.AddComponent<RawImage>();
            _retrato.rectTransform.Esticar(3);
            _retrato.texture = ui.Estudio.TexturaDoRetrato;
            _nome = IU.Texto(_caixa, "nome", "", 28, IU.Ambar);
            _nome.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(236, -16), new Vector2(900, 36));
            _texto = IU.Texto(_caixa, "texto", "", 25, IU.CorTexto);
            _texto.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(236, -58), new Vector2(910, 150));
            _continuar = IU.Texto(_caixa, "continuar", "▼", 20, IU.Ambar, TextAnchor.LowerRight);
            _continuar.rectTransform.Lugar(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 10), new Vector2(200, 30));
            _opcoes = IU.Novo(pai, "opcoes").Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(330, 395), new Vector2(520, 10));
            var clique = IU.Eventos(fundo.gameObject);
            clique.AoClicar = Avancar;
            _caixa.gameObject.SetActive(false);
        }

        public Fala Falar(string quem, string texto)
        {
            var f = new Fala { Quem = quem, Texto = texto };
            _fila.Enqueue(f);
            if (_atual == null) Proxima();
            return f;
        }

        private void Proxima()
        {
            _atual = _fila.Count > 0 ? _fila.Dequeue() : null;
            if (_atual == null) return;
            _fecharQuandoAcabar = false;
            Abrir();
            MostrandoTopicos = false;
            LimparOpcoes();
            (string nome, DadosDeHumano rosto) = Quem(_atual.Quem);
            _nome.text = nome;
            _nome.gameObject.SetActive(nome != null);
            bool temRosto = rosto != null;
            _moldura.gameObject.SetActive(temRosto);
            if (_quemNoRetrato != _atual.Quem)
            {
                _quemNoRetrato = _atual.Quem;
                _ui.Estudio.Retrato(rosto);
            }
            float x = temRosto ? 236 : 40;
            _nome.rectTransform.anchoredPosition = new Vector2(x, -16);
            _texto.rectTransform.anchoredPosition = new Vector2(x, nome != null ? -58 : -40);
            _texto.fontStyle = nome == null ? FontStyle.Italic : FontStyle.Normal;
            _letras = 0;
            _texto.text = "";
        }

        /// <summary>Nome para mostrar e rosto para o retrato de quem fala.</summary>
        private (string, DadosDeHumano) Quem(string quem)
        {
            if (quem == null) return (null, null);
            Catalogo c = _ui.P.Partida.Catalogo;
            if (quem == "jogador") return (_ui.P.Partida.Heroi.Nome, FabricaDeAtores.DadosDoJogador(_ui.P.Partida));
            if (c.Npcs.TryGetValue(quem, out NpcDef npc)) return (npc.Nome, FabricaDeAtores.DadosDoNpc(c, quem));
            return (quem.Replace('_', ' '), null);
        }

        private void Abrir()
        {
            _caixa.gameObject.SetActive(true);
        }

        public void Fechar(bool forcado = false)
        {
            if (forcado)
            {
                _fila.Clear();
                if (_atual != null) _atual.Concluida = true;
            }
            _atual = null;
            MostrandoTopicos = false;
            LimparOpcoes();
            _caixa.gameObject.SetActive(false);
            _quemNoRetrato = null;
            _ui.Estudio.Retrato(null);
        }

        public void FecharQuandoAcabar()
        {
            _fecharQuandoAcabar = true;
            _fechaEm = 0.15f;
        }

        private void Avancar()
        {
            if (_atual == null) return;
            if (_letras < _atual.Texto.Length)
            {
                _letras = _atual.Texto.Length;
                return;
            }
            _atual.Concluida = true;
            _ui.P.Sons.UI("ui_clique");
            Proxima();
            if (_atual == null && _aoEscolher == null) FecharQuandoAcabar();
        }

        public void Atualizar(float dt)
        {
            if (_atual != null)
            {
                int antes = (int)_letras;
                _letras = Mathf.Min(_atual.Texto.Length, _letras + dt * 60);
                int agora = (int)_letras;
                if (agora != antes)
                {
                    _texto.text = _atual.Texto.Substring(0, agora);
                    if (agora % 3 == 0 && agora < _atual.Texto.Length && _atual.Quem != null) _ui.P.Sons.UI("fala");
                }
                _continuar.enabled = agora >= _atual.Texto.Length && Time.unscaledTime % 1 < 0.6f;
                if (_aoEscolher == null && (Controles.Desceu(Key.Space) || Controles.Desceu(Key.Enter) || Controles.Desceu(Key.F) || (Controles.Controle?.buttonSouth.wasPressedThisFrame ?? false)))
                    Avancar();
            }
            else
            {
                _continuar.enabled = false;
            }
            if (_fecharQuandoAcabar && _atual == null && _aoEscolher == null && !MostrandoTopicos)
            {
                _fechaEm -= dt;
                if (_fechaEm <= 0)
                {
                    _fecharQuandoAcabar = false;
                    if (!_ui.P.Partida.EmCena || _fila.Count == 0) Fechar();
                }
            }
            // Atalhos 1..9 para as escolhas.
            if (_aoEscolher != null)
            {
                Key[] teclas = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
                for (int i = 0; i < teclas.Length && i < _botoes.Count; i++)
                    if (Controles.Desceu(teclas[i])) _botoes[i].onClick.Invoke();
            }
        }

        /// <summary>Modo de teste: termina a fala da tela ou clica na primeira escolha.</summary>
        public void PularParaTeste()
        {
            if (_botoes.Count > 0 && !MostrandoTopicos)
            {
                _botoes[0].onClick.Invoke();
                return;
            }
            if (_atual != null) _letras = _atual.Texto.Length;
            Avancar();
        }

        // ------------------------------------------------------------------ escolhas

        private readonly List<Button> _botoes = new List<Button>();

        private void LimparOpcoes()
        {
            foreach (Button b in _botoes) UnityEngine.Object.Destroy(b.gameObject);
            _botoes.Clear();
            _aoEscolher = null;
        }

        /// <summary>
        /// As escolhas aparecem por cima da ultima fala. Se a fala ainda
        /// estiver na tela, ela fica; a escolha encerra a fala junto.
        /// </summary>
        public void Escolhas(string[] opcoes, Action<int> escolher)
        {
            Abrir();
            LimparOpcoes();
            // O texto da ultima fala fica: e o contexto da pergunta.
            _continuar.enabled = false;
            _aoEscolher = escolher;
            for (int i = 0; i < opcoes.Length; i++)
            {
                int indice = i;
                Button b = IU.Botao(_opcoes, "opcao", $"{i + 1}. {opcoes[i]}", () =>
                {
                    Action<int> a = _aoEscolher;
                    if (_atual != null) _atual.Concluida = true;
                    _atual = null;
                    LimparOpcoes();
                    a?.Invoke(indice);
                }, 21);
                RectTransform r = b.GetComponent<RectTransform>();
                r.Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, (opcoes.Length - 1 - i) * 52), new Vector2(560, 46));
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(16, 4);
                _botoes.Add(b);
            }
        }

        /// <summary>Os assuntos de um NPC: um botao por topico e um para ir embora.</summary>
        public void Topicos(Entidade npc, List<DialogoDef> topicos)
        {
            _fila.Clear();
            _atual = null;
            Abrir();
            MostrandoTopicos = true;
            (string nome, DadosDeHumano rosto) = Quem(npc.NpcId);
            _nome.text = nome;
            _nome.gameObject.SetActive(true);
            _moldura.gameObject.SetActive(rosto != null);
            if (_quemNoRetrato != npc.NpcId)
            {
                _quemNoRetrato = npc.NpcId;
                _ui.Estudio.Retrato(rosto);
            }
            _nome.rectTransform.anchoredPosition = new Vector2(236, -16);
            _texto.rectTransform.anchoredPosition = new Vector2(236, -58);
            _texto.fontStyle = FontStyle.Italic;
            NpcDef def = _ui.P.Partida.Catalogo.Npc(npc.NpcId);
            _texto.text = $"{def.Titulo}. Sobre o que você quer falar?";
            var nomes = new List<string>();
            foreach (DialogoDef t in topicos) nomes.Add(t.Topico);
            nomes.Add("Até mais");
            LimparOpcoes();
            _aoEscolher = i =>
            {
                MostrandoTopicos = false;
                if (i < topicos.Count) _ui.P.Partida.EscolherTopico(topicos[i]);
                else Fechar();
            };
            Action<int> escolha = _aoEscolher;
            for (int i = 0; i < nomes.Count; i++)
            {
                int indice = i;
                Button b = IU.Botao(_opcoes, "topico", $"{i + 1}. {nomes[i]}", () =>
                {
                    LimparOpcoes();
                    escolha(indice);
                }, 21);
                b.GetComponent<RectTransform>().Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, (nomes.Count - 1 - i) * 52), new Vector2(560, 46));
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(16, 4);
                _botoes.Add(b);
            }
            _aoEscolher = escolha;
        }
    }

    /// <summary>Caixa de dica do tutorial, no alto da tela; some sozinha.</summary>
    public sealed class CaixaDeDica
    {
        private readonly RectTransform _caixa;
        private readonly Text _texto;
        private float _resta;

        public CaixaDeDica(Interface ui, Transform pai)
        {
            Image fundo = IU.Painel(pai, "dica", new Color(0.08f, 0.07f, 0.04f, 0.92f));
            _caixa = fundo.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 96));
            Text titulo = IU.Texto(_caixa, "titulo", "DICA", 16, IU.Ambar);
            titulo.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -10), new Vector2(200, 22));
            _texto = IU.Texto(_caixa, "texto", "", 20, IU.CorTexto);
            _texto.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -34), new Vector2(864, 60));
            IU.Eventos(fundo.gameObject).AoClicar = () => _resta = 0;
            _caixa.gameObject.SetActive(false);
        }

        public void Mostrar(string texto)
        {
            _texto.text = texto;
            _resta = 14;
            _caixa.gameObject.SetActive(true);
            float altura = Mathf.Max(96, _texto.preferredHeight + 48);
            _caixa.sizeDelta = new Vector2(900, altura);
        }

        public void Atualizar(float dt)
        {
            if (!_caixa.gameObject.activeSelf) return;
            _resta -= dt;
            if (_resta <= 0) _caixa.gameObject.SetActive(false);
        }
    }
}
