using System;
using System.Collections.Generic;
using System.Linq;
using Esquecidos.Motor.Entrada;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Regras;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>
    /// As janelas de jogo: mochila, personagem, poderes, diario, mapa, loja,
    /// oficina, bau e registro. Cada uma e reconstruida quando abre ou quando
    /// algo nela muda; nada de estado escondido na interface.
    /// </summary>
    public sealed partial class Janelas
    {
        private readonly Interface _ui;
        private readonly RectTransform _raiz;
        private readonly Dictionary<string, RectTransform> _janelas = new Dictionary<string, RectTransform>();
        private readonly HashSet<string> _abertas = new HashSet<string>();
        private RectTransform _dica;
        private Text _dicaTexto, _dicaComparar;
        private RectTransform _dicaCompararCaixa;
        private int _segurando = -1;
        private string _poderSegurado;
        private Image _cursorItem;
        private bool _sujo;
        private string _loja, _estacao;
        private bool _mesaDosMapas;
        private string _registro;
        private string _abaPoderes = "corredor";
        private string _abaDiario = "missoes";
        private string _missaoSelecionada;
        private string _receitaSelecionada;

        public bool Alguma => _abertas.Count > 0;

        public Janelas(Interface ui, Transform pai)
        {
            _ui = ui;
            _raiz = IU.Novo(pai, "janelas");
            _raiz.Esticar();
            Image d = IU.Painel(pai, "dica", new Color(0.04f, 0.04f, 0.035f, 0.96f));
            _dica = d.rectTransform;
            _dica.pivot = new Vector2(0, 1);
            _dicaTexto = IU.Texto(_dica, "texto", "", 18, IU.CorTexto);
            _dicaTexto.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(340, 400));
            Image dc = IU.Painel(pai, "comparar", new Color(0.04f, 0.04f, 0.035f, 0.9f));
            _dicaCompararCaixa = dc.rectTransform;
            _dicaCompararCaixa.pivot = new Vector2(0, 1);
            _dicaComparar = IU.Texto(_dicaCompararCaixa, "texto", "", 16, IU.CorFraca);
            _dicaComparar.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(320, 400));
            d.raycastTarget = false;
            dc.raycastTarget = false;
            foreach (Graphic g in d.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            foreach (Graphic g in dc.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            _dica.gameObject.SetActive(false);
            _dicaCompararCaixa.gameObject.SetActive(false);
            _cursorItem = IU.Imagem(pai, "segurando", Color.white);
            _cursorItem.raycastTarget = false;
            _cursorItem.preserveAspect = true;
            _cursorItem.gameObject.SetActive(false);
        }

        private Partida Partida => _ui.P.Partida;
        private Catalogo C => Partida.Catalogo;

        // ------------------------------------------------------------------ abrir e fechar

        public void Alternar(string nome)
        {
            if (_abertas.Contains(nome)) Fechar(nome);
            else Abrir(nome);
        }

        public void Abrir(string nome)
        {
            if (nome != "inventario" && nome != "loja" && nome != "guarda" && nome != "oficina")
                foreach (string outra in _abertas.ToList())
                    if (outra != "inventario") Fechar(outra);
            _abertas.Add(nome);
            _ui.P.Sons.UI("ui_abrir");
            _sujo = true;
            Reconstruir();
        }

        public void Fechar(string nome)
        {
            if (!_abertas.Remove(nome)) return;
            if (_janelas.TryGetValue(nome, out RectTransform r)) r.gameObject.SetActive(false);
            if (nome == "inventario") Soltar();
            if (nome == "poderes") _poderSegurado = null;
            if (nome == "loja") _loja = null;
            if (nome == "mapa") _mesaDosMapas = false;
            EsconderDica();
            _ui.P.Sons.UI("ui_fechar");
        }

        public void FecharTodas()
        {
            foreach (string n in _abertas.ToList()) Fechar(n);
        }

        public void AbrirLoja(string loja)
        {
            FecharTodas();
            _loja = loja;
            Abrir("loja");
            Abrir("inventario");
        }

        public void AbrirOficina(string estacao)
        {
            FecharTodas();
            _estacao = estacao;
            _receitaSelecionada = null;
            Abrir("oficina");
            Abrir("inventario");
        }

        public void AbrirGuarda()
        {
            FecharTodas();
            Abrir("guarda");
            Abrir("inventario");
        }

        public void AbrirMapa(bool mesa)
        {
            FecharTodas();
            _mesaDosMapas = mesa;
            Abrir("mapa");
        }

        public void AbrirRegistro(string id)
        {
            _registro = id;
            Abrir("registro");
        }

        public void Atualizar(float dt)
        {
            if (_segurando >= 0 || _poderSegurado != null)
            {
                _cursorItem.gameObject.SetActive(true);
                Vector2 pos = Controles.PosicaoDoMouse / _ui.Canvas.scaleFactor;
                _cursorItem.rectTransform.Lugar(Vector2.zero, new Vector2(0.5f, 0.5f), pos, new Vector2(56, 56));
                _cursorItem.transform.SetAsLastSibling();
            }
            else _cursorItem.gameObject.SetActive(false);
            if (_dica.gameObject.activeSelf) PosicionarDica();
            // Atalho: com o mouse em cima de um poder, 1..6 poe ele na barra.
            if (_poderSobMouse != null && _abertas.Contains("poderes"))
            {
                Acao[] teclas = { Acao.Barra1, Acao.Barra2, Acao.Barra3, Acao.Barra4, Acao.Barra5, Acao.Barra6 };
                for (int i = 0; i < teclas.Length; i++)
                    if (Controles.Desceu(teclas[i]) && Partida.Heroi.RankDe(_poderSobMouse) > 0)
                    {
                        Partida.ColocarNaBarra(2 + i, _poderSobMouse);
                        _ui.P.Sons.UI("equipar");
                    }
            }
            if (_sujo) Reconstruir();
        }

        public void Sujar() => _sujo = true;

        /// <summary>Para o relatorio do modo de teste: quais janelas existem, abertas e onde.</summary>
        public string Depurar()
        {
            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, RectTransform> kv in _janelas)
                sb.Append($"{kv.Key}: ativa={kv.Value.gameObject.activeInHierarchy} pos={kv.Value.anchoredPosition} tam={kv.Value.rect.size} filhos={kv.Value.childCount}; ");
            sb.Append("abertas=" + string.Join(",", _abertas));
            return sb.ToString();
        }

        private void Reconstruir()
        {
            _sujo = false;
            foreach (string n in _abertas.ToList())
            {
                switch (n)
                {
                    case "inventario": Inventario(); break;
                    case "personagem": Personagem(); break;
                    case "poderes": Poderes(); break;
                    case "diario": Diario(); break;
                    case "mapa": Mapa(); break;
                    case "loja": Loja(); break;
                    case "oficina": Oficina(); break;
                    case "guarda": Guarda(); break;
                    case "registro": Registro(); break;
                }
            }
        }

        /// <summary>A janela com o nome dado, limpa e pronta para desenhar de novo.</summary>
        private RectTransform Janela(string nome, string titulo, Vector2 ancora, Vector2 pos, Vector2 tam)
        {
            if (!_janelas.TryGetValue(nome, out RectTransform r))
            {
                Image p = IU.Painel(_raiz, nome);
                r = p.rectTransform;
                _janelas[nome] = r;
            }
            r.gameObject.SetActive(true);
            r.Lugar(ancora, ancora, pos, tam);
            for (int i = r.childCount - 1; i >= 1; i--) UnityEngine.Object.Destroy(r.GetChild(i).gameObject);
            Text t = IU.Texto(r, "titulo", titulo, 26, IU.Ambar, TextAnchor.UpperCenter);
            t.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(tam.x - 80, 34));
            Button x = IU.Botao(r, "fechar", "×", () => Fechar(nome), 26);
            x.GetComponent<RectTransform>().Lugar(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(36, 36));
            return r;
        }

        // ------------------------------------------------------------------ dica

        public void EsconderDica()
        {
            _dica.gameObject.SetActive(false);
            _dicaCompararCaixa.gameObject.SetActive(false);
        }

        private void MostrarDica(string texto, string comparar = null)
        {
            _dicaTexto.text = texto;
            _dica.gameObject.SetActive(true);
            _dica.SetAsLastSibling();
            _dica.sizeDelta = new Vector2(364, _dicaTexto.preferredHeight + 22);
            if (comparar != null)
            {
                _dicaComparar.text = comparar;
                _dicaCompararCaixa.gameObject.SetActive(true);
                _dicaCompararCaixa.SetAsLastSibling();
                _dicaCompararCaixa.sizeDelta = new Vector2(344, _dicaComparar.preferredHeight + 22);
            }
            else _dicaCompararCaixa.gameObject.SetActive(false);
            PosicionarDica();
        }

        private void PosicionarDica()
        {
            Vector2 m = Controles.PosicaoDoMouse / _ui.Canvas.scaleFactor;
            Vector2 tela = new Vector2(Screen.width, Screen.height) / _ui.Canvas.scaleFactor;
            Vector2 tam = _dica.sizeDelta;
            float x = m.x + 22, y = m.y - 10;
            if (x + tam.x > tela.x) x = m.x - tam.x - 22;
            if (y - tam.y < 0) y = tam.y + 4;
            _dica.anchorMin = _dica.anchorMax = Vector2.zero;
            _dica.anchoredPosition = new Vector2(x, y);
            if (_dicaCompararCaixa.gameObject.activeSelf)
            {
                float cx = x + tam.x + 6;
                if (cx + _dicaCompararCaixa.sizeDelta.x > tela.x) cx = x - _dicaCompararCaixa.sizeDelta.x - 6;
                _dicaCompararCaixa.anchorMin = _dicaCompararCaixa.anchorMax = Vector2.zero;
                _dicaCompararCaixa.anchoredPosition = new Vector2(cx, y);
            }
        }

        private string TextoDoItem(Item item, bool comPreco = false, bool venda = false)
        {
            ItemBase b = C.Item(item.Base);
            var sb = new System.Text.StringBuilder();
            sb.Append(IU.Cor($"<size=22><b>{DescricaoDeItem.Nome(item, C)}</b></size>", DescricaoDeItem.CorDaRaridade(item.Raridade)));
            if (item.Quantidade > 1) sb.Append($"  ×{item.Quantidade}");
            List<string> linhas = DescricaoDeItem.Linhas(item, C);
            for (int i = 0; i < linhas.Count; i++)
            {
                string l = linhas[i];
                sb.Append('\n');
                if (i == 0) sb.Append(IU.Cor(l, "#a8a090"));
                else if (l.StartsWith(T("Requer"), System.StringComparison.Ordinal))
                {
                    bool atende = Partida.Heroi.AtendeRequisitos(b, C, out _);
                    sb.Append(IU.Cor(l, atende ? "#a8a090" : "#ff6050"));
                }
                else if (l.StartsWith("“")) sb.Append(IU.Cor($"<i>{l}</i>", "#c0b090"));
                else if (l.StartsWith("+") || l.StartsWith("Usar")) sb.Append(IU.Cor(l, "#9fd0ff"));
                else sb.Append(l);
            }
            if (comPreco)
            {
                int preco = venda ? DescricaoDeItem.PrecoDeVenda(item, C) : DescricaoDeItem.PrecoDeCompra(new Item(item.Base) { Raridade = item.Raridade, Nivel = item.Nivel, Afixos = item.Afixos }, C);
                sb.Append('\n').Append(IU.Cor(venda ? $"Vende por {preco} de sucata" : $"Custa {preco} de sucata", "#e0c890"));
            }
            return sb.ToString();
        }

        private string Comparacao(Item item)
        {
            ItemBase b = C.Item(item.Base);
            if (!b.Equipavel) return null;
            SlotEquip? slot = Nucleo.Regras.Personagem.SlotPara(b);
            if (slot == null) return null;
            Item atual = Partida.Heroi.Equipado[(int)slot.Value];
            if (atual == null || atual == item) return null;
            return IU.Cor(T("EQUIPADO AGORA\n"), "#8a8070") + TextoDoItem(atual);
        }

        // ------------------------------------------------------------------ slots de item

        private Image SlotDeItem(Transform pai, Vector2 pos, float tam, Item item, Action clique, Action direito, Action entrar = null, string vazio = null)
        {
            Image fundo = IU.Painel(pai, "slot", new Color(0.07f, 0.07f, 0.065f, 0.95f));
            fundo.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(tam, tam));
            if (item != null)
            {
                Color raridade = Util.Cor(DescricaoDeItem.CorDaRaridade(item.Raridade));
                if (item.Raridade > Raridade.Comum)
                {
                    Image brilho = IU.Imagem(fundo.transform, "raridade", new Color(raridade.r, raridade.g, raridade.b, 0.18f));
                    brilho.rectTransform.Esticar(2);
                    brilho.raycastTarget = false;
                }
                Image icone = IU.Imagem(fundo.transform, "icone", Color.white);
                icone.sprite = _ui.Estudio.Icone(C.Item(item.Base));
                icone.preserveAspect = true;
                icone.rectTransform.Esticar(4);
                icone.raycastTarget = false;
                if (item.Quantidade > 1)
                {
                    Text q = IU.Texto(fundo.transform, "qtd", item.Quantidade.ToString(), 16, IU.CorTexto, TextAnchor.LowerRight);
                    q.rectTransform.Esticar(4);
                }
                if (!Partida.Heroi.AtendeRequisitos(C.Item(item.Base), C, out _))
                {
                    Image x = IU.Imagem(fundo.transform, "nao", new Color(0.7f, 0.1f, 0.05f, 0.35f));
                    x.rectTransform.Esticar(2);
                    x.raycastTarget = false;
                }
            }
            else if (vazio != null)
            {
                Text v = IU.Texto(fundo.transform, "vazio", vazio, 13, new Color(0.45f, 0.42f, 0.38f), TextAnchor.MiddleCenter);
                v.rectTransform.Esticar(2);
            }
            Gatilho g = IU.Eventos(fundo.gameObject);
            g.AoClicar = clique;
            g.AoClicarDireito = direito;
            g.AoEntrar = () =>
            {
                if (entrar != null) entrar();
                else if (item != null) MostrarDica(TextoDoItem(item), Comparacao(item));
            };
            g.AoSair = EsconderDica;
            return fundo;
        }

        private void Soltar()
        {
            _segurando = -1;
            _cursorItem.gameObject.SetActive(false);
        }

        private void Segurar(int espaco)
        {
            Item item = Partida.Heroi.Mochila.Espacos[espaco];
            if (item == null) return;
            _segurando = espaco;
            _cursorItem.sprite = _ui.Estudio.Icone(C.Item(item.Base));
            _ui.P.Sons.UI("ui_clique");
        }

        // ------------------------------------------------------------------ mochila

        private static readonly (SlotEquip slot, string rotulo, Vector2 pos)[] SlotsDeEquipamento =
        {
            (SlotEquip.Cabeca, T("cabeça"), new Vector2(150, -56)),
            (SlotEquip.Pingente, T("pingente"), new Vector2(236, -76)),
            (SlotEquip.Arma, T("arma"), new Vector2(40, -150)),
            (SlotEquip.Corpo, "corpo", new Vector2(150, -146)),
            (SlotEquip.Secundaria, T("mão 2"), new Vector2(260, -150)),
            (SlotEquip.Maos, T("mãos"), new Vector2(40, -240)),
            (SlotEquip.Bugiganga1, T("bugiganga"), new Vector2(150, -236)),
            (SlotEquip.Bugiganga2, T("bugiganga"), new Vector2(260, -240)),
            (SlotEquip.Pes, T("pés"), new Vector2(150, -326)),
        };

        private void Inventario()
        {
            RectTransform j = Janela("inventario", T("Mochila"), new Vector2(1, 0.5f), new Vector2(-30, 40), new Vector2(620, 860));
            j.pivot = new Vector2(1, 0.5f);
            Nucleo.Regras.Personagem h = Partida.Heroi;
            RectTransform eq = IU.Novo(j, "equipamento").Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(380, 420));
            Image silhueta = IU.Imagem(eq, "silhueta", new Color(1, 1, 1, 0.03f));
            silhueta.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -40), new Vector2(130, 380));
            foreach ((SlotEquip slot, string rotulo, Vector2 pos) in SlotsDeEquipamento)
            {
                Item item = h.Equipado[(int)slot];
                SlotEquip s = slot;
                SlotDeItem(eq, pos, 74, item,
                    () =>
                    {
                        if (_segurando >= 0)
                        {
                            int de = _segurando;
                            Soltar();
                            Partida.Equipar(de, s);
                        }
                        else if (item != null) Partida.Desequipar(s);
                        Sujar();
                    },
                    () =>
                    {
                        if (item != null) Partida.Desequipar(s);
                        Sujar();
                    }, null, rotulo);
            }
            // Resumo dos numeros que importam.
            Ficha f = Partida.Jogador?.Estat ?? h.Calcular(C);
            Text resumo = IU.Texto(j, "resumo",
                F("Vida {0}   Fôlego {1}   Absorção {2:0}–{3:0}   Esquiva {4:0}", Mathf.RoundToInt(f[Estat.Vida]), Mathf.RoundToInt(f[Estat.Energia]), f[Estat.AbsorcaoMin], f[Estat.AbsorcaoMax], f[Estat.Esquiva]),
                17, IU.CorFraca, TextAnchor.MiddleCenter);
            resumo.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(580, 26));

            float tam = 64, pad = 5;
            for (int i = 0; i < h.Mochila.Tamanho; i++)
            {
                int linha = i / 8, coluna = i % 8;
                int espaco = i;
                Item item = h.Mochila.Espacos[i];
                Image s = SlotDeItem(j, new Vector2(24 + coluna * (tam + pad), -504 - linha * (tam + pad)), tam, item,
                    () => CliqueNaMochila(espaco),
                    () => DireitoNaMochila(espaco),
                    () =>
                    {
                        if (item == null) return;
                        bool venda = _loja != null;
                        MostrarDica(TextoDoItem(item, venda, venda) + Rodape(item), Comparacao(item));
                    });
                if (_segurando == i) s.color = IU.Ambar;
            }
            Text sucata = IU.Texto(j, "sucata", F("⚙ {0} de sucata", h.Sucata), 20, new Color(0.95f, 0.8f, 0.45f));
            sucata.rectTransform.Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 16), new Vector2(300, 30));
            Button org = IU.Botao(j, "organizar", T("Organizar"), () =>
            {
                h.Mochila.Organizar(C);
                Sujar();
            }, 18);
            org.GetComponent<RectTransform>().Lugar(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 14), new Vector2(140, 36));
            if (_segurando >= 0)
            {
                Button fora = IU.Botao(j, "jogar", T("Jogar fora"), () =>
                {
                    int e = _segurando;
                    Soltar();
                    Partida.JogarFora(e);
                    Sujar();
                }, 18);
                fora.GetComponent<RectTransform>().Lugar(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-174, 14), new Vector2(140, 36));
            }
        }

        private string Rodape(Item item)
        {
            ItemBase b = C.Item(item.Base);
            string acao = _loja != null ? T("botão direito: vender") : _abertas.Contains("guarda") ? T("botão direito: guardar no baú") : b.Equipavel ? T("botão direito: equipar") : b.Uso != null ? T("botão direito: usar") : "";
            return acao.Length > 0 ? "\n" + IU.Cor($"<size=14>{acao} · clique: mover</size>", "#8a8070") : "";
        }

        private void CliqueNaMochila(int espaco)
        {
            if (_segurando >= 0)
            {
                Partida.Heroi.Mochila.Mover(_segurando, espaco, C);
                Soltar();
                Partida.Diario.Reavaliar();
            }
            else Segurar(espaco);
            Sujar();
        }

        private void DireitoNaMochila(int espaco)
        {
            Soltar();
            Item item = Partida.Heroi.Mochila.Espacos[espaco];
            if (item == null) return;
            if (_loja != null) Partida.Vender(_loja, espaco);
            else if (_abertas.Contains("guarda")) Partida.GuardarNaGuarda(espaco);
            else Partida.UsarItem(espaco);
            EsconderDica();
            Sujar();
        }

        // ------------------------------------------------------------------ barra

        public void DicaDaBarra(int espaco, RectTransform onde)
        {
            string id = Partida.Heroi.Barra[espaco];
            if (id == null)
            {
                MostrarDica(T("Espaço vazio.\n") + IU.Cor(T("<size=14>Na janela de poderes (K), passe o mouse num poder e aperte 1–6 para pôr aqui.</size>"), "#8a8070"));
                return;
            }
            if (id == "ataque")
            {
                Poder p = Partida.PoderBasico();
                MostrarDica(F("<b>{0}</b>\nO ataque da arma na mão.\n", p.Nome) + IU.Cor(T("clique esquerdo em um inimigo; Shift + clique ataca parado"), "#8a8070"));
                return;
            }
            if (C.Itens.TryGetValue(id, out ItemBase b))
            {
                MostrarDica(TextoDoItem(new Item(id)) + "\n" + IU.Cor(F("na mochila: {0}", Partida.Heroi.Mochila.Contar(id)), "#8a8070"));
                return;
            }
            MostrarDica(TextoDoPoder(C.Poder(id)));
        }

        public void CliqueNaBarra(int espaco, bool direito = false)
        {
            if (direito)
            {
                if (espaco > 0) Partida.Heroi.Barra[espaco] = null;
                return;
            }
            if (_poderSegurado != null)
            {
                Partida.ColocarNaBarra(espaco, _poderSegurado);
                _poderSegurado = null;
                _ui.P.Sons.UI("equipar");
                Sujar();
            }
            else if (_segurando >= 0)
            {
                Item item = Partida.Heroi.Mochila.Espacos[_segurando];
                if (item != null && C.Item(item.Base).Uso != null) Partida.ColocarNaBarra(espaco, item.Base);
                Soltar();
            }
        }

        // ------------------------------------------------------------------ personagem

        private void Personagem()
        {
            RectTransform j = Janela("personagem", T("Personagem"), new Vector2(0, 0.5f), new Vector2(30, 40), new Vector2(520, 820));
            j.pivot = new Vector2(0, 0.5f);
            Nucleo.Regras.Personagem h = Partida.Heroi;
            Ficha f = Partida.Jogador?.Estat ?? h.Calcular(C);
            Classe classe = C.Classe(h.Classe);
            Text cab = IU.Texto(j, "cab", F("<b>{0}</b>   {1}, nível {2}\n{3}", h.Nome, classe.Nome, h.Nivel, IU.Cor(classe.Papel, "#a8a090")), 21, IU.CorTexto);
            cab.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -58), new Vector2(470, 60));
            Text pontos = IU.Texto(j, "pontos", h.PontosDeAtributo > 0 ? IU.Cor(F("{0} pontos para distribuir", h.PontosDeAtributo), IU.Ambar) : "", 19, IU.CorTexto);
            pontos.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -120), new Vector2(470, 26));
            (Atributo a, string desc)[] atributos =
            {
                (Atributo.Fisico, T("vida, dano corpo a corpo")),
                (Atributo.Mente, T("fôlego, engenhocas, cura")),
                (Atributo.Ofensiva, T("precisão, crítico, dano à distância")),
                (Atributo.Defesa, T("absorção, esquiva, tenacidade")),
            };
            for (int i = 0; i < atributos.Length; i++)
            {
                Atributo a = atributos[i].a;
                float y = -158 - i * 58;
                int bonus = Mathf.RoundToInt(f[(Estat)(int)a]) - h.Atributos[(int)a];
                string valor = $"<size=30><b>{Mathf.RoundToInt(f[(Estat)(int)a])}</b></size>" + (bonus != 0 ? IU.Cor($" ({h.Atributos[(int)a]}{(bonus > 0 ? "+" : "")}{bonus})", "#9fd0ff") : "");
                Text t = IU.Texto(j, "attr", $"{Nomes.Exibir(a)}   {valor}\n{IU.Cor($"<size=15>{atributos[i].desc}</size>", "#8a8070")}", 22, IU.CorTexto);
                t.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, y), new Vector2(380, 56));
                if (h.PontosDeAtributo > 0)
                {
                    Button b = IU.Botao(j, "mais", "+", () =>
                    {
                        Partida.GastarAtributo(a);
                        Sujar();
                    }, 26);
                    b.GetComponent<RectTransform>().Lugar(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, y - 6), new Vector2(46, 42));
                }
            }
            var linhas = new List<string>
            {
                F("Vida {0}  ({1:0.0}/s)", Mathf.RoundToInt(f[Estat.Vida]), f[Estat.RegenVida]),
                F("Fôlego {0}  ({1:0.0}/s)", Mathf.RoundToInt(f[Estat.Energia]), f[Estat.RegenEnergia]),
                F("Dano corpo a corpo {0:0}–{1:0}", f[Estat.DanoCorpoMin] + (Partida.Jogador?.Arma?.Arma != ClasseArma.Distancia ? Partida.Jogador?.Arma?.DanoMin ?? 0 : 0), f[Estat.DanoCorpoMax] + (Partida.Jogador?.Arma?.Arma != ClasseArma.Distancia ? Partida.Jogador?.Arma?.DanoMax ?? 0 : 0)),
                F("Dano à distância {0:0}–{1:0}", f[Estat.DanoDistMin] + (Partida.Jogador?.Arma?.Arma == ClasseArma.Distancia ? Partida.Jogador.Arma.DanoMin : 0), f[Estat.DanoDistMax] + (Partida.Jogador?.Arma?.Arma == ClasseArma.Distancia ? Partida.Jogador.Arma.DanoMax : 0)),
                F("Dano de engenhoca {0:0}–{1:0}", f[Estat.DanoTecMin], f[Estat.DanoTecMax]),
                F("Precisão {0:0}   Esquiva {1:0}", f[Estat.Precisao], f[Estat.Esquiva]),
                F("Crítico {0:0.#}%   dano crítico ×{1:0.00}", f[Estat.Critico], C.Regras.MultiplicadorCritico + f[Estat.DanoCritico] / 100f),
                F("Absorção {0:0.#}–{1:0.#}   Bloqueio {2:0}%", f[Estat.AbsorcaoMin], f[Estat.AbsorcaoMax], f[Estat.Bloqueio]),
                F("Resistências: veneno {0:0}%  fogo {1:0}%  choque {2:0}%", f[Estat.ResVeneno], f[Estat.ResFogo], f[Estat.ResChoque]),
                F("Velocidade {0:0}%   Ataque {1:0}%", 100 + f[Estat.Velocidade], 100 + f[Estat.VelocidadeAtaque]),
            };
            if (f[Estat.RouboVida] > 0) linhas.Add(F("Roubo de vida {0:0.#}%", f[Estat.RouboVida]));
            if (f[Estat.ReducaoRecarga] > 0) linhas.Add(F("Recarga {0:0}% mais rápida", f[Estat.ReducaoRecarga]));
            if (f[Estat.AchadoRaro] > 0) linhas.Add(F("Achado raro +{0:0}%", f[Estat.AchadoRaro]));
            if (f[Estat.BonusXp] > 0) linhas.Add(F("Experiência +{0:0}%", f[Estat.BonusXp]));
            int xpProx = C.Regras.XpTotalPara(h.Nivel + 1);
            linhas.Add("");
            linhas.Add(IU.Cor(F("Experiência {0} / {1}", h.Xp, xpProx), "#e0c890"));
            linhas.Add(IU.Cor(F("Inimigos derrotados {0}   Noites lá fora {1}   Mortes {2}", Partida.Contador("inimigos_mortos"), Partida.Contador("noites_sobrevividas"), Partida.Contador("mortes")), "#8a8070"));
            Text est = IU.Texto(j, "estat", string.Join("\n", linhas), 18, IU.CorTexto);
            est.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -400), new Vector2(470, 400));
        }

        // ------------------------------------------------------------------ poderes

        private string _poderSobMouse;

        private string TextoDoPoder(Poder p)
        {
            Nucleo.Regras.Personagem h = Partida.Heroi;
            int rank = h.RankDe(p.Id);
            Poder ef = rank > 0 ? Partida.PoderDoHeroi(p.Id) : p;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=22><b>{p.Nome}</b></size>");
            if (p.RankMaximo > 1) sb.Append(IU.Cor($"  rank {rank}/{p.RankMaximo}", "#e0c890"));
            sb.Append('\n').Append(p.Descricao);
            var detalhes = new List<string>();
            if (ef.Custo > 0) detalhes.Add(F("{0:0} de fôlego", ef.Custo));
            if (ef.Recarga > 0) detalhes.Add(F("recarga {0:0.#}s", ef.Recarga));
            if (ef.Fonte != FonteDeDano.Nenhuma && ef.Tipo != TipoPoder.Reforco && ef.Tipo != TipoPoder.Invocacao)
                detalhes.Add(F("dano ×{0:0.00} ({1})", ef.Multiplicador, ef.Fonte == FonteDeDano.Corpo ? T("corpo a corpo") : ef.Fonte == FonteDeDano.Distancia ? T("distância") : T("engenhoca")));
            if (ef.Efeito != null) detalhes.Add($"{Hud.NomeDoEfeito(ef.Efeito.Nome).Item1} {ef.Efeito.Duracao:0.#}s");
            if (ef.Raio > 0) detalhes.Add(F("raio {0:0.#}m", ef.Raio));
            if (ef.ArmasExigidas.Count > 0) detalhes.Add(F("arma {0}", string.Join("/", ef.ArmasExigidas.Select(Partida.NomeDaClasseDeArma))));
            if (detalhes.Count > 0) sb.Append('\n').Append(IU.Cor(string.Join(" · ", detalhes), "#9fd0ff"));
            string motivo = h.MotivoParaNaoAprender(p);
            if (rank == 0) sb.Append('\n').Append(IU.Cor(motivo == null ? "Pode aprender agora." : "Ainda não: " + motivo, motivo == null ? "#8fe08f" : "#ff8060"));
            else if (rank < p.RankMaximo && motivo == null) sb.Append('\n').Append(IU.Cor("Pode subir de rank agora.", "#8fe08f"));
            if (rank > 0) sb.Append('\n').Append(IU.Cor("<size=14>com o mouse aqui, aperte 1–6 para pôr na barra; ou clique e depois clique na barra</size>", "#8a8070"));
            return sb.ToString();
        }

        private void Poderes()
        {
            RectTransform j = Janela("poderes", T("Poderes"), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(760, 820));
            Nucleo.Regras.Personagem h = Partida.Heroi;
            Text pts = IU.Texto(j, "pontos", h.PontosDePoder > 0 ? IU.Cor(F("{0} pontos de poder", h.PontosDePoder), IU.Ambar) : IU.Cor(T("sem pontos — ganhe nível"), "#8a8070"), 19, IU.CorTexto, TextAnchor.UpperCenter);
            pts.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(700, 26));
            string[] abas = { "corredor", "guardiao", "remendeiro" };
            for (int i = 0; i < abas.Length; i++)
            {
                string aba = abas[i];
                Button b = IU.Botao(j, "aba", C.Classes[aba].Nome, () =>
                {
                    _abaPoderes = aba;
                    Sujar();
                }, 20);
                b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 1) * 210, -86), new Vector2(200, 40));
                if (aba == _abaPoderes) b.GetComponent<Image>().color = IU.Ambar;
            }
            List<Poder> lista = C.Poderes.Values.Where(p => p.Arvore == _abaPoderes).ToList();
            RectTransform area = IU.Novo(j, "arvore").Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(700, 640));
            // Linhas de requisito primeiro, por tras dos nos.
            foreach (Poder p in lista.Where(x => x.RequerPoder != null))
            {
                Poder pai = C.Poder(p.RequerPoder);
                Vector2 a = PosNo(pai), b = PosNo(p);
                Image linha = IU.Imagem(area, "linha", h.RankDe(pai.Id) > 0 ? new Color(0.8f, 0.6f, 0.3f, 0.8f) : new Color(0.3f, 0.3f, 0.3f, 0.8f));
                linha.raycastTarget = false;
                Vector2 meio = (a + b) / 2;
                linha.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0.5f, 0.5f), meio, new Vector2(4, (b - a).magnitude));
                linha.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg - 90);
            }
            foreach (Poder p in lista) NoDePoder(area, p, h);
            // Comuns: o rolamento.
            Poder rol = C.Poder("rolamento");
            Text comum = IU.Texto(j, "comum", IconesDePoder.Simbolo("rolamento") + " " + F("{0} ({1}) — todos sabem.", rol.Nome, Controles.Nome(Acao.Rolar)) + " " + IU.Cor(T("Clique em cima de um poder e depois num espaço da barra para trocar."), "#8a8070"), 16, IU.CorFraca, TextAnchor.LowerCenter);
            comum.rectTransform.Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(700, 40));
        }

        private static Vector2 PosNo(Poder p) => new Vector2(175 + p.Coluna * 350, -80 - p.Linha * 150);

        private void NoDePoder(RectTransform area, Poder p, Nucleo.Regras.Personagem h)
        {
            int rank = h.RankDe(p.Id);
            bool pode = h.MotivoParaNaoAprender(p) == null;
            Vector2 pos = PosNo(p);
            Image no = IU.Painel(area, "no", rank > 0 ? new Color(0.12f, 0.1f, 0.07f, 0.98f) : new Color(0.06f, 0.06f, 0.06f, 0.95f));
            no.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0.5f, 0.5f), pos, new Vector2(300, 108));
            if (_poderSegurado == p.Id) no.color = IU.Ambar;
            Image icone = IU.Imagem(no.transform, "icone", rank > 0 ? IconesDePoder.CorDaArvore(p.Arvore) : new Color(0.25f, 0.25f, 0.25f));
            icone.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(72, 72));
            Text sim = IU.Texto(icone.transform, "sim", IconesDePoder.Simbolo(p.Id), 38, rank > 0 ? Color.white : new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter);
            sim.rectTransform.Esticar();
            Text nome = IU.Texto(no.transform, "nome", $"<b>{p.Nome}</b>\n{IU.Cor(F("rank {0}/{1}", rank, p.RankMaximo), rank > 0 ? "#e0c890" : "#7a7060")}   {IU.Cor(F("nív. {0}", p.NivelMinimo), h.Nivel >= p.NivelMinimo ? "#8a8070" : "#c06050")}", 18, IU.CorTexto);
            nome.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -12), new Vector2(160, 80));
            if (pode)
            {
                Button b = IU.Botao(no.transform, "aprender", "+", () =>
                {
                    Partida.AprenderPoder(p.Id);
                    _ui.P.Sons.UI("nivel");
                    Sujar();
                }, 26);
                b.GetComponent<RectTransform>().Lugar(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(42, 42));
            }
            Gatilho g = IU.Eventos(no.gameObject);
            g.AoEntrar = () =>
            {
                _poderSobMouse = p.Id;
                MostrarDica(TextoDoPoder(p));
            };
            g.AoSair = () =>
            {
                if (_poderSobMouse == p.Id) _poderSobMouse = null;
                EsconderDica();
            };
            g.AoClicar = () =>
            {
                if (rank <= 0) return;
                _poderSegurado = _poderSegurado == p.Id ? null : p.Id;
                Sujar();
            };
        }
    }
}
