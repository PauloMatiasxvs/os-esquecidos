using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo.Atores
{
    /// <summary>Escolhe e monta o corpo de cada entidade a partir do "modelo" dos dados.</summary>
    public static class FabricaDeAtores
    {
        public static VisualDeEntidade Criar(Entidade e, Partida partida, Transform pai)
        {
            var g = new GameObject(e.Nome ?? e.Tipo);
            g.transform.SetParent(pai, false);
            VisualDeEntidade v;
            if (e == partida.Jogador || (e.Tipo == "jogador"))
            {
                var h = g.AddComponent<Humanoide>();
                h.Dados = DadosDoJogador(partida);
                h.Dados.Tipo = "jogador";
                v = h;
            }
            else if (e.Def == null && e.NpcId != null)
            {
                var h = g.AddComponent<Humanoide>();
                h.Dados = DadosDoNpc(partida.Catalogo, e.NpcId);
                v = h;
            }
            else
            {
                v = PorModelo(g, e, partida.Catalogo);
            }
            v.Ligar(e);
            return v;
        }

        private static VisualDeEntidade PorModelo(GameObject g, Entidade e, Catalogo c)
        {
            InimigoDef d = e.Def;
            string[] partes = (d?.Modelo ?? "boneco").Split(':');
            string familia = partes[0];
            string variante = partes.Length > 1 ? partes[1] : "";
            float escala = (d?.Escala ?? 1f) * (e.Elite ? 1.15f : 1f);
            Color cor = Util.Cor(d?.Cor, new Color(0.5f, 0.5f, 0.5f));
            Color olhos = Util.Cor(d?.CorOlhos, Color.red);
            if (e.Elite) olhos = Color.Lerp(olhos, new Color(1f, 0.85f, 0.2f), 0.6f);
            switch (familia)
            {
                case "tecelao":
                {
                    var t = g.AddComponent<Tecelao>();
                    t.Variante = variante;
                    t.CorCorpo = cor;
                    t.CorOlhos = olhos;
                    t.Escala = escala;
                    return t;
                }
                case "drone":
                {
                    var t = g.AddComponent<Drone>();
                    t.Variante = variante;
                    t.CorCorpo = cor;
                    t.CorOlho = olhos;
                    t.Escala = escala;
                    return t;
                }
                case "mech":
                {
                    var t = g.AddComponent<Mech>();
                    t.Variante = variante;
                    t.CorCorpo = cor;
                    t.CorOlho = olhos;
                    t.Escala = escala;
                    return t;
                }
                case "humano":
                {
                    var h = g.AddComponent<Humanoide>();
                    if (variante == "npc" && partes.Length > 2)
                    {
                        h.Dados = DadosDoNpc(c, partes[2]);
                    }
                    else
                    {
                        h.Dados = DadosDeInimigoHumano(variante, cor, olhos, e.Id);
                    }
                    h.Escala = escala;
                    return h;
                }
                default:
                    return g.AddComponent<Boneco>();
            }
        }

        public static DadosDeHumano DadosDoJogador(Partida p)
        {
            Personagem h = p.Heroi;
            Catalogo c = p.Catalogo;
            var d = new DadosDeHumano { Ap = h.Aparencia, Tipo = "jogador" };
            Item arma = h.Equipado[(int)SlotEquip.Arma];
            if (arma != null)
            {
                ItemBase b = c.Item(arma.Base);
                d.Arma = b.Modelo;
                d.CorArma = Util.Cor(b.Cor);
            }
            Item sec = h.Equipado[(int)SlotEquip.Secundaria];
            if (sec != null)
            {
                ItemBase b = c.Item(sec.Base);
                d.Escudo = b.Modelo;
                d.CorEscudo = Util.Cor(b.Cor);
            }
            Item cab = h.Equipado[(int)SlotEquip.Cabeca];
            if (cab != null)
            {
                ItemBase b = c.Item(cab.Base);
                d.Chapeu = b.Modelo;
                d.CorChapeu = Util.Cor(b.Cor);
            }
            Item corpo = h.Equipado[(int)SlotEquip.Corpo];
            if (corpo != null)
            {
                ItemBase b = c.Item(corpo.Base);
                if (b.Modelo != "camisa")
                {
                    d.Veste = b.Modelo;
                    d.CorVeste = Util.Cor(b.Cor);
                }
            }
            Item maos = h.Equipado[(int)SlotEquip.Maos];
            if (maos != null) d.CorLuvas = Util.Cor(c.Item(maos.Base).Cor);
            Item pes = h.Equipado[(int)SlotEquip.Pes];
            if (pes != null) d.CorBotas = Util.Cor(c.Item(pes.Base).Cor);
            return d;
        }

        public static DadosDeHumano DadosDoNpc(Catalogo c, string npc)
        {
            NpcDef def = c.Npc(npc);
            var d = new DadosDeHumano { Ap = def.Aparencia, Tipo = "npc" };
            switch (def.Aparencia.Acessorio)
            {
                case "lanca":
                    d.Arma = "lanca";
                    d.CorArma = new Color(0.6f, 0.6f, 0.6f);
                    break;
            }
            if (npc == "marreta")
            {
                d.Arma = "marreta";
                d.CorArma = new Color(0.45f, 0.45f, 0.48f);
            }
            if (npc == "teo")
            {
                d.Arma = "machete";
                d.CorArma = new Color(0.62f, 0.64f, 0.66f);
            }
            if (npc == "iara")
            {
                d.Arma = "estilingue";
                d.CorArma = new Color(0.45f, 0.3f, 0.18f);
            }
            return d;
        }

        private static DadosDeHumano DadosDeInimigoHumano(string variante, Color cor, Color olhos, int semente)
        {
            var rng = new System.Random(semente);
            var ap = new Aparencia
            {
                Pele = Util.Hex(cor),
                Cabelo = Util.Hex(new Color(0.18f, 0.17f, 0.16f)),
                Penteado = rng.NextDouble() < 0.5 ? "baguncado" : "raspado",
                Roupa = Util.Hex(Util.Variar(new Color(0.32f, 0.3f, 0.28f), rng, 0.3f)),
                Calca = Util.Hex(Util.Variar(new Color(0.22f, 0.22f, 0.25f), rng, 0.3f)),
                Botas = "#1e1a16",
            };
            var d = new DadosDeHumano { Ap = ap, Tipo = variante, CorOlhos = olhos };
            switch (variante)
            {
                case "bruto":
                    ap.Porte = 1.35f;
                    ap.Penteado = "careca";
                    break;
                case "guarda":
                    ap.Roupa = Util.Hex(cor);
                    ap.Calca = Util.Hex(Util.Escurecer(cor, 0.6f));
                    ap.Botas = "#2a2c30";
                    d.Veste = "peitoral";
                    d.CorVeste = cor;
                    d.Arma = "bastao";
                    d.CorArma = new Color(0.85f, 0.87f, 0.9f);
                    d.Escudo = "escudo_antimotim";
                    d.CorEscudo = new Color(0.85f, 0.88f, 0.92f);
                    break;
                case "atirador":
                    ap.Roupa = Util.Hex(cor);
                    ap.Calca = Util.Hex(Util.Escurecer(cor, 0.6f));
                    ap.Botas = "#2a2c30";
                    d.Veste = "peitoral";
                    d.CorVeste = cor;
                    d.Arma = "rifle";
                    d.CorArma = Color.white;
                    break;
            }
            return d;
        }
    }
}
