using UnityEngine;
using UnityEngine.InputSystem;

namespace Esquecidos.Motor.Entrada
{
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

        public static bool Shift => Segurado(Key.LeftShift) || Segurado(Key.RightShift);
        public static bool Alt => Segurado(Key.LeftAlt) || Segurado(Key.RightAlt);
        public static bool Ctrl => Segurado(Key.LeftCtrl) || Segurado(Key.RightCtrl);

        /// <summary>WASD, setas ou analogico esquerdo, ja no circulo unitario.</summary>
        public static Vector2 Movimento()
        {
            Vector2 v = Vector2.zero;
            if (Segurado(Key.W) || Segurado(Key.UpArrow)) v.y += 1;
            if (Segurado(Key.S) || Segurado(Key.DownArrow)) v.y -= 1;
            if (Segurado(Key.D) || Segurado(Key.RightArrow)) v.x += 1;
            if (Segurado(Key.A) || Segurado(Key.LeftArrow)) v.x -= 1;
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
