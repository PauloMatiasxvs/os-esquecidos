using System.Collections.Generic;
using System.Linq;
using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Cena;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Progresso;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.UI;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>O que fica sempre na tela durante o jogo.</summary>
    public sealed class Hud
    {
        private readonly Interface _ui;
        private readonly RectTransform _raiz;
        private readonly RectTransform _mundo;
        private Image _vida, _folego, _xp, _chefeVida, _alvoVida;
        private Text _vidaTxt, _folegoTxt, _xpTxt, _nome, _pontos, _relogio, _local, _missao, _chefeNome, _alvoNome, _sucata, _prompt;
        private RectTransform _chefe, _alvo, _efeitos;
        private readonly Slot[] _barra = new Slot[Personagem.TamanhoDaBarra];
        private RawImage _mini;
        private Texture2D _miniTex;
        private RectTransform _miniJogador;
        private readonly List<RectTransform> _miniPontos = new List<RectTransform>();
        private RectTransform _miniPontosRaiz;
        private int _exploradoVisto = -1;
        private int _versaoGrade = -1;
        private Mapa _mapa;
        private float _missaoAtualizar;
        private readonly Dictionary<int, (RectTransform r, Image vida)> _barrasDeCabeca = new Dictionary<int, (RectTransform, Image)>();
        private readonly Dictionary<int, Text> _rotulosLoot = new Dictionary<int, Text>();
        private readonly List<Text> _poolNomes = new List<Text>();
        private int _nomesUsados;

        private sealed class Slot
        {
            public Image Fundo, Icone, Recarga;
            public Text Tecla, Qtd, Simbolo;
            public string Atual;
        }

        public Hud(Interface ui, RectTransform raiz, RectTransform mundo)
        {
            _ui = ui;
            _raiz = IU.Novo(raiz, "hud");
            _raiz.Esticar();
            _mundo = mundo;
            Montar();
        }

        private Partida Partida => _ui.P.Partida;

        public void Mostrar(bool sim)
        {
            _raiz.gameObject.SetActive(sim);
            _mundo.gameObject.SetActive(sim);
        }

        private void Montar()
        {
            // Barra de baixo: vida, poderes, folego, experiencia.
            RectTransform baixo = IU.Novo(_raiz, "baixo").Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(1060, 104));
            _xp = IU.Barra(baixo, "xp", IU.Xp, out _xpTxt, 12);
            _xp.transform.parent.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(1060, 12));
            _vida = IU.Barra(baixo, "vida", IU.Vida, out _vidaTxt, 18);
            _vida.transform.parent.GetComponent<RectTransform>().Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 22), new Vector2(230, 34));
            _folego = IU.Barra(baixo, "folego", IU.Folego, out _folegoTxt, 18);
            _folego.transform.parent.GetComponent<RectTransform>().Lugar(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 22), new Vector2(230, 34));
            string[] teclas = { "◧", "◨", "1", "2", "3", "4", "5", "6" };
            for (int i = 0; i < _barra.Length; i++)
            {
                var s = new Slot();
                s.Fundo = IU.Painel(baixo, "slot" + i, new Color(0.06f, 0.06f, 0.06f, 0.85f));
                s.Fundo.rectTransform.Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - 3.5f) * 70, 20), new Vector2(64, 64));
                s.Icone = IU.Imagem(s.Fundo.transform, "icone", Color.white);
                s.Icone.rectTransform.Esticar(4);
                s.Icone.preserveAspect = true;
                s.Icone.enabled = false;
                s.Simbolo = IU.Texto(s.Fundo.transform, "simbolo", "", 32, Color.white, TextAnchor.MiddleCenter);
                s.Simbolo.rectTransform.Esticar(2);
                s.Recarga = IU.Imagem(s.Fundo.transform, "recarga", new Color(0, 0, 0, 0.7f));
                s.Recarga.rectTransform.Esticar(2);
                s.Recarga.type = Image.Type.Filled;
                s.Recarga.fillMethod = Image.FillMethod.Radial360;
                s.Recarga.fillOrigin = 2;
                s.Recarga.fillClockwise = false;
                s.Recarga.fillAmount = 0;
                s.Tecla = IU.Texto(s.Fundo.transform, "tecla", teclas[i], 14, IU.Ambar, TextAnchor.UpperLeft);
                s.Tecla.rectTransform.Esticar(4);
                s.Qtd = IU.Texto(s.Fundo.transform, "qtd", "", 15, IU.CorTexto, TextAnchor.LowerRight);
                s.Qtd.rectTransform.Esticar(4);
                int indice = i;
                Gatilho g = IU.Eventos(s.Fundo.gameObject);
                g.AoEntrar = () => _ui.Janelas.DicaDaBarra(indice, s.Fundo.rectTransform);
                g.AoSair = () => _ui.Janelas.EsconderDica();
                g.AoClicar = () => _ui.Janelas.CliqueNaBarra(indice);
                g.AoClicarDireito = () => _ui.Janelas.CliqueNaBarra(indice, true);
                _barra[i] = s;
            }
            _efeitos = IU.Novo(_raiz, "efeitos").Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-300, 130), new Vector2(400, 34));

            // Canto de cima a esquerda: quem voce e.
            _nome = IU.Texto(_raiz, "nome", "", 26, IU.CorTexto);
            _nome.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -18), new Vector2(600, 34));
            _pontos = IU.Texto(_raiz, "pontos", "", 18, IU.Ambar);
            _pontos.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -52), new Vector2(600, 26));
            _sucata = IU.Texto(_raiz, "sucata", "", 20, new Color(0.95f, 0.8f, 0.45f));
            _sucata.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -78), new Vector2(400, 26));

            // Missao atual, a esquerda.
            _missao = IU.Texto(_raiz, "missao", "", 19, IU.CorTexto);
            _missao.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -130), new Vector2(430, 220));

            // Canto de cima a direita: minimapa e relogio.
            Image moldura = IU.Painel(_raiz, "minimapa", new Color(0.02f, 0.02f, 0.02f, 0.85f));
            moldura.rectTransform.Lugar(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(250, 250));
            moldura.gameObject.AddComponent<RectMask2D>();
            _mini = IU.Novo(moldura.transform, "mapa").gameObject.AddComponent<RawImage>();
            _mini.rectTransform.Esticar(3);
            _mini.raycastTarget = false;
            _mini.enabled = false;
            _miniPontosRaiz = IU.Novo(moldura.transform, "pontos");
            _miniPontosRaiz.Esticar(3);
            _miniJogador = IU.Texto(moldura.transform, "jogador", "▲", 22, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter).rectTransform;
            _miniJogador.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            _relogio = IU.Texto(_raiz, "relogio", "", 22, IU.CorTexto, TextAnchor.UpperRight);
            _relogio.rectTransform.Lugar(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -276), new Vector2(400, 30));
            _local = IU.Texto(_raiz, "local", "", 17, IU.CorFraca, TextAnchor.UpperRight);
            _local.rectTransform.Lugar(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -304), new Vector2(400, 26));

            // Chefe e alvo, no alto do meio.
            _chefe = IU.Novo(_raiz, "chefe").Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(720, 60));
            _chefeNome = IU.Texto(_chefe, "nome", "", 26, new Color(1f, 0.82f, 0.6f), TextAnchor.UpperCenter);
            _chefeNome.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(720, 32));
            _chefeVida = IU.Barra(_chefe, "vida", new Color(0.7f, 0.12f, 0.1f), out _, 14);
            _chefeVida.transform.parent.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(720, 20));
            _chefe.gameObject.SetActive(false);
            _alvo = IU.Novo(_raiz, "alvo").Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(380, 46));
            _alvoNome = IU.Texto(_alvo, "nome", "", 19, IU.CorTexto, TextAnchor.UpperCenter);
            _alvoNome.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(380, 24));
            _alvoVida = IU.Barra(_alvo, "vida", new Color(0.7f, 0.15f, 0.12f), out _, 12);
            _alvoVida.transform.parent.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(300, 12));
            _alvo.gameObject.SetActive(false);

            _prompt = IU.Texto(_raiz, "prompt", "", 20, IU.CorTexto, TextAnchor.MiddleCenter);
            _prompt.rectTransform.Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(700, 30));
        }

        public void MissaoMudou() => _missaoAtualizar = 0;

        public void MapaMudou(Mapa mapa)
        {
            _mapa = mapa;
            _exploradoVisto = -1;
            _versaoGrade = -1;
            foreach (var b in _barrasDeCabeca.Values) Object.Destroy(b.r.gameObject);
            _barrasDeCabeca.Clear();
            foreach (Text t in _rotulosLoot.Values) Object.Destroy(t.gameObject);
            _rotulosLoot.Clear();
        }

        // ------------------------------------------------------------------ atualizacao

        public void Atualizar(float dt)
        {
            if (!_raiz.gameObject.activeSelf || Partida == null || Partida.Jogador == null) return;
            Entidade j = Partida.Jogador;
            Personagem h = Partida.Heroi;
            Catalogo c = Partida.Catalogo;
            _vida.fillAmount = j.Vida / j.VidaMax;
            _vidaTxt.text = $"{Mathf.CeilToInt(j.Vida)} / {Mathf.RoundToInt(j.VidaMax)}";
            _folego.fillAmount = j.EnergiaMax > 0 ? j.Energia / j.EnergiaMax : 0;
            _folegoTxt.text = $"{Mathf.FloorToInt(j.Energia)} / {Mathf.RoundToInt(j.EnergiaMax)}";
            int xpAtual = h.Xp - c.Regras.XpTotalPara(h.Nivel), xpProx = c.Regras.XpTotalPara(h.Nivel + 1) - c.Regras.XpTotalPara(h.Nivel);
            _xp.fillAmount = h.Nivel >= c.Regras.NivelMaximo ? 1 : xpAtual / (float)Mathf.Max(1, xpProx);
            _xpTxt.text = h.Nivel >= c.Regras.NivelMaximo ? "nível máximo" : $"{xpAtual} / {xpProx} XP";
            _nome.text = $"{h.Nome}  <size=20><color=#b0a890>{c.Classe(h.Classe).Nome} · nível {h.Nivel}</color></size>";
            string pontos = "";
            if (h.PontosDeAtributo > 0) pontos += $"+{h.PontosDeAtributo} atributo (C)   ";
            if (h.PontosDePoder > 0) pontos += $"+{h.PontosDePoder} poder (K)";
            _pontos.text = pontos;
            _sucata.text = $"⚙ {h.Sucata} sucata";
            CicloDoDia ciclo = Partida.Ciclo;
            string sol = ciclo.Noite ? "☾" : "☀";
            string aviso = !ciclo.Noite && ciclo.Hora >= CicloDoDia.AvisoDeFechamento ? IU.Cor("  portões fechando!", "#ff9a4a") : "";
            _relogio.text = $"{sol} {ciclo.Relogio()}{aviso}";
            _local.text = Partida.Mapa?.Nome ?? "";
            AtualizarBarra(h, j, c);
            AtualizarEfeitos(j);
            AtualizarChefeEAlvo();
            _missaoAtualizar -= dt;
            if (_missaoAtualizar <= 0)
            {
                _missaoAtualizar = 0.5f;
                AtualizarMissao();
            }
            AtualizarMinimapa();
            AtualizarPrompt();
        }

        private void AtualizarBarra(Personagem h, Entidade j, Catalogo c)
        {
            for (int i = 0; i < _barra.Length; i++)
            {
                Slot s = _barra[i];
                string id = h.Barra[i];
                if (id != s.Atual)
                {
                    s.Atual = id;
                    s.Icone.enabled = false;
                    s.Simbolo.text = "";
                    if (id == "ataque")
                    {
                        ItemBase arma = h.Arma(c);
                        if (arma != null)
                        {
                            s.Icone.sprite = _ui.Estudio.Icone(arma);
                            s.Icone.enabled = true;
                            s.Icone.color = Color.white;
                        }
                        else s.Simbolo.text = IconesDePoder.Simbolo("soco");
                    }
                    else if (id != null && c.Itens.TryGetValue(id, out ItemBase item))
                    {
                        s.Icone.sprite = _ui.Estudio.Icone(item);
                        s.Icone.enabled = true;
                        s.Icone.color = Color.white;
                    }
                    else if (id != null && c.Poderes.TryGetValue(id, out Poder p))
                    {
                        s.Icone.sprite = IU.Branco;
                        s.Icone.enabled = true;
                        s.Icone.color = IconesDePoder.CorDaArvore(p.Arvore);
                        s.Simbolo.text = IconesDePoder.Simbolo(id);
                    }
                }
                s.Recarga.fillAmount = 0;
                s.Qtd.text = "";
                if (id == null) continue;
                if (c.Itens.ContainsKey(id))
                {
                    int n = h.Mochila.Contar(id);
                    s.Qtd.text = n.ToString();
                    s.Icone.color = n > 0 ? Color.white : new Color(1, 1, 1, 0.3f);
                }
                else if (id != "ataque" && c.Poderes.TryGetValue(id, out Poder p))
                {
                    Poder ef = Partida.PoderDoHeroi(id);
                    float rec = j.Recarga(id);
                    s.Recarga.fillAmount = ef.Recarga > 0 ? rec / ef.Recarga : 0;
                    bool sem = j.Energia < ef.Custo;
                    bool arma = ef.AceitaArma(j.ClasseDaArma);
                    s.Simbolo.color = !arma ? new Color(0.5f, 0.5f, 0.5f) : sem ? new Color(0.5f, 0.7f, 1f) : Color.white;
                    if (h.RankDe(id) > 1) s.Qtd.text = "★" + h.RankDe(id);
                }
            }
        }

        private readonly List<Text> _iconesDeEfeito = new List<Text>();

        private void AtualizarEfeitos(Entidade j)
        {
            int i = 0;
            foreach (EfeitoAtivo e in j.Efeitos)
            {
                if (i >= 10) break;
                if (_iconesDeEfeito.Count <= i)
                {
                    Text t = IU.Texto(_efeitos, "ef", "", 16, Color.white, TextAnchor.MiddleCenter);
                    t.rectTransform.Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * 76, 0), new Vector2(72, 30));
                    _iconesDeEfeito.Add(t);
                }
                Text ic = _iconesDeEfeito[i];
                ic.gameObject.SetActive(true);
                (string nome, Color cor) = NomeDoEfeito(e.Nome);
                ic.text = $"{nome} {Mathf.CeilToInt(e.Restante)}s";
                ic.color = cor;
                i++;
            }
            for (; i < _iconesDeEfeito.Count; i++) _iconesDeEfeito[i].gameObject.SetActive(false);
        }

        public static (string, Color) NomeDoEfeito(string e)
        {
            switch (e)
            {
                case "veneno": return ("veneno", new Color(0.55f, 1f, 0.35f));
                case "sangramento": return ("sangra", new Color(1f, 0.35f, 0.3f));
                case "queimadura": return ("queima", new Color(1f, 0.6f, 0.25f));
                case "atordoado": return ("tonto", new Color(1f, 0.9f, 0.4f));
                case "lento": return ("lento", new Color(0.55f, 0.75f, 1f));
                case "enraizado": return ("preso", new Color(0.9f, 0.75f, 0.5f));
                case "furia": return ("fúria", new Color(1f, 0.4f, 0.25f));
                case "pele": return ("pele", new Color(0.8f, 0.85f, 0.95f));
                case "barreira": return ("escudo", new Color(0.6f, 0.8f, 1f));
                case "regeneracao": return ("cura", new Color(0.5f, 1f, 0.55f));
                case "rapidez": return ("rápido", new Color(0.9f, 0.95f, 1f));
                case "vulneravel": return ("exposto", new Color(1f, 0.85f, 0.3f));
                case "provocado": return ("provocado", new Color(1f, 0.5f, 0.3f));
            }
            return (e, Color.white);
        }

        private void AtualizarChefeEAlvo()
        {
            Entidade chefe = null;
            Entidade j = Partida.Jogador;
            foreach (Entidade e in Partida.Mundo.Entidades)
            {
                if (!e.Chefe || !e.Vivo) continue;
                if (System.Numerics.Vector2.Distance(e.Pos, j.Pos) > 24) continue;
                if (e.Cerebro is IAComum ia && !ia.Acordado && e.Vida >= e.VidaMax) continue;
                chefe = e;
            }
            _chefe.gameObject.SetActive(chefe != null);
            if (chefe != null)
            {
                _chefeNome.text = chefe.Nome;
                _chefeVida.fillAmount = chefe.Vida / chefe.VidaMax;
            }
            VisualDeEntidade alvo = _ui.P.Controle.Mirado;
            bool mostra = alvo != null && alvo.E.Vivo && alvo.E != chefe;
            _alvo.gameObject.SetActive(mostra);
            if (mostra)
            {
                Entidade e = alvo.E;
                bool inimigo = e.Faccao == Faccao.Inimigo;
                string titulo = e.NpcId != null && e.Def == null ? _ui.P.Partida.Catalogo.Npc(e.NpcId).Titulo : $"nível {e.Nivel}";
                _alvoNome.text = $"{e.Nome} <size=15><color=#b0a890>{titulo}</color></size>";
                _alvoNome.color = inimigo ? (e.Elite ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.75f, 0.7f)) : new Color(0.8f, 0.95f, 0.8f);
                _alvoVida.transform.parent.gameObject.SetActive(inimigo);
                _alvoVida.fillAmount = e.Vida / e.VidaMax;
            }
        }

        private void AtualizarMissao()
        {
            Diario d = Partida.Diario;
            var linhas = new List<string>();
            foreach (string id in d.Ordem)
            {
                EstadoDeMissao m = d.Missoes[id];
                if (m.Concluida) continue;
                MissaoDef def = Partida.Catalogo.Missao(id);
                if (!def.Principal) continue;
                linhas.Add(IU.Cor(def.Nome, IU.Ambar));
                linhas.Add(d.TextoDaEtapa(id));
                break;
            }
            int secundarias = 0;
            foreach (string id in d.Ordem)
            {
                EstadoDeMissao m = d.Missoes[id];
                MissaoDef def = Partida.Catalogo.Missao(id);
                if (m.Concluida || def.Principal || secundarias >= 2) continue;
                secundarias++;
                linhas.Add("");
                linhas.Add(IU.Cor(def.Nome, "#d0c8a8"));
                linhas.Add(IU.Cor(d.TextoDaEtapa(id), "#a8a090"));
            }
            _missao.text = string.Join("\n", linhas);
        }

        private void AtualizarPrompt()
        {
            Controle ctl = _ui.P.Controle;
            string texto = "";
            if (Partida.EmCena) texto = "";
            else if (ctl.Mirado != null && ctl.Mirado.E.NpcId != null && ctl.Mirado.E.Faccao == Faccao.Neutro) texto = $"clique: falar com {ctl.Mirado.E.Nome}";
            else if (ctl.LootMirado != null) texto = $"clique: pegar {ctl.LootMirado.Nome}";
            else if (ctl.ObjetoMirado != null) texto = $"clique: {ctl.ObjetoMirado.O.Nome}";
            else
            {
                // O mais perto ao alcance da tecla F.
                Entidade j = Partida.Jogador;
                float melhor = 3.2f;
                foreach (Entidade e in Partida.Mundo.Entidades)
                {
                    if (e.NpcId == null || e.Faccao != Faccao.Neutro) continue;
                    float d = System.Numerics.Vector2.Distance(e.Pos, j.Pos);
                    if (d < melhor)
                    {
                        melhor = d;
                        texto = $"F: falar com {e.Nome}";
                    }
                }
                foreach (ObjetoDeCena o in Partida.Mundo.Objetos)
                {
                    if (!o.Interativo || o.Tipo == TipoObjeto.Saida || o.Tipo == TipoObjeto.MuroMovel || (o.Tipo == TipoObjeto.Bau && o.Usado) || (o.Tipo == TipoObjeto.Porta && o.Ligado)) continue;
                    float d = System.Numerics.Vector2.Distance(o.Pos, j.Pos) - (o.RaioInteracao - 1.6f);
                    if (d < melhor)
                    {
                        melhor = d;
                        texto = $"F: {o.Nome}";
                    }
                }
            }
            _prompt.text = texto;
        }

        // ------------------------------------------------------------------ minimapa

        private void AtualizarMinimapa()
        {
            Mapa m = Partida.Mapa;
            if (m == null) return;
            Grade g = m.Grade;
            string chave = Partida.ChaveDeExploracao();
            int explorado = chave != null && Partida.Explorado.TryGetValue(chave, out HashSet<int> cel) ? cel.Count : 0;
            if (_miniTex == null || _miniTex.width != g.Largura || _miniTex.height != g.Altura || explorado != _exploradoVisto || g.Versao != _versaoGrade)
            {
                _exploradoVisto = explorado;
                _versaoGrade = g.Versao;
                PintarMinimapa(m, chave != null && Partida.Explorado.TryGetValue(chave, out HashSet<int> c2) ? c2 : null);
            }
            Entidade j = Partida.Jogador;
            float janela = 44f;
            float u = j.Pos.X / g.LarguraMetros, v = j.Pos.Y / g.AlturaMetros;
            float wu = janela / g.LarguraMetros, wv = janela / g.AlturaMetros;
            _mini.uvRect = new Rect(u - wu / 2, v - wv / 2, wu, wv);
            _miniJogador.localRotation = Quaternion.Euler(0, 0, j.Direcao * Mathf.Rad2Deg - 90);

            // Pontos: inimigos acordados em vermelho, gente em amarelo, saidas em azul.
            int k = 0;
            Rect area = _miniPontosRaiz.rect;
            foreach (Entidade e in Partida.Mundo.Entidades)
            {
                if (!e.Vivo || e == j) continue;
                Vector2 d = new Vector2(e.Pos.X - j.Pos.X, e.Pos.Y - j.Pos.Y) / janela;
                if (Mathf.Abs(d.x) > 0.5f || Mathf.Abs(d.y) > 0.5f) continue;
                Color cor;
                if (e.Faccao == Faccao.Inimigo)
                {
                    if (!Partida.Mundo.Grade.LinhaDeVisao(j.Pos, e.Pos) && System.Numerics.Vector2.Distance(e.Pos, j.Pos) > 6) continue;
                    cor = e.Chefe ? new Color(1f, 0.3f, 0.9f) : new Color(1f, 0.25f, 0.2f);
                }
                else if (e.Faccao == Faccao.Neutro) cor = new Color(1f, 0.9f, 0.4f);
                else cor = new Color(0.4f, 0.9f, 0.5f);
                Ponto(k++, d, cor, area, e.Chefe ? 12 : 7);
            }
            foreach (ObjetoDeCena o in Partida.Mundo.Objetos)
            {
                if (o.Tipo != TipoObjeto.Saida && o.Tipo != TipoObjeto.Marco && o.Tipo != TipoObjeto.Alavanca && o.Tipo != TipoObjeto.Coletavel && !(o.Tipo == TipoObjeto.Bau && !o.Usado)) continue;
                Vector2 d = new Vector2(o.Pos.X - j.Pos.X, o.Pos.Y - j.Pos.Y) / janela;
                if (Mathf.Abs(d.x) > 0.5f || Mathf.Abs(d.y) > 0.5f) continue;
                Color cor = o.Tipo == TipoObjeto.Saida ? new Color(0.4f, 0.75f, 1f) : o.Tipo == TipoObjeto.Marco ? new Color(1f, 0.7f, 0.3f) : o.Tipo == TipoObjeto.Alavanca ? (o.Ligado ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.2f)) : new Color(0.9f, 0.85f, 0.6f);
                Ponto(k++, d, cor, area, 9);
            }
            for (; k < _miniPontos.Count; k++) _miniPontos[k].gameObject.SetActive(false);
        }

        private void Ponto(int k, Vector2 d, Color cor, Rect area, float tamanho)
        {
            if (_miniPontos.Count <= k)
            {
                Image i = IU.Imagem(_miniPontosRaiz, "p", Color.white);
                i.raycastTarget = false;
                _miniPontos.Add(i.rectTransform);
            }
            RectTransform r = _miniPontos[k];
            r.gameObject.SetActive(true);
            r.Lugar(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(d.x * area.width, d.y * area.height), new Vector2(tamanho, tamanho));
            r.GetComponent<Image>().color = cor;
        }

        private void PintarMinimapa(Mapa m, HashSet<int> explorado)
        {
            Grade g = m.Grade;
            if (_miniTex == null || _miniTex.width != g.Largura || _miniTex.height != g.Altura)
            {
                _miniTex = new Texture2D(g.Largura, g.Altura, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _mini.texture = _miniTex;
                _mini.enabled = true;
            }
            var px = new Color32[g.Largura * g.Altura];
            Color32 muro = new Color32(28, 26, 24, 255), chao = new Color32(120, 116, 104, 255), escuro = new Color32(14, 14, 14, 255);
            Color32 grama = new Color32(70, 98, 52, 255);
            PlantaDoLabirinto pl = m.Planta;
            bool[] alcancavel = pl != null && Partida.Jogador != null ? Janelas.Alcancavel(g, Partida.Jogador.Pos) : null;
            for (int z = 0; z < g.Altura; z++)
            for (int x = 0; x < g.Largura; x++)
            {
                Color32 cor;
                bool solido = g.Solido(x, z);
                if (pl != null)
                {
                    System.Numerics.Vector2 c = g.Centro(x, z);
                    (int cx, int cz) = pl.CelulaEm(c);
                    bool visto = pl.Dentro(cx, cz)
                        ? explorado != null && explorado.Contains(cz * pl.Colunas + cx)
                        : alcancavel != null && (alcancavel[z * g.Largura + x] || Janelas.Vizinho(alcancavel, g, x, z));
                    cor = solido ? (visto ? muro : escuro) : (visto ? chao : escuro);
                }
                else
                {
                    cor = solido ? muro : (m.Tema == Tema.Patio ? grama : chao);
                }
                px[z * g.Largura + x] = cor;
            }
            _miniTex.SetPixels32(px);
            _miniTex.Apply(false);
        }

        // ------------------------------------------------------------------ rotulos no mundo

        /// <summary>Barra de vida sobre bicho ferido ou mirado, nome sobre gente, nome de item no chao.</summary>
        public void Rotulos(Camera cam, float escala)
        {
            if (!_raiz.gameObject.activeSelf || Partida?.Mundo == null) return;
            var vistos = new HashSet<int>();
            Controle ctl = _ui.P.Controle;
            _nomesUsados = 0;
            foreach (VisualDeEntidade v in _ui.P.Visuais.Entidades)
            {
                if (v == null) continue;
                Entidade e = v.E;
                Vector3 topo = v.PosicaoVisual + Vector3.up * (v.AlturaDaCabeca * v.Escala + 0.35f);
                Vector3 tela = cam.WorldToScreenPoint(topo);
                if (tela.z < 0) continue;
                Vector2 pos = (Vector2)tela / escala;
                if (e.Faccao == Faccao.Inimigo && e.Vivo && e.Def != null && e.Tipo != "boneco" && !e.Chefe && (e.Vida < e.VidaMax || e.Elite || v == ctl.Mirado))
                {
                    vistos.Add(e.Id);
                    if (!_barrasDeCabeca.TryGetValue(e.Id, out var b))
                    {
                        Image fundo = IU.Imagem(_mundo, "barra", new Color(0, 0, 0, 0.75f));
                        Image vida = IU.Imagem(fundo.transform, "vida", e.Elite ? new Color(1f, 0.75f, 0.2f) : new Color(0.85f, 0.2f, 0.15f));
                        vida.rectTransform.anchorMin = Vector2.zero;
                        vida.rectTransform.anchorMax = Vector2.one;
                        vida.rectTransform.offsetMin = new Vector2(1, 1);
                        vida.rectTransform.offsetMax = new Vector2(-1, -1);
                        vida.type = Image.Type.Filled;
                        vida.fillMethod = Image.FillMethod.Horizontal;
                        fundo.raycastTarget = vida.raycastTarget = false;
                        b = (fundo.rectTransform, vida);
                        _barrasDeCabeca[e.Id] = b;
                    }
                    b.r.Lugar(Vector2.zero, new Vector2(0.5f, 0.5f), pos, new Vector2(60 * Mathf.Clamp(v.Escala, 0.8f, 1.6f), 7));
                    b.vida.fillAmount = e.Vida / e.VidaMax;
                }
                else if (e.NpcId != null && e.Faccao == Faccao.Neutro)
                {
                    float d = Vector3.Distance(v.PosicaoVisual, Util.Mundo(Partida.Jogador?.Pos ?? default));
                    if (d < 9 || v == ctl.Mirado) Nome(pos, e.Nome, new Color(0.9f, 0.95f, 0.8f));
                }
                else if (e.Faccao == Faccao.Jogador && e.NpcId != null && e.Vivo)
                {
                    Nome(pos, e.Nome, new Color(0.6f, 0.95f, 0.6f));
                }
            }
            foreach (int id in _barrasDeCabeca.Keys.Where(k => !vistos.Contains(k)).ToList())
            {
                Object.Destroy(_barrasDeCabeca[id].r.gameObject);
                _barrasDeCabeca.Remove(id);
            }
            for (int i = _nomesUsados; i < _poolNomes.Count; i++) _poolNomes[i].gameObject.SetActive(false);

            var lootVisto = new HashSet<int>();
            foreach (VisualDeLoot l in _ui.P.Visuais.Loot)
            {
                if (l == null || l.L.Item == null) continue;
                bool raro = l.L.Item.Raridade >= Raridade.Reforcado;
                if (!ctl.MostrarRotulos && !raro && ctl.LootMirado != l) continue;
                Vector3 tela = cam.WorldToScreenPoint(l.transform.position + Vector3.up * 0.55f);
                if (tela.z < 0) continue;
                lootVisto.Add(l.L.Id);
                if (!_rotulosLoot.TryGetValue(l.L.Id, out Text t))
                {
                    t = IU.Texto(_mundo, "loot", l.Nome, 15, l.Cor, TextAnchor.MiddleCenter);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    _rotulosLoot[l.L.Id] = t;
                }
                t.rectTransform.Lugar(Vector2.zero, new Vector2(0.5f, 0.5f), (Vector2)tela / escala, new Vector2(260, 20));
                t.fontSize = ctl.LootMirado == l ? 19 : 15;
            }
            foreach (int id in _rotulosLoot.Keys.Where(k => !lootVisto.Contains(k)).ToList())
            {
                Object.Destroy(_rotulosLoot[id].gameObject);
                _rotulosLoot.Remove(id);
            }
        }

        private void Nome(Vector2 pos, string nome, Color cor)
        {
            if (_poolNomes.Count <= _nomesUsados)
            {
                Text t = IU.Texto(_mundo, "nome", "", 17, cor, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                _poolNomes.Add(t);
            }
            Text n = _poolNomes[_nomesUsados++];
            n.gameObject.SetActive(true);
            n.text = nome;
            n.color = cor;
            n.rectTransform.Lugar(Vector2.zero, new Vector2(0.5f, 0.5f), pos, new Vector2(240, 22));
        }
    }
}
