using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Esquecidos.Jogo.Atores;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.UI;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>Menu principal, criacao de personagem, pausa, opcoes, morte e final.</summary>
    public sealed class Menus
    {
        private readonly Interface _ui;
        private readonly RectTransform _raiz;
        private RectTransform _atual;
        private Humanoide _previa;
        private string _classe = "corredor";
        private string _nome = "";
        private int _pele = 2, _cabelo = 1, _penteado;
        private float _creditosT;
        private RectTransform _rolagem;

        public bool Pausado { get; private set; }

        private static readonly string[] Peles = { "#f1d2b6", "#e2b48f", "#c99470", "#a8704a", "#7f5235", "#5a3a26" };
        private static readonly string[] Cabelos = { "#1a1410", "#3a2414", "#6b4424", "#a8743a", "#d8c890", "#8a2a18", "#6a6a6a" };
        private static readonly string[] Penteados = { "curto", "raspado", "longo", "tranca", "coque", "rabo", "cacheado", "baguncado", "careca" };
        private static readonly string[] NomesDePenteado = { "curto", "raspado", "longo", "trança", "coque", "rabo", "cacheado", "bagunçado", "careca" };

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

        // ------------------------------------------------------------------ principal

        public void Principal()
        {
            Pausado = false;
            RectTransform t = Tela("principal", false);
            Image faixa = IU.Imagem(t, "faixa", new Color(0, 0, 0, 0.45f));
            faixa.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(620, 2000));
            Text titulo = IU.Texto(t, "titulo", "OS ESQUECIDOS", 86, new Color(0.95f, 0.9f, 0.8f), TextAnchor.MiddleLeft);
            titulo.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 270), new Vector2(900, 110));
            titulo.fontStyle = FontStyle.Bold;
            Text sub = IU.Texto(t, "sub", "Ninguém lembra de como chegou. Ninguém nunca saiu.", 24, IU.Ambar, TextAnchor.MiddleLeft);
            sub.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(74, 200), new Vector2(900, 40));
            RectTransform botoes = IU.Novo(t, "botoes").Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -40), new Vector2(400, 400));
            float y = 140;
            Button cont = Botao(botoes, "Continuar", () => P.Continuar(), y);
            IU.Ativo(cont, P.TemSalvo);
            y -= 66;
            Botao(botoes, "Novo jogo", Criacao, y);
            y -= 66;
            Botao(botoes, "Opções", () => Opcoes(Principal), y);
            y -= 66;
            Botao(botoes, "Controles", () => ControlesTela(Principal), y);
            y -= 66;
            Botao(botoes, "Sair", Application.Quit, y);
            Text rodape = IU.Texto(t, "rodape", "Inspirado em Flare e Pal3.Unity. Tudo no jogo — modelos, música, sons — é gerado por código.", 16, IU.CorFraca, TextAnchor.LowerLeft);
            rodape.rectTransform.Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(70, 24), new Vector2(1200, 30));
        }

        private void ControlesTela(System.Action voltar)
        {
            RectTransform t = Tela("controles");
            Image p = IU.Painel(t, "painel");
            p.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 760));
            string texto =
                "<b>Clique esquerdo</b> — andar; em inimigo, atacar (segure para continuar); em gente, conversar; em item, pegar\n" +
                "<b>W A S D</b> — andar direto\n" +
                "<b>Shift + clique</b> — atacar parado na direção do cursor\n" +
                "<b>Clique direito</b> — poder do segundo espaço da barra\n" +
                "<b>1 a 6</b> — poderes e itens da barra\n" +
                "<b>Espaço</b> — rolamento (nada te acerta no meio dele)\n" +
                "<b>F</b> — interagir com o que estiver perto\n" +
                "<b>Q / E</b> — girar a câmera    <b>roda</b> — aproximar\n" +
                "<b>Alt</b> — mostrar nomes dos itens no chão\n" +
                "<b>I</b> mochila   <b>C</b> personagem   <b>K</b> poderes   <b>J</b> diário   <b>M</b> mapa\n" +
                "<b>F5</b> salvar   <b>F9</b> carregar   <b>Esc</b> pausa\n\n" +
                "<b>Controle</b>: analógico anda, A ataca o mais perto, B rola, X e gatilhos usam a barra, Y interage, LB/RB giram a câmera.\n\n" +
                IU.Cor("Os portões do Pátio fecham às 19h30. Quem fica lá fora passa a noite com eles.", "#ffb040");
            Text tx = IU.Texto(p.transform, "texto", texto, 22, IU.CorTexto);
            tx.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(820, 620));
            Button b = IU.Botao(p.transform, "voltar", "Voltar", voltar, 24);
            b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(240, 50));
        }

        // ------------------------------------------------------------------ criacao

        public void Criacao()
        {
            RectTransform t = Tela("criacao", false);
            Catalogo c = P.Catalogo;
            Image painel = IU.Painel(t, "painel");
            painel.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(720, 980));
            Text titulo = IU.Texto(painel.transform, "titulo", "Quem saiu da caixa?", 36, IU.Ambar);
            titulo.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -22), new Vector2(660, 48));

            Text lnome = IU.Texto(painel.transform, "lnome", "A única coisa que você lembra é o seu nome:", 20, IU.CorFraca);
            lnome.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -82), new Vector2(660, 28));
            Image campo = IU.Painel(painel.transform, "campo", new Color(0.02f, 0.02f, 0.02f, 1));
            campo.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -114), new Vector2(660, 52));
            Text texto = IU.Texto(campo.transform, "texto", "", 28, IU.CorTexto, TextAnchor.MiddleLeft);
            texto.rectTransform.Esticar(12);
            Text dica = IU.Texto(campo.transform, "dica", "digite um nome", 28, new Color(0.4f, 0.38f, 0.35f), TextAnchor.MiddleLeft);
            dica.rectTransform.Esticar(12);
            var input = campo.gameObject.AddComponent<InputField>();
            input.textComponent = texto;
            input.placeholder = dica;
            input.characterLimit = 18;
            input.text = _nome;
            input.onValueChanged.AddListener(v => _nome = v);

            float y = -190;
            Text lclasse = IU.Texto(painel.transform, "lclasse", "O que você faz no Pátio:", 20, IU.CorFraca);
            lclasse.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(660, 28));
            y -= 36;
            foreach (string id in c.OrdemDasClasses)
            {
                Classe cl = c.Classes[id];
                string cid = id;
                Image card = IU.Painel(painel.transform, "classe", id == _classe ? new Color(0.18f, 0.13f, 0.06f, 1) : new Color(0.07f, 0.07f, 0.06f, 1));
                card.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(660, 128));
                if (id == _classe) card.color = IU.Ambar;
                string attrs = $"Físico {cl.Atributos[0]} · Mente {cl.Atributos[1]} · Ofensiva {cl.Atributos[2]} · Defesa {cl.Atributos[3]}";
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
            Text lpele = IU.Texto(painel.transform, "lpele", "Pele", 20, IU.CorFraca);
            lpele.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(200, 28));
            Amostras(painel.transform, Peles, _pele, y - 30, i =>
            {
                _pele = i;
                Criacao();
            });
            y -= 88;
            Text lcab = IU.Texto(painel.transform, "lcab", "Cabelo", 20, IU.CorFraca);
            lcab.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(200, 28));
            Amostras(painel.transform, Cabelos, _cabelo, y - 30, i =>
            {
                _cabelo = i;
                Criacao();
            });
            y -= 88;
            Text lpent = IU.Texto(painel.transform, "lpent", $"Penteado: {IU.Cor(NomesDePenteado[_penteado], "#e8e2d0")}", 20, IU.CorFraca);
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

            Button voltar = IU.Botao(painel.transform, "voltar", "Voltar", Principal, 24);
            voltar.GetComponent<RectTransform>().Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 26), new Vector2(200, 56));
            Button comecar = IU.Botao(painel.transform, "comecar", "Acordar", () =>
            {
                string nome = string.IsNullOrWhiteSpace(_nome) ? "Novato" : _nome.Trim();
                Aparencia ap = Aparencia();
                EsconderTudo();
                P.ComecarNovoJogo(_classe, nome, ap);
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
            Text titulo = IU.Texto(t, "titulo", "PAUSA", 60, IU.Ambar, TextAnchor.MiddleCenter);
            titulo.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(600, 80));
            float y = 150;
            Botao(t, "Continuar", Despausar, y);
            Botao(t, "Salvar", () =>
            {
                P.Salvar();
                Despausar();
            }, y -= 66);
            IU.Ativo(Botao(t, "Carregar o último salvo", () =>
            {
                Pausado = false;
                EsconderTudo();
                P.Continuar();
            }, y -= 66), P.TemSalvo);
            Botao(t, "Opções", () => Opcoes(Pausar), y -= 66);
            Botao(t, "Controles", () => ControlesTela(Pausar), y -= 66);
            Botao(t, "Menu principal", () =>
            {
                Pausado = false;
                P.Salvar();
                P.AbrirMenu();
            }, y -= 66);
        }

        public void Despausar()
        {
            Pausado = false;
            EsconderTudo();
        }

        // ------------------------------------------------------------------ opcoes

        private void Opcoes(System.Action voltar)
        {
            RectTransform t = Tela("opcoes");
            Image p = IU.Painel(t, "painel");
            p.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 560));
            Text titulo = IU.Texto(p.transform, "titulo", "Opções", 36, IU.Ambar, TextAnchor.UpperCenter);
            titulo.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(600, 50));
            Deslizante(p.transform, "Música", -110, P.Sons.Musica.Volume / 0.8f, v =>
            {
                P.Sons.Musica.Volume = v * 0.8f;
                Jogo.Opcoes.Salvar(P);
            });
            Deslizante(p.transform, "Efeitos", -190, P.Sons.VolumeEfeitos, v =>
            {
                P.Sons.VolumeEfeitos = v;
                Jogo.Opcoes.Salvar(P);
            });
            Deslizante(p.transform, "Ambiente", -270, P.Sons.Musica.VolumeAmbiente, v =>
            {
                P.Sons.Musica.VolumeAmbiente = v;
                Jogo.Opcoes.Salvar(P);
            });
            Button tela = IU.Botao(p.transform, "tela", Screen.fullScreen ? "Tela cheia: sim" : "Tela cheia: não", () =>
            {
                Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
                Jogo.Opcoes.Salvar(P);
                Opcoes(voltar);
            }, 22);
            tela.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(360, 48));
            Button v2 = IU.Botao(p.transform, "voltar", "Voltar", voltar, 24);
            v2.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(240, 52));
        }

        private static void Deslizante(Transform pai, string rotulo, float y, float valor, System.Action<float> mudar)
        {
            Text t = IU.Texto(pai, "rotulo", rotulo, 22, IU.CorTexto);
            t.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(180, 40));
            RectTransform s = IU.Novo(pai, "deslizante").Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, y - 8), new Vector2(400, 24));
            Image fundo = IU.Imagem(s, "fundo", new Color(0.1f, 0.1f, 0.1f));
            fundo.rectTransform.Esticar();
            RectTransform area = IU.Novo(s, "area");
            area.Esticar();
            Image cheio = IU.Imagem(area, "cheio", IU.Ambar);
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
            Text titulo = IU.Texto(t, "titulo", "O LABIRINTO VENCEU HOJE", 64, new Color(0.9f, 0.3f, 0.25f), TextAnchor.MiddleCenter);
            titulo.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 90));
            Text sub = IU.Texto(t, "sub", perda > 0 ? $"Alguém te arrastou de volta. Pelo caminho, {perda} de sucata ficaram para trás." : "Alguém te arrastou de volta.", 26, IU.CorTexto, TextAnchor.MiddleCenter);
            sub.rectTransform.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 40));
            Botao(t, "Acordar na enfermaria", () =>
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
                ? "<size=60><b>OS LEMBRADOS</b></size>\n\nOs muros desceram ao amanhecer.\nTrinta e oito pessoas atravessaram o labirinto andando, pela primeira vez sem correr.\n\nLá fora, o mundo era cinza e estava vazio.\nMas era grande. E era deles.\n\nNinguém ali lembrava de quem tinha sido.\nTodo mundo ia lembrar de quem estava sendo."
                : "<size=60><b>O NOVO ARQUITETO</b></size>\n\nOs muros continuaram no lugar.\nÀ noite, eles se moveram de novo, do jeito que você desenhou.\n\nCem dias depois, a cura funcionou em ratos.\nDuzentos dias depois, em gente.\n\nNo Pátio, ninguém soube. A caixa continuou subindo, uma vez por mês.\nNa etiqueta, a sua letra.";
            texto += "\n\n\n\n<size=40><b>OS ESQUECIDOS</b></size>\n\nUm RPG de ação feito inteiramente em código:\nmodelos, animação, shaders, música e sons.\n\nInspirado em <b>Flare</b> (Clint Bellanger e colaboradores)\ne no <b>Pal3.Unity</b> (Jiaqi Liu).\n\nObrigado por correr.";
            Text tx = IU.Texto(_rolagem, "texto", texto, 28, IU.CorTexto, TextAnchor.UpperCenter);
            tx.rectTransform.Esticar();
            _creditosT = 0;
            Button b = IU.Botao(t, "menu", "Menu principal", () =>
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
                _rolagem.anchoredPosition = new Vector2(0, Mathf.Max(0, _creditosT - 1.5f) * 42 - 400);
            }
        }
    }
}

namespace Esquecidos.Jogo
{
    /// <summary>Volumes e tela, guardados num arquivo de texto ao lado do jogo salvo.</summary>
    public static class Opcoes
    {
        public static void Carregar(Principal p)
        {
            try
            {
                if (!File.Exists(Principal.CaminhoDasOpcoes)) return;
                foreach (string linha in File.ReadAllLines(Principal.CaminhoDasOpcoes))
                {
                    string[] kv = linha.Split('=');
                    if (kv.Length != 2 || !float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) continue;
                    switch (kv[0].Trim())
                    {
                        case "musica": p.Sons.Musica.Volume = v; break;
                        case "efeitos": p.Sons.VolumeEfeitos = v; break;
                        case "ambiente": p.Sons.Musica.VolumeAmbiente = v; break;
                        case "tela_cheia": if (v > 0.5f) Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
                    }
                }
            }
            catch (System.Exception e)
            {
                Motor.Registro.Aviso("opcoes: " + e.Message);
            }
        }

        public static void Salvar(Principal p)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Principal.CaminhoDasOpcoes));
                File.WriteAllText(Principal.CaminhoDasOpcoes, string.Format(CultureInfo.InvariantCulture,
                    "musica={0}\nefeitos={1}\nambiente={2}\ntela_cheia={3}\n",
                    p.Sons.Musica.Volume, p.Sons.VolumeEfeitos, p.Sons.Musica.VolumeAmbiente, Screen.fullScreen ? 1 : 0));
            }
            catch (System.Exception e)
            {
                Motor.Registro.Aviso("opcoes: " + e.Message);
            }
        }
    }
}
