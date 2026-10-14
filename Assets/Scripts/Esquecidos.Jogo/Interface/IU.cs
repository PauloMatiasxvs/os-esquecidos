using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Esquecidos.Jogo.Interface
{
    /// <summary>
    /// Pecas de interface montadas em codigo: painel, texto, botao, barra,
    /// espaco de item. Visual do jogo: chapa escura com borda de ferrugem,
    /// texto creme, destaque ambar.
    /// </summary>
    public static class IU
    {
        public static readonly Color Fundo = new Color(0.055f, 0.06f, 0.058f, 0.92f);
        public static readonly Color FundoClaro = new Color(0.11f, 0.115f, 0.11f, 0.95f);
        public static readonly Color Borda = new Color(0.42f, 0.36f, 0.26f, 1f);
        public static readonly Color CorTexto = new Color(0.91f, 0.886f, 0.816f, 1f);
        public static readonly Color CorFraca = new Color(0.62f, 0.6f, 0.55f, 1f);
        public static readonly Color Ambar = new Color(1f, 0.69f, 0.25f, 1f);
        public static readonly Color Vida = new Color(0.78f, 0.17f, 0.14f, 1f);
        public static readonly Color Folego = new Color(0.25f, 0.55f, 0.82f, 1f);
        public static readonly Color Xp = new Color(0.85f, 0.7f, 0.25f, 1f);

        private static Font _fonte;
        private static Sprite _branco;

        public static Font Fonte
        {
            get
            {
                if (_fonte != null) return _fonte;
                _fonte = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Segoe UI Symbol", "Arial" }, 32);
                if (_fonte == null) _fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _fonte;
            }
        }

        /// <summary>Sprite branco 1x1: toda cor de painel e barra sai dele.</summary>
        public static Sprite Branco
        {
            get
            {
                if (_branco != null) return _branco;
                var t = new Texture2D(4, 4);
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                t.SetPixels(px);
                t.Apply();
                _branco = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
                return _branco;
            }
        }

        public static RectTransform Rect(GameObject g) => (RectTransform)g.transform;

        public static RectTransform Novo(Transform pai, string nome)
        {
            var g = new GameObject(nome, typeof(RectTransform));
            g.transform.SetParent(pai, false);
            return Rect(g);
        }

        /// <summary>Ancora e posicao num gesto: ancora (0..1), pivo, posicao em pixels, tamanho.</summary>
        public static RectTransform Lugar(this RectTransform r, Vector2 ancora, Vector2 pivo, Vector2 pos, Vector2 tam)
        {
            r.anchorMin = r.anchorMax = ancora;
            r.pivot = pivo;
            r.anchoredPosition = pos;
            r.sizeDelta = tam;
            return r;
        }

        public static RectTransform Esticar(this RectTransform r, float margem = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(margem, margem);
            r.offsetMax = new Vector2(-margem, -margem);
            return r;
        }

        public static Image Imagem(Transform pai, string nome, Color cor)
        {
            RectTransform r = Novo(pai, nome);
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = Branco;
            i.color = cor;
            i.type = Image.Type.Sliced;
            return i;
        }

        /// <summary>Painel com fundo escuro e borda fina.</summary>
        public static Image Painel(Transform pai, string nome, Color? fundo = null, bool borda = true)
        {
            Image b = Imagem(pai, nome, borda ? Borda : Color.clear);
            if (!borda) b.color = fundo ?? Fundo;
            if (borda)
            {
                Image f = Imagem(b.transform, "fundo", fundo ?? Fundo);
                f.rectTransform.Esticar(1.5f);
                f.raycastTarget = false;
            }
            return b;
        }

        public static Text Texto(Transform pai, string nome, string conteudo, int tamanho, Color? cor = null, TextAnchor alinhamento = TextAnchor.UpperLeft)
        {
            RectTransform r = Novo(pai, nome);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Fonte;
            t.fontSize = tamanho;
            t.color = cor ?? CorTexto;
            t.alignment = alinhamento;
            t.text = conteudo;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var s = r.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.75f);
            s.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public static Button Botao(Transform pai, string nome, string rotulo, Action clique, int tamanho = 22)
        {
            Image fundo = Painel(pai, nome, FundoClaro);
            var b = fundo.gameObject.AddComponent<Button>();
            ColorBlock cores = b.colors;
            cores.normalColor = Color.white;
            cores.highlightedColor = new Color(1.6f, 1.35f, 0.9f);
            cores.pressedColor = new Color(0.8f, 0.7f, 0.5f);
            cores.selectedColor = Color.white;
            cores.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cores.colorMultiplier = 1.5f;
            b.colors = cores;
            b.targetGraphic = fundo;
            b.onClick.AddListener(() =>
            {
                Principal.Instancia?.Sons.UI("ui_clique");
                clique?.Invoke();
            });
            Text t = Texto(fundo.transform, "rotulo", rotulo, tamanho, CorTexto, TextAnchor.MiddleCenter);
            t.rectTransform.Esticar(4);
            return b;
        }

        /// <summary>Liga ou desliga o botao, apagando o rotulo junto (o tom do Button so pinta a borda).</summary>
        public static void Ativo(Button b, bool sim)
        {
            b.interactable = sim;
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.color = sim ? CorTexto : new Color(CorFraca.r, CorFraca.g, CorFraca.b, 0.55f);
        }

        /// <summary>Barra de preenchimento horizontal. Devolve a imagem do preenchimento (fillAmount).</summary>
        public static Image Barra(Transform pai, string nome, Color cor, out Text rotulo, int tamanhoTexto = 14)
        {
            Image fundo = Painel(pai, nome, new Color(0.03f, 0.03f, 0.03f, 0.85f));
            Image cheio = Imagem(fundo.transform, "cheio", cor);
            cheio.rectTransform.Esticar(2);
            cheio.type = Image.Type.Filled;
            cheio.fillMethod = Image.FillMethod.Horizontal;
            cheio.fillOrigin = 0;
            cheio.raycastTarget = false;
            Image brilho = Imagem(cheio.transform, "brilho", new Color(1, 1, 1, 0.12f));
            brilho.rectTransform.anchorMin = new Vector2(0, 0.55f);
            brilho.rectTransform.anchorMax = new Vector2(1, 1);
            brilho.rectTransform.offsetMin = brilho.rectTransform.offsetMax = Vector2.zero;
            brilho.raycastTarget = false;
            rotulo = Texto(fundo.transform, "rotulo", "", tamanhoTexto, CorTexto, TextAnchor.MiddleCenter);
            rotulo.rectTransform.Esticar(0);
            return cheio;
        }

        /// <summary>Componente para reagir a passar o mouse e clicar com cada botao.</summary>
        public static Gatilho Eventos(GameObject g)
        {
            Gatilho e = g.GetComponent<Gatilho>();
            return e != null ? e : g.AddComponent<Gatilho>();
        }

        public static string Cor(string texto, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{texto}</color>";

        public static string Cor(string texto, string hex) => $"<color={hex}>{texto}</color>";
    }

    /// <summary>Entrar, sair e clicar (esquerdo, direito) num elemento da interface.</summary>
    public sealed class Gatilho : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action AoEntrar, AoSair, AoClicar, AoClicarDireito;

        public void OnPointerEnter(PointerEventData e) => AoEntrar?.Invoke();
        public void OnPointerExit(PointerEventData e) => AoSair?.Invoke();

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) AoClicarDireito?.Invoke();
            else if (e.button == PointerEventData.InputButton.Left) AoClicar?.Invoke();
        }
    }
}
