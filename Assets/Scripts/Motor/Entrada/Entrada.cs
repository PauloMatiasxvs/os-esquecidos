using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Esquecidos.Motor.Entrada
{
    /// <summary>O que o jogador quer fazer; cada acao tem uma tecla que ele pode trocar.</summary>
    public enum Acao
    {
        Cima,
        Baixo,
        Esquerda,
        Direita,
        Interagir,
        Rolar,
        AtacarParado,
        MostrarItens,
        GirarEsquerda,
        GirarDireita,
        Barra1,
        Barra2,
        Barra3,
        Barra4,
        Barra5,
        Barra6,
        Mochila,
        Personagem,
        Poderes,
        Diario,
        Mapa,
        Salvar,
        Carregar,
    }

    /// <summary>
    /// Teclado, mouse e controle num lugar so, como o InputManager do Pal3.
    /// O jogo pergunta "o jogador quer atacar?" e nao "a tecla tal desceu?".
    /// </summary>
    public static class Controles
    {
        /// <summary>Modo de teste: o jogo roda sozinho e ignora teclado, mouse e controle de quem estiver no PC.</summary>
        public static bool Ignorar;

        private static Mouse M => Ignorar ? null : Mouse.current;
        private static Keyboard T => Ignorar ? null : Keyboard.current;

        public static Vector2 PosicaoDoMouse => M?.position.ReadValue() ?? Vector2.zero;
        public static float Roda => M != null ? M.scroll.ReadValue().y : 0f;

        public static bool CliqueEsquerdoDesceu => M?.leftButton.wasPressedThisFrame ?? false;
        public static bool CliqueEsquerdoSegurado => M?.leftButton.isPressed ?? false;
        public static bool CliqueEsquerdoSubiu => M?.leftButton.wasReleasedThisFrame ?? false;
        public static bool CliqueDireitoDesceu => M?.rightButton.wasPressedThisFrame ?? false;
        public static bool CliqueDireitoSegurado => M?.rightButton.isPressed ?? false;

        public static bool Desceu(Key k) => T != null && T[k].wasPressedThisFrame;
        public static bool Segurado(Key k) => T != null && T[k].isPressed;

        public static bool Shift => Segurado(Acao.AtacarParado) || Segurado(Key.RightShift);
        public static bool Alt => Segurado(Acao.MostrarItens) || Segurado(Key.RightAlt);
        public static bool Ctrl => Segurado(Key.LeftCtrl) || Segurado(Key.RightCtrl);

        // ------------------------------------------------------------ teclas trocaveis

        private static readonly Dictionary<Acao, Key> Padrao = new Dictionary<Acao, Key>
        {
            { Acao.Cima, Key.W }, { Acao.Baixo, Key.S }, { Acao.Esquerda, Key.A }, { Acao.Direita, Key.D },
            { Acao.Interagir, Key.F }, { Acao.Rolar, Key.Space }, { Acao.AtacarParado, Key.LeftShift }, { Acao.MostrarItens, Key.LeftAlt },
            { Acao.GirarEsquerda, Key.Q }, { Acao.GirarDireita, Key.E },
            { Acao.Barra1, Key.Digit1 }, { Acao.Barra2, Key.Digit2 }, { Acao.Barra3, Key.Digit3 },
            { Acao.Barra4, Key.Digit4 }, { Acao.Barra5, Key.Digit5 }, { Acao.Barra6, Key.Digit6 },
            { Acao.Mochila, Key.I }, { Acao.Personagem, Key.C }, { Acao.Poderes, Key.K }, { Acao.Diario, Key.J }, { Acao.Mapa, Key.M },
            { Acao.Salvar, Key.F5 }, { Acao.Carregar, Key.F9 },
        };

        private static readonly Dictionary<Acao, Key> _teclas = new Dictionary<Acao, Key>(Padrao);

        public static Key Tecla(Acao a) => _teclas[a];

        public static bool Desceu(Acao a) => Desceu(_teclas[a]);

        public static bool Segurado(Acao a) => Segurado(_teclas[a]);

        /// <summary>Troca a tecla da acao; se outra acao usava essa tecla, as duas trocam entre si.</summary>
        public static void Definir(Acao a, Key k)
        {
            Key antiga = _teclas[a];
            foreach (Acao outra in new List<Acao>(_teclas.Keys))
                if (outra != a && _teclas[outra] == k) _teclas[outra] = antiga;
            _teclas[a] = k;
        }

        public static void Restaurar()
        {
            foreach (KeyValuePair<Acao, Key> kv in Padrao) _teclas[kv.Key] = kv.Value;
        }

        /// <summary>"Cima:W,Baixo:S,..." para o arquivo de opcoes.</summary>
        public static string Serializar()
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<Acao, Key> kv in _teclas)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            return sb.ToString();
        }

        public static void Ler(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;
            foreach (string par in texto.Split(','))
            {
                string[] p = par.Split(':');
                if (p.Length == 2 && Enum.TryParse(p[0], out Acao a) && Enum.TryParse(p[1], out Key k) && k != Key.None) _teclas[a] = k;
            }
        }

        /// <summary>O nome da tecla como esta no teclado de quem joga (o layout dele).</summary>
        public static string Nome(Key k)
        {
            Keyboard t = Keyboard.current;
            string n = t != null ? t[k].displayName : null;
            return string.IsNullOrEmpty(n) ? k.ToString() : n.ToUpperInvariant();
        }

        public static string Nome(Acao a) => Nome(_teclas[a]);

        /// <summary>A tecla que desceu neste quadro (para trocar uma tecla nas opcoes); Esc nao conta.</summary>
        public static Key? TeclaQueDesceu()
        {
            Keyboard t = T;
            if (t == null) return null;
            foreach (KeyControl c in t.allKeys)
                if (c != null && c.wasPressedThisFrame && c.keyCode != Key.Escape) return c.keyCode;
            return null;
        }

        /// <summary>WASD (ou o que o jogador escolheu), setas ou analogico esquerdo, ja no circulo unitario.</summary>
        public static Vector2 Movimento()
        {
            Vector2 v = Vector2.zero;
            if (Segurado(Acao.Cima) || Segurado(Key.UpArrow)) v.y += 1;
            if (Segurado(Acao.Baixo) || Segurado(Key.DownArrow)) v.y -= 1;
            if (Segurado(Acao.Direita) || Segurado(Key.RightArrow)) v.x += 1;
            if (Segurado(Acao.Esquerda) || Segurado(Key.LeftArrow)) v.x -= 1;
            Gamepad g = Controle;
            if (g != null)
            {
                Vector2 s = g.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) v += s;
            }
            return v.sqrMagnitude > 1 ? v.normalized : v;
        }

        public static Vector2 MiraDoControle()
        {
            Gamepad g = Controle;
            if (g == null) return Vector2.zero;
            Vector2 s = g.rightStick.ReadValue();
            return s.sqrMagnitude > 0.09f ? s : Vector2.zero;
        }

        public static bool TemControle => Controle != null;
        public static Gamepad Controle => Ignorar ? null : Gamepad.current;
    }
}
