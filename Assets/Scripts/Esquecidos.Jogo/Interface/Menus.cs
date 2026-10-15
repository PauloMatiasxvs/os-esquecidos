using System.Collections.Generic;
using System.Linq;
using Esquecidos.Jogo.Atores;
using Esquecidos.Motor.Entrada;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>Menu principal, criacao de personagem, espacos de salvamento, pausa, opcoes, morte e final.</summary>
    public sealed class Menus
    {
        private readonly Interface _ui;
        private readonly RectTransform _raiz;
        private RectTransform _atual;
        private Humanoide _previa;
        private string _classe = "corredor";
        private string _nome = "";
        private int _pele = 2, _cabelo = 1, _penteado;
        private int _espacoNovo;
        private float _creditosT;
        private RectTransform _rolagem;
        private string _abaOpcoes = "som";
        private System.Action _voltarDasOpcoes;
        private Acao? _esperandoTecla;

        public bool Pausado { get; private set; }

        /// <summary>Esperando o jogador apertar a tecla nova: o Esc cancela isso, nao a pausa.</summary>
        public bool CapturandoTecla => _esperandoTecla.HasValue;

        private static readonly string[] Peles = { "#f1d2b6", "#e2b48f", "#c99470", "#a8704a", "#7f5235", "#5a3a26" };
        private static readonly string[] Cabelos = { "#1a1410", "#3a2414", "#6b4424", "#a8743a", "#d8c890", "#8a2a18", "#6a6a6a" };
        private static readonly string[] Penteados = { "curto", "raspado", "longo", "tranca", "coque", "rabo", "cacheado", "baguncado", "careca" };
        private static readonly string[] NomesDePenteado = { "curto", "raspado", "longo", "trança", "coque", "rabo", "cacheado", "bagunçado", "careca" };
        private static readonly float[] Escalas = { 0.9f, 1f, 1.15f, 1.3f };

        public Menus(Interface ui, Transform pai)
        {
            _ui = ui;
            _raiz = IU.Novo(pai, "menus");
            _raiz.Esticar();
        }

        private Principal P => _ui.P;

        private RectTransform Tela(string nome, bool escurecer = true)
        {
            EsconderTudo();
            RectTransform r = IU.Novo(_raiz, nome);
            r.Esticar();
            if (escurecer)
            {
                Image f = IU.Imagem(r, "fundo", new Color(0, 0, 0, 0.55f));
                f.rectTransform.Esticar();
            }
            _atual = r;
            return r;
        }

        public void EsconderTudo()
        {
            if (_atual != null) Object.Destroy(_atual.gameObject);
            _atual = null;
            _rolagem = null;
            if (_previa != null) Object.Destroy(_previa.gameObject);
            _previa = null;
        }

        private static Button Botao(Transform pai, string texto, System.Action acao, float y, float largura = 380)
        {
            Button b = IU.Botao(pai, texto, texto, acao, 26);
            b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(largura, 54));
            return b;
        }

        /// <summary>Botao pequeno de escolha numa fileira; o escolhido fica ambar.</summary>
        private static Button Opcao(Transform pai, string rotulo, bool escolhido, System.Action acao, Vector2 pos, float largura)
        {
            Button b = IU.Botao(pai, rotulo, rotulo, acao, 20);
            b.GetComponent<RectTransform>().Lugar(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(largura, 42));
            if (escolhido) b.GetComponent<Image>().color = IU.Ambar;
            return b;
        }

        private static Text Rotulo(Transform pai, string texto, Vector2 pos, float largura = 260, int tamanho = 22)
        {
            Text t = IU.Texto(pai, "rotulo", texto, tamanho, IU.CorTexto);
            t.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(largura, 40));
            return t;
        }

        // ------------------------------------------------------------------ principal

        public void Principal()
        {
            Pausado = false;
            RectTransform t = Tela("principal", false);
            Image faixa = IU.Imagem(t, "faixa", new Color(0, 0, 0, 0.45f));
            faixa.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(620, 2000));
            Text titulo = IU.Texto(t, "titulo", T("OS ESQUECIDOS"), 86, new Color(0.95f, 0.9f, 0.8f), TextAnchor.MiddleLeft);
            titulo.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 290), new Vector2(900, 110));
            titulo.fontStyle = FontStyle.Bold;
            Text sub = IU.Texto(t, "sub", T("Ninguém lembra de como chegou. Ninguém nunca saiu."), 24, IU.Ambar, TextAnchor.MiddleLeft);
            sub.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(74, 220), new Vector2(900, 40));
            RectTransform botoes = IU.Novo(t, "botoes").Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -60), new Vector2(400, 480));
            float y = 170;
            Button cont = Botao(botoes, T("Continuar"), () => P.Continuar(), y);
            IU.Ativo(cont, P.TemSalvo);
            Button carregar = Botao(botoes, T("Carregar"), () => Espacos(Principal), y -= 66);
            IU.Ativo(carregar, P.TemSalvo);
            Botao(botoes, T("Novo jogo"), NovoJogo, y -= 66);
            Botao(botoes, T("Opções"), () => Opcoes(Principal), y -= 66);
            Botao(botoes, T("Controles"), () => ControlesTela(Principal), y -= 66);
            Botao(botoes, T("Sair"), Application.Quit, y -= 66);
            Text rodape = IU.Texto(t, "rodape", T("Inspirado em Flare e Pal3.Unity. Tudo no jogo — modelos, música, sons — é gerado por código."), 16, IU.CorFraca, TextAnchor.LowerLeft);
            rodape.rectTransform.Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(70, 24), new Vector2(1200, 30));
        }

        private void NovoJogo()
        {
            // Novo jogo vai para o primeiro espaco vazio; se todos estao ocupados, para o mais antigo.
            _espacoNovo = 0;
            for (int i = 1; i <= Jogo.Principal.Espacos && _espacoNovo == 0; i++)
                if (!Jogo.Principal.EspacoUsado(i)) _espacoNovo = i;
            if (_espacoNovo == 0)
            {
                System.DateTime antigo = System.DateTime.MaxValue;
                for (int i = 1; i <= Jogo.Principal.Espacos; i++)
                {
                    System.DateTime d = System.IO.File.GetLastWriteTime(Jogo.Principal.CaminhoDoEspaco(i));
                    if (d < antigo)
                    {
                        antigo = d;
                        _espacoNovo = i;
                    }
                }
            }
            Criacao();
        }

        private string NomeDaTecla(Acao a) => Controles.Nome(a);

        private void ControlesTela(System.Action voltar)
        {
            RectTransform t = Tela("controles");
            Image p = IU.Painel(t, "painel");
            p.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 780));
            string mover = $"{NomeDaTecla(Acao.Cima)} {NomeDaTecla(Acao.Esquerda)} {NomeDaTecla(Acao.Baixo)} {NomeDaTecla(Acao.Direita)}";
            string barra = $"{NomeDaTecla(Acao.Barra1)} … {NomeDaTecla(Acao.Barra6)}";
            string texto =
                T("<b>Clique esquerdo</b> — andar; em inimigo, atacar (segure para continuar); em gente, conversar; em item, pegar") + "\n" +
                F("<b>{0}</b> — andar direto", mover) + "\n" +
                F("<b>{0} + clique</b> — atacar parado na direção do cursor", NomeDaTecla(Acao.AtacarParado)) + "\n" +
                T("<b>Clique direito</b> — poder do segundo espaço da barra") + "\n" +
                F("<b>{0}</b> — poderes e itens da barra", barra) + "\n" +
                F("<b>{0}</b> — rolamento (nada te acerta no meio dele)", NomeDaTecla(Acao.Rolar)) + "\n" +
                F("<b>{0}</b> — interagir com o que estiver perto", NomeDaTecla(Acao.Interagir)) + "\n" +
                F("<b>{0} / {1}</b> — girar a câmera    <b>roda</b> — aproximar", NomeDaTecla(Acao.GirarEsquerda), NomeDaTecla(Acao.GirarDireita)) + "\n" +
                F("<b>{0}</b> — mostrar nomes dos itens no chão", NomeDaTecla(Acao.MostrarItens)) + "\n" +
                F("<b>{0}</b> mochila   <b>{1}</b> personagem   <b>{2}</b> poderes   <b>{3}</b> diário   <b>{4}</b> mapa",
                    NomeDaTecla(Acao.Mochila), NomeDaTecla(Acao.Personagem), NomeDaTecla(Acao.Poderes), NomeDaTecla(Acao.Diario), NomeDaTecla(Acao.Mapa)) + "\n" +
                F("<b>{0}</b> salvar   <b>{1}</b> carregar   <b>Esc</b> pausa", NomeDaTecla(Acao.Salvar), NomeDaTecla(Acao.Carregar)) + "\n\n" +
                T("<b>Controle</b>: analógico anda, A ataca o mais perto, B rola, X e gatilhos usam a barra, Y interage, LB/RB giram a câmera.") + "\n\n" +
                T("As teclas podem ser trocadas em Opções → Teclas.") + "\n\n" +
                IU.Cor(T("Os portões do Pátio fecham às 19h30. Quem fica lá fora passa a noite com eles."), "#ffb040");
            Text tx = IU.Texto(p.transform, "texto", texto, 21, IU.CorTexto);
            tx.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(840, 650));
            Button b = IU.Botao(p.transform, "voltar", T("Voltar"), voltar, 24);
            b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(240, 50));
        }

        // ------------------------------------------------------------------ espacos de salvamento

        /// <summary>Os tres espacos: clicar num usado carrega o jogo dele.</summary>
        public void Espacos(System.Action voltar)
        {
            RectTransform t = Tela("espacos");
            Image p = IU.Painel(t, "painel");
            p.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 560));
            Text titulo = IU.Texto(p.transform, "titulo", T("Carregar"), 36, IU.Ambar, TextAnchor.UpperCenter);
            titulo.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(700, 50));
            for (int i = 1; i <= Jogo.Principal.Espacos; i++)
            {
                int espaco = i;
                string resumo = P.ResumoDoEspaco(i);
                Image card = IU.Painel(p.transform, "espaco", resumo != null ? new Color(0.1f, 0.09f, 0.07f, 1) : new Color(0.05f, 0.05f, 0.05f, 1));
                card.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90 - (i - 1) * 120), new Vector2(780, 104));
                string linha = resumo != null ? resumo : IU.Cor(T("vazio"), "#6a6258");
                Text tx = IU.Texto(card.transform, "t", $"<size=26><b>{F("Espaço {0}", i)}</b></size>\n{linha}", 21, IU.CorTexto);
                tx.rectTransform.Esticar(16);
                if (resumo != null)
                {
                    IU.Eventos(card.gameObject).AoClicar = () =>
                    {
                        Pausado = false;
                        EsconderTudo();
                        P.CarregarEspaco(espaco);
                    };
                }
            }
            Button b = IU.Botao(p.transform, "voltar", T("Voltar"), voltar, 24);
            b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(240, 50));
        }

        // ------------------------------------------------------------------ criacao

        public void Criacao()
        {
            if (_espacoNovo == 0) _espacoNovo = 1;
            RectTransform t = Tela("criacao", false);
            Catalogo c = P.Catalogo;
            Image painel = IU.Painel(t, "painel");
            painel.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(720, 1040));
            Text titulo = IU.Texto(painel.transform, "titulo", T("Quem saiu da caixa?"), 36, IU.Ambar);
            titulo.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -22), new Vector2(660, 48));

            Text lnome = IU.Texto(painel.transform, "lnome", T("A única coisa que você lembra é o seu nome:"), 20, IU.CorFraca);
            lnome.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -82), new Vector2(660, 28));
            Image campo = IU.Painel(painel.transform, "campo", new Color(0.02f, 0.02f, 0.02f, 1));
            campo.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -114), new Vector2(660, 52));
            Text texto = IU.Texto(campo.transform, "texto", "", 28, IU.CorTexto, TextAnchor.MiddleLeft);
            texto.rectTransform.Esticar(12);
            Text dica = IU.Texto(campo.transform, "dica", T("digite um nome"), 28, new Color(0.4f, 0.38f, 0.35f), TextAnchor.MiddleLeft);
            dica.rectTransform.Esticar(12);
            var input = campo.gameObject.AddComponent<InputField>();
            input.textComponent = texto;
            input.placeholder = dica;
            input.characterLimit = 18;
            input.text = _nome;
            input.onValueChanged.AddListener(v => _nome = v);

            float y = -190;
            Text lclasse = IU.Texto(painel.transform, "lclasse", T("O que você faz no Pátio:"), 20, IU.CorFraca);
            lclasse.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(660, 28));
            y -= 36;
            foreach (string id in c.OrdemDasClasses)
            {
                Classe cl = c.Classes[id];
                string cid = id;
                Image card = IU.Painel(painel.transform, "classe", id == _classe ? new Color(0.18f, 0.13f, 0.06f, 1) : new Color(0.07f, 0.07f, 0.06f, 1));
                card.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(660, 128));
                if (id == _classe) card.color = IU.Ambar;
                string attrs = F("Físico {0} · Mente {1} · Ofensiva {2} · Defesa {3}", cl.Atributos[0], cl.Atributos[1], cl.Atributos[2], cl.Atributos[3]);
                Text ct = IU.Texto(card.transform, "t", $"<size=26><b>{cl.Nome}</b></size>  {IU.Cor(cl.Papel, "#e0c890")}\n<size=17>{cl.Descricao}</size>\n{IU.Cor($"<size=15>{attrs}</size>", "#9fd0ff")}", 20, IU.CorTexto);
                ct.rectTransform.Esticar(14);
                IU.Eventos(card.gameObject).AoClicar = () =>
                {
                    _classe = cid;
                    P.Sons.UI("ui_clique");
                    Criacao();
                };
                y -= 138;
            }

            y -= 6;
            Text lpele = IU.Texto(painel.transform, "lpele", T("Pele"), 20, IU.CorFraca);
            lpele.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(200, 28));
            Amostras(painel.transform, Peles, _pele, y - 30, i =>
            {
                _pele = i;
                Criacao();
            });
            y -= 88;
            Text lcab = IU.Texto(painel.transform, "lcab", T("Cabelo"), 20, IU.CorFraca);
            lcab.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(200, 28));
            Amostras(painel.transform, Cabelos, _cabelo, y - 30, i =>
            {
                _cabelo = i;
                Criacao();
            });
            y -= 88;
            Text lpent = IU.Texto(painel.transform, "lpent", F("Penteado: {0}", IU.Cor(T(NomesDePenteado[_penteado]), "#e8e2d0")), 20, IU.CorFraca);
            lpent.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(400, 28));
            Button ant = IU.Botao(painel.transform, "ant", "◀", () =>
            {
                _penteado = (_penteado + Penteados.Length - 1) % Penteados.Length;
                Criacao();
            }, 22);
            ant.GetComponent<RectTransform>().Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(440, y + 6), new Vector2(50, 40));
            Button prox = IU.Botao(painel.transform, "prox", "▶", () =>
            {
                _penteado = (_penteado + 1) % Penteados.Length;
                Criacao();
            }, 22);
            prox.GetComponent<RectTransform>().Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(500, y + 6), new Vector2(50, 40));

            // Em qual dos tres espacos o jogo vai ser salvo.
            y -= 56;
            Text lesp = IU.Texto(painel.transform, "lesp", T("Salvar no espaço:"), 20, IU.CorFraca);
            lesp.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(240, 28));
            for (int i = 1; i <= Jogo.Principal.Espacos; i++)
            {
                int espaco = i;
                string rotulo = Jogo.Principal.EspacoUsado(i) ? $"{i} •" : $"{i}";
                Opcao(painel.transform, rotulo, _espacoNovo == i, () =>
                {
                    _espacoNovo = espaco;
                    Criacao();
                }, new Vector2(250 + (i - 1) * 76, y + 8), 66);
            }
            if (Jogo.Principal.EspacoUsado(_espacoNovo))
            {
                Text aviso = IU.Texto(painel.transform, "aviso", IU.Cor(F("Vai substituir: {0}", P.ResumoDoEspaco(_espacoNovo)), "#ff9a4a"), 16, IU.CorTexto);
                aviso.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y - 34), new Vector2(660, 44));
            }

            Button voltar = IU.Botao(painel.transform, "voltar", T("Voltar"), Principal, 24);
            voltar.GetComponent<RectTransform>().Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 26), new Vector2(200, 56));
            Button comecar = IU.Botao(painel.transform, "comecar", T("Acordar"), () =>
            {
                string nome = string.IsNullOrWhiteSpace(_nome) ? T("Novato") : _nome.Trim();
                Aparencia ap = Aparencia();
                EsconderTudo();
                P.ComecarNovoJogo(_classe, nome, ap, _espacoNovo);
            }, 28);
            comecar.GetComponent<RectTransform>().Lugar(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 26), new Vector2(300, 56));
            comecar.GetComponent<Image>().color = IU.Ambar;
            Previa();
        }

        private Aparencia Aparencia()
        {
            Classe cl = P.Catalogo.Classe(_classe);
            return new Aparencia
            {
                Pele = Peles[_pele],
                Cabelo = Cabelos[_cabelo],
                Penteado = Penteados[_penteado],
                Roupa = cl.CorRoupa,
                Calca = "#38383a",
                Botas = "#2a2420",
            };
        }

        private void Amostras(Transform pai, string[] cores, int escolhida, float y, System.Action<int> escolher)
        {
            for (int i = 0; i < cores.Length; i++)
            {
                int indice = i;
                Image a = IU.Painel(pai, "amostra", Util.Cor(cores[i]));
                a.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30 + i * 62, y), new Vector2(52, 44));
                if (i == escolhida) a.color = IU.Ambar;
                else a.color = new Color(0.2f, 0.2f, 0.2f);
                IU.Eventos(a.gameObject).AoClicar = () =>
                {
                    P.Sons.UI("ui_clique");
                    escolher(indice);
                };
            }
        }

        /// <summary>O boneco de previa fica no Patio, ao lado do monta-cargas, com a camera perto.</summary>
        private void Previa()
        {
            if (_previa != null) Object.Destroy(_previa.gameObject);
            var e = new Entidade { Id = -77, Tipo = "jogador", Nome = "previa", Pos = new System.Numerics.Vector2(52.5f, 49.5f), Direcao = -Mathf.PI / 2 - 0.5f, Faccao = Faccao.Neutro };
            e.PosAnterior = e.Pos;
            var g = new GameObject("previa");
            g.transform.SetParent(P.transform, false);
            _previa = g.AddComponent<Humanoide>();
            _previa.Dados = new DadosDeHumano { Ap = Aparencia(), Tipo = "jogador" };
            Classe cl = P.Catalogo.Classe(_classe);
            foreach (string id in cl.Equipamento)
            {
                Nucleo.Itens.ItemBase b = P.Catalogo.Item(id);
                if (b.Tipo == Nucleo.Itens.TipoItem.Arma)
                {
                    _previa.Dados.Arma = b.Modelo;
                    _previa.Dados.CorArma = Util.Cor(b.Cor);
                }
                else if (b.Tipo == Nucleo.Itens.TipoItem.Pes) _previa.Dados.ModeloBotas = b.Modelo;
            }
            _previa.Ligar(e);
            P.Camera.FocarCena(new Vector3(54.5f, 0.2f, 50.5f), 2);
            P.Camera.Aproximar(10);
        }

        // ------------------------------------------------------------------ pausa

        public void Pausar()
        {
            Pausado = true;
            RectTransform t = Tela("pausa");
            Text titulo = IU.Texto(t, "titulo", T("PAUSA"), 60, IU.Ambar, TextAnchor.MiddleCenter);
            titulo.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(600, 80));
            float y = 180;
            Botao(t, T("Continuar"), Despausar, y);
            Botao(t, F("Salvar (espaço {0})", P.Espaco), () =>
            {
                P.Salvar();
                Despausar();
            }, y -= 66);
            IU.Ativo(Botao(t, T("Carregar"), () => Espacos(Pausar), y -= 66), P.TemSalvo);
            Botao(t, T("Opções"), () => Opcoes(Pausar), y -= 66);
            Botao(t, T("Controles"), () => ControlesTela(Pausar), y -= 66);
            Botao(t, T("Menu principal"), () =>
            {
                Pausado = false;
                P.Salvar();
                P.AbrirMenu();
            }, y -= 66);
        }

        public void Despausar()
        {
            Pausado = false;
            _esperandoTecla = null;
            EsconderTudo();
        }

        // ------------------------------------------------------------------ opcoes

        public void Opcoes(System.Action voltar)
        {
            _voltarDasOpcoes = voltar;
            RectTransform t = Tela("opcoes");
            Image p = IU.Painel(t, "painel");
            p.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 820));
            Text titulo = IU.Texto(p.transform, "titulo", T("Opções"), 36, IU.Ambar, TextAnchor.UpperCenter);
            titulo.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(600, 50));
            string[] abas = { "som", "tela", "teclas" };
            string[] nomes = { T("Som"), T("Tela"), T("Teclas") };
            for (int i = 0; i < abas.Length; i++)
            {
                string aba = abas[i];
                Button b = IU.Botao(p.transform, "aba", nomes[i], () =>
                {
                    _abaOpcoes = aba;
                    Opcoes(voltar);
                }, 22);
                b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 1) * 230, -86), new Vector2(220, 46));
                if (aba == _abaOpcoes) b.GetComponent<Image>().color = IU.Ambar;
            }
            RectTransform corpo = IU.Novo(p.transform, "corpo").Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -150), new Vector2(900, 580));
            switch (_abaOpcoes)
            {
                case "tela": AbaTela(corpo); break;
                case "teclas": AbaTeclas(corpo); break;
                default: AbaSom(corpo); break;
            }
            Button v2 = IU.Botao(p.transform, "voltar", T("Voltar"), () =>
            {
                _esperandoTecla = null;
                voltar();
            }, 24);
            v2.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(240, 52));
        }

        private void Reabrir() => Opcoes(_voltarDasOpcoes);

        private void AbaSom(Transform corpo)
        {
            Deslizante(corpo, T("Música"), -20, P.Sons.Musica.Volume, v =>
            {
                P.Sons.Musica.Volume = v;
                Jogo.Opcoes.Salvar(P);
            });
            Deslizante(corpo, T("Efeitos"), -100, P.Sons.VolumeEfeitos, v =>
            {
                P.Sons.VolumeEfeitos = v;
                Jogo.Opcoes.Salvar(P);
            });
            Deslizante(corpo, T("Ambiente"), -180, P.Sons.Musica.VolumeAmbiente, v =>
            {
                P.Sons.Musica.VolumeAmbiente = v;
                Jogo.Opcoes.Salvar(P);
            });
        }

        private void AbaTela(Transform corpo)
        {
            float x0 = 60, x1 = 330;
            float y = -10;

            // Idioma: troca e remonta o jogo (no meio de uma partida, salva antes).
            Rotulo(corpo, T("Idioma"), new Vector2(x0, y));
            Opcao(corpo, "Português", Jogo.Opcoes.Idioma == Portugues, () => MudarIdioma(Portugues), new Vector2(x1, y), 190);
            Opcao(corpo, "English", Jogo.Opcoes.Idioma == Ingles, () => MudarIdioma(Ingles), new Vector2(x1 + 200, y), 190);

            y -= 70;
            Rotulo(corpo, T("Resolução"), new Vector2(x0, y));
            List<(int w, int h)> lista = Resolucoes();
            int atual = lista.FindIndex(r => r.w == Screen.width && r.h == Screen.height);
            Opcao(corpo, "◀", false, () => TrocarResolucao(lista, atual - 1), new Vector2(x1, y), 50);
            Text res = IU.Texto(corpo, "res", $"{Screen.width} × {Screen.height}", 22, IU.CorTexto, TextAnchor.MiddleCenter);
            res.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x1 + 60, y), new Vector2(270, 42));
            Opcao(corpo, "▶", false, () => TrocarResolucao(lista, atual + 1), new Vector2(x1 + 340, y), 50);

            y -= 70;
            Rotulo(corpo, T("Tela cheia"), new Vector2(x0, y));
            Opcao(corpo, T("Sim"), Jogo.Opcoes.TelaCheia, () => MudarTela(true), new Vector2(x1, y), 190);
            Opcao(corpo, T("Não"), !Jogo.Opcoes.TelaCheia, () => MudarTela(false), new Vector2(x1 + 200, y), 190);

            y -= 70;
            Rotulo(corpo, T("Sincronia vertical"), new Vector2(x0, y));
            Opcao(corpo, T("Sim"), Jogo.Opcoes.VSync, () => MudarVSync(true), new Vector2(x1, y), 190);
            Opcao(corpo, T("Não"), !Jogo.Opcoes.VSync, () => MudarVSync(false), new Vector2(x1 + 200, y), 190);

            y -= 70;
            Rotulo(corpo, T("Qualidade"), new Vector2(x0, y));
            for (int i = 0; i < QualidadeGrafica.Nomes.Length; i++)
            {
                int nivel = i;
                Opcao(corpo, T(QualidadeGrafica.Nomes[i]), Jogo.Opcoes.Qualidade == i, () =>
                {
                    Jogo.Opcoes.Qualidade = nivel;
                    QualidadeGrafica.Aplicar(nivel, P.Camera);
                    Jogo.Opcoes.Salvar(P);
                    Reabrir();
                }, new Vector2(x1 + i * 160, y), 150);
            }

            y -= 70;
            Rotulo(corpo, T("Tamanho do texto"), new Vector2(x0, y));
            for (int i = 0; i < Escalas.Length; i++)
            {
                float escala = Escalas[i];
                Opcao(corpo, $"{escala * 100:0}%", Mathf.Abs(Jogo.Opcoes.EscalaDoTexto - escala) < 0.01f, () =>
                {
                    Jogo.Opcoes.EscalaDoTexto = escala;
                    _ui.EscalaDoTexto(escala);
                    Jogo.Opcoes.Salvar(P);
                    Reabrir();
                }, new Vector2(x1 + i * 120, y), 110);
            }

            y -= 80;
            Text nota = IU.Texto(corpo, "nota", T("Qualidade baixa desliga a oclusão de ambiente e as sombras de longe; ajuda em placa de vídeo fraca."), 17, IU.CorFraca);
            nota.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0, y), new Vector2(780, 50));
        }

        private static List<(int w, int h)> Resolucoes()
        {
            var lista = Screen.resolutions.Select(r => (r.width, r.height)).Where(r => r.width >= 960 && r.height >= 540).Distinct().OrderBy(r => r.width * 10000 + r.height).ToList();
            if (lista.Count == 0) lista.Add((Screen.width, Screen.height));
            return lista;
        }

        private void TrocarResolucao(List<(int w, int h)> lista, int indice)
        {
            if (indice < 0) indice = lista.Count - 1;
            if (indice >= lista.Count) indice = 0;
            Jogo.Opcoes.Largura = lista[indice].w;
            Jogo.Opcoes.Altura = lista[indice].h;
            Jogo.Opcoes.AplicarTela();
            Jogo.Opcoes.Salvar(P);
            Reabrir();
        }

        private void MudarTela(bool cheia)
        {
            Jogo.Opcoes.TelaCheia = cheia;
            Jogo.Opcoes.AplicarTela();
            Jogo.Opcoes.Salvar(P);
            Reabrir();
        }

        private void MudarVSync(bool ligado)
        {
            Jogo.Opcoes.VSync = ligado;
            QualitySettings.vSyncCount = ligado ? 1 : 0;
            Application.targetFrameRate = ligado ? -1 : 144;
            Jogo.Opcoes.Salvar(P);
            Reabrir();
        }

        private void MudarIdioma(string idioma)
        {
            if (idioma == Jogo.Opcoes.Idioma) return;
            if (P.Estado == EstadoDoJogo.Jogando && !P.Demonstracao) P.Salvar();
            P.TrocarIdioma(idioma);
        }

        private static string NomeDaAcao(Acao a)
        {
            switch (a)
            {
                case Acao.Cima: return T("Andar para cima");
                case Acao.Baixo: return T("Andar para baixo");
                case Acao.Esquerda: return T("Andar para a esquerda");
                case Acao.Direita: return T("Andar para a direita");
                case Acao.Interagir: return T("Interagir");
                case Acao.Rolar: return T("Rolar");
                case Acao.AtacarParado: return T("Atacar parado (segure)");
                case Acao.MostrarItens: return T("Mostrar itens no chão");
                case Acao.GirarEsquerda: return T("Girar câmera ←");
                case Acao.GirarDireita: return T("Girar câmera →");
                case Acao.Mochila: return T("Mochila");
                case Acao.Personagem: return T("Personagem");
                case Acao.Poderes: return T("Poderes");
                case Acao.Diario: return T("Diário");
                case Acao.Mapa: return T("Mapa");
                case Acao.Salvar: return T("Salvar");
                case Acao.Carregar: return T("Carregar");
                default: return F("Barra {0}", (int)a - (int)Acao.Barra1 + 1);
            }
        }

        private void AbaTeclas(Transform corpo)
        {
            Acao[] acoes = (Acao[])System.Enum.GetValues(typeof(Acao));
            int porColuna = (acoes.Length + 1) / 2;
            for (int i = 0; i < acoes.Length; i++)
            {
                Acao a = acoes[i];
                int coluna = i / porColuna, linha = i % porColuna;
                float x = 40 + coluna * 420, y = -6 - linha * 44;
                Text nome = IU.Texto(corpo, "acao", NomeDaAcao(a), 18, IU.CorTexto, TextAnchor.MiddleLeft);
                nome.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(250, 38));
                bool esperando = _esperandoTecla == a;
                Button b = IU.Botao(corpo, "tecla", esperando ? T("aperte…") : Controles.Nome(a), () =>
                {
                    _esperandoTecla = a;
                    Reabrir();
                }, 18);
                b.GetComponent<RectTransform>().Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 260, y), new Vector2(130, 38));
                if (esperando) b.GetComponent<Image>().color = IU.Ambar;
            }
            Button restaurar = IU.Botao(corpo, "padrao", T("Restaurar o padrão"), () =>
            {
                Controles.Restaurar();
                Jogo.Opcoes.Salvar(P);
                Reabrir();
            }, 18);
            restaurar.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(280, 40));
            Text nota = IU.Texto(corpo, "nota", _esperandoTecla.HasValue ? T("Aperte a tecla nova. Esc cancela.") : T("Clique numa tecla para trocar. Se outra ação usava a mesma, as duas trocam."), 16, IU.CorFraca, TextAnchor.MiddleCenter);
            nota.rectTransform.Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(800, 30));
        }

        private static void Deslizante(Transform pai, string rotulo, float y, float valor, System.Action<float> mudar)
        {
            Text t = IU.Texto(pai, "rotulo", rotulo, 22, IU.CorTexto);
            t.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, y), new Vector2(220, 40));
            RectTransform s = IU.Novo(pai, "deslizante").Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(330, y - 8), new Vector2(460, 24));
            Image fundo = IU.Imagem(s, "fundo", new Color(0.1f, 0.1f, 0.1f));
            fundo.rectTransform.Esticar();
            RectTransform area = IU.Novo(s, "area");
            area.Esticar();
            Image cheio = IU.Imagem(area, "cheio", IU.Ambar);
            // Sem esticar, o preenchimento herdaria o tamanho padrao da imagem e viraria um bloco.
            cheio.rectTransform.Esticar();
            RectTransform alca = IU.Novo(s, "alca");
            alca.Esticar();
            Image botao = IU.Imagem(alca, "botao", Color.white);
            botao.rectTransform.sizeDelta = new Vector2(18, 34);
            var sl = s.gameObject.AddComponent<Slider>();
            sl.fillRect = cheio.rectTransform;
            sl.handleRect = botao.rectTransform;
            sl.targetGraphic = botao;
            sl.minValue = 0;
            sl.maxValue = 1;
            sl.value = Mathf.Clamp01(valor);
            sl.onValueChanged.AddListener(v => mudar(v));
        }

        // ------------------------------------------------------------------ morte

        public void Morte(int perda)
        {
            RectTransform t = Tela("morte");
            Image f = t.Find("fundo").GetComponent<Image>();
            f.color = new Color(0.15f, 0, 0, 0.55f);
            Text titulo = IU.Texto(t, "titulo", T("O LABIRINTO VENCEU HOJE"), 64, new Color(0.9f, 0.3f, 0.25f), TextAnchor.MiddleCenter);
            titulo.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 90));
            Text sub = IU.Texto(t, "sub", perda > 0 ? F("Alguém te arrastou de volta. Pelo caminho, {0} de sucata ficaram para trás.", perda) : T("Alguém te arrastou de volta."), 26, IU.CorTexto, TextAnchor.MiddleCenter);
            sub.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 40));
            Botao(t, T("Acordar na enfermaria"), () =>
            {
                EsconderTudo();
                P.Renascer();
            }, -70, 440);
        }

        // ------------------------------------------------------------------ final

        public void Creditos(string final)
        {
            RectTransform t = Tela("creditos");
            t.Find("fundo").GetComponent<Image>().color = new Color(0, 0, 0, 0.92f);
            _rolagem = IU.Novo(t, "rolagem").Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(1100, 2600));
            string texto = final == "libertacao"
                ? T("<size=60><b>OS LEMBRADOS</b></size>\n\nOs muros desceram ao amanhecer.\nTrinta e oito pessoas atravessaram o labirinto andando, pela primeira vez sem correr.\n\nLá fora, o mundo era cinza e estava vazio.\nMas era grande. E era deles.\n\nNinguém ali lembrava de quem tinha sido.\nTodo mundo ia lembrar de quem estava sendo.")
                : T("<size=60><b>O NOVO ARQUITETO</b></size>\n\nOs muros continuaram no lugar.\nÀ noite, eles se moveram de novo, do jeito que você desenhou.\n\nCem dias depois, a cura funcionou em ratos.\nDuzentos dias depois, em gente.\n\nNo Pátio, ninguém soube. A caixa continuou subindo, uma vez por mês.\nNa etiqueta, a sua letra.");
            texto += "\n\n\n\n" + T("<size=40><b>OS ESQUECIDOS</b></size>\n\nUm RPG de ação feito inteiramente em código:\nmodelos, animação, shaders, música e sons.\n\nInspirado em <b>Flare</b> (Clint Bellanger e colaboradores)\ne no <b>Pal3.Unity</b> (0x7c13).\n\nObrigado por correr.");
            Text tx = IU.Texto(_rolagem, "texto", texto, 28, IU.CorTexto, TextAnchor.UpperCenter);
            tx.rectTransform.Esticar();
            _creditosT = 0;
            Button b = IU.Botao(t, "menu", T("Menu principal"), () =>
            {
                EsconderTudo();
                P.AbrirMenu();
            }, 22);
            b.GetComponent<RectTransform>().Lugar(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 24), new Vector2(260, 50));
        }

        public void Atualizar(float dt)
        {
            if (_rolagem != null)
            {
                _creditosT += dt;
                // O topo do texto nasce a um terco da tela e sobe devagar: o titulo do final aparece na hora.
                _rolagem.anchoredPosition = new Vector2(0, 320 + Mathf.Max(0, _creditosT - 1.5f) * 42);
            }
            if (_esperandoTecla.HasValue)
            {
                if (Controles.Desceu(Key.Escape))
                {
                    _esperandoTecla = null;
                    Reabrir();
                    return;
                }
                Key? k = Controles.TeclaQueDesceu();
                if (k.HasValue)
                {
                    Controles.Definir(_esperandoTecla.Value, k.Value);
                    _esperandoTecla = null;
                    Jogo.Opcoes.Salvar(P);
                    Reabrir();
                }
            }
        }
    }
}
