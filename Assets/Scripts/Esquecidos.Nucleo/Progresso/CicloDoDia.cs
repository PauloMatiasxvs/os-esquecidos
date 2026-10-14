using System;

namespace Esquecidos.Nucleo.Progresso
{
    /// <summary>
    /// O relogio do labirinto. De dia os portoes abrem; ao anoitecer eles
    /// fecham, quem ficou do lado de fora fica, e de madrugada os muros andam.
    /// O dia passa mais devagar que a noite: a noite e curta e perigosa.
    /// </summary>
    public sealed class CicloDoDia
    {
        public const float Amanhecer = 6f;
        public const float AvisoDeFechamento = 18.5f;
        public const float Fechamento = 19.5f;
        public const float Anoitecer = 20f;

        /// <summary>Segundos reais que o dia (6h as 20h) dura.</summary>
        public float SegundosDeDia = 720f;
        /// <summary>Segundos reais que a noite (20h as 6h) dura.</summary>
        public float SegundosDeNoite = 240f;

        public int Dia { get; private set; } = 1;
        public float Hora { get; private set; } = 7f;
        public bool Parado { get; set; }

        public bool Noite => Hora >= Anoitecer || Hora < Amanhecer;
        public bool PortoesAbertos => Hora >= Amanhecer && Hora < Fechamento;

        public event Action Amanheceu;
        public event Action AvisoDePortao;
        public event Action PortoesFecharam;
        public event Action Anoiteceu;
        /// <summary>De hora em hora durante a noite: e quando os muros se mexem.</summary>
        public event Action<int> HoraDaNoite;

        public void Definir(int dia, float hora)
        {
            Dia = Math.Max(1, dia);
            Hora = ((hora % 24) + 24) % 24;
        }

        public void Avancar(float dt)
        {
            if (Parado || dt <= 0) return;
            float porSegundo = Noite ? 10f / SegundosDeNoite : 14f / SegundosDeDia;
            float antes = Hora;
            float depois = Hora + dt * porSegundo;
            if (depois >= 24f)
            {
                depois -= 24f;
                Dia++;
            }
            Hora = depois;
            Cruzou(antes, depois, AvisoDeFechamento, AvisoDePortao);
            Cruzou(antes, depois, Fechamento, PortoesFecharam);
            Cruzou(antes, depois, Anoitecer, Anoiteceu);
            Cruzou(antes, depois, Amanhecer, Amanheceu);
            if (Noite)
            {
                int ha = (int)MathF.Floor(antes), hd = (int)MathF.Floor(depois);
                if (ha != hd) HoraDaNoite?.Invoke(hd);
            }
        }

        private static void Cruzou(float antes, float depois, float marco, Action evento)
        {
            bool cruzou = depois >= antes ? antes < marco && depois >= marco : antes < marco || depois >= marco;
            if (cruzou) evento?.Invoke();
        }

        /// <summary>Dormir: pula para o amanhecer seguinte (o dia muda se passar da meia-noite).</summary>
        public void DormirAteAmanhecer()
        {
            if (Hora >= Amanhecer) Dia++;
            Hora = Amanhecer;
            Amanheceu?.Invoke();
        }

        public void AvancarHoras(float horas)
        {
            float alvo = Hora + horas;
            while (alvo >= 24)
            {
                alvo -= 24;
                Dia++;
            }
            Hora = alvo;
        }

        public string Relogio()
        {
            int h = (int)Hora;
            int min = (int)((Hora - h) * 60);
            return $"Dia {Dia} · {h:00}:{min:00}";
        }

        /// <summary>0 a meia-noite, 1 ao meio-dia: o quanto o sol esta alto, para a luz.</summary>
        public float Sol()
        {
            return MathF.Max(0, MathF.Sin((Hora - 6f) / 14f * MathF.PI));
        }
    }
}
