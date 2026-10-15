using System;
using System.Collections.Generic;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Nucleo.Regras
{
    /// <summary>
    /// O jogador fora de qualquer mapa: o que sobrevive a troca de cena e vai
    /// para o arquivo salvo. A entidade no mundo e so o corpo dele naquele mapa.
    /// </summary>
    public sealed class Personagem
    {
        public const int TamanhoDaMochila = 40;
        public const int TamanhoDaBarra = 8;

        public string Nome = T("Novato");
        public string Classe;
        public int Nivel = 1;
        public int Xp;
        public int[] Atributos = new int[4];
        public int PontosDeAtributo;
        public int PontosDePoder;
        public int Sucata;
        public Dictionary<string, int> Poderes = new Dictionary<string, int>();
        public Item[] Equipado = new Item[Enum.GetValues(typeof(SlotEquip)).Length];
        public Inventario Mochila = new Inventario(TamanhoDaMochila);
        public string[] Barra = new string[TamanhoDaBarra];
        public Aparencia Aparencia = new Aparencia();

        public static Personagem Novo(Catalogo c, string classeId, string nome)
        {
            Classe classe = c.Classe(classeId);
            var p = new Personagem { Nome = nome, Classe = classeId };
            Array.Copy(classe.Atributos, p.Atributos, 4);
            foreach (string poder in classe.Poderes) p.Poderes[poder] = 1;
            foreach (string id in classe.Equipamento)
            {
                var item = new Item(id) { Nivel = c.Item(id).Nivel };
                SlotEquip? slot = SlotPara(c.Item(id), p);
                if (slot.HasValue && p.Equipado[(int)slot.Value] == null) p.Equipado[(int)slot.Value] = item;
                else p.Mochila.Adicionar(item, c);
            }
            foreach ((string item, int qtd) in classe.Mochila) p.Mochila.Adicionar(new Item(item, qtd), c);
            Array.Copy(classe.Barra, p.Barra, Math.Min(classe.Barra.Length, p.Barra.Length));
            p.Aparencia.Roupa = classe.CorRoupa;
            return p;
        }

        public ItemBase Arma(Catalogo c)
        {
            Item a = Equipado[(int)SlotEquip.Arma];
            return a == null ? null : c.Item(a.Base);
        }

        public int RankDe(string poder) => Poderes.TryGetValue(poder, out int r) ? r : 0;

        /// <summary>Soma de tudo que esta equipado, pronto para Regras.Calcular.</summary>
        public Ficha BonusDeEquipamento(Catalogo c)
        {
            var f = new Ficha();
            foreach (Item i in Equipado)
                if (i != null) DescricaoDeItem.SomarBonus(i, c, f);
            return f;
        }

        public Ficha Calcular(Catalogo c)
        {
            return c.Regras.Calcular(Nivel, Atributos, BonusDeEquipamento(c));
        }

        /// <summary>Ganha experiencia e sobe quantos niveis couberem. Devolve quantos subiu.</summary>
        public int GanharXp(int quantidade, Regras regras)
        {
            if (Nivel >= regras.NivelMaximo) return 0;
            Xp += Math.Max(0, quantidade);
            int subiu = 0;
            while (Nivel < regras.NivelMaximo && Xp >= regras.XpTotalPara(Nivel + 1))
            {
                Nivel++;
                subiu++;
                PontosDeAtributo += regras.PontosAtributoPorNivel;
                PontosDePoder += regras.PontosPoderPorNivel;
            }
            return subiu;
        }

        public bool GastarAtributo(Atributo a, Regras regras)
        {
            if (PontosDeAtributo <= 0 || Atributos[(int)a] >= regras.AtributoMaximo) return false;
            Atributos[(int)a]++;
            PontosDeAtributo--;
            return true;
        }

        /// <summary>Por que esse poder nao pode receber ponto agora (null se pode).</summary>
        public string MotivoParaNaoAprender(Poder p)
        {
            int rank = RankDe(p.Id);
            if (rank >= p.RankMaximo) return T("no máximo");
            if (PontosDePoder <= 0) return T("sem pontos de poder");
            if (Nivel < p.NivelMinimo + rank * 2 && rank > 0) return F("requer nível {0}", p.NivelMinimo + rank * 2);
            if (Nivel < p.NivelMinimo) return F("requer nível {0}", p.NivelMinimo);
            if (p.RequerPoder != null && RankDe(p.RequerPoder) == 0) return T("requer o poder anterior");
            return null;
        }

        public bool Aprender(Poder p)
        {
            if (MotivoParaNaoAprender(p) != null) return false;
            Poderes[p.Id] = RankDe(p.Id) + 1;
            PontosDePoder--;
            return true;
        }

        public bool AtendeRequisitos(ItemBase b, Catalogo c, out string motivo)
        {
            var bonus = BonusDeEquipamento(c);
            foreach ((Atributo atributo, int valor) in b.Requisitos)
            {
                float tem = Atributos[(int)atributo] + bonus[(Estat)(int)atributo];
                if (tem < valor)
                {
                    motivo = F("Requer {0} {1}", Nomes.Exibir(atributo), valor);
                    return false;
                }
            }
            motivo = null;
            return true;
        }

        public static SlotEquip? SlotPara(ItemBase b, Personagem p = null)
        {
            switch (b.Tipo)
            {
                case TipoItem.Arma: return SlotEquip.Arma;
                case TipoItem.Secundaria: return SlotEquip.Secundaria;
                case TipoItem.Cabeca: return SlotEquip.Cabeca;
                case TipoItem.Corpo: return SlotEquip.Corpo;
                case TipoItem.Maos: return SlotEquip.Maos;
                case TipoItem.Pes: return SlotEquip.Pes;
                case TipoItem.Pingente: return SlotEquip.Pingente;
                case TipoItem.Bugiganga:
                    if (p != null && p.Equipado[(int)SlotEquip.Bugiganga1] != null && p.Equipado[(int)SlotEquip.Bugiganga2] == null)
                        return SlotEquip.Bugiganga2;
                    return SlotEquip.Bugiganga1;
            }
            return null;
        }

        public static bool SlotAceita(SlotEquip slot, ItemBase b)
        {
            switch (slot)
            {
                case SlotEquip.Bugiganga1:
                case SlotEquip.Bugiganga2: return b.Tipo == TipoItem.Bugiganga;
                default: return SlotPara(b) == slot;
            }
        }

        /// <summary>
        /// Equipa o item de um espaco da mochila. O que estava no lugar volta
        /// para o mesmo espaco; arma de duas maos tira a secundaria.
        /// </summary>
        public bool Equipar(int espaco, Catalogo c, out string motivo, SlotEquip? slotPedido = null)
        {
            Item item = Mochila.Espacos[espaco];
            motivo = null;
            if (item == null) return false;
            ItemBase b = c.Item(item.Base);
            SlotEquip? slot = slotPedido ?? SlotPara(b, this);
            if (slot == null || !SlotAceita(slot.Value, b))
            {
                motivo = T("Não dá para equipar isso aí.");
                return false;
            }
            if (!AtendeRequisitos(b, c, out motivo)) return false;
            if (b.Tipo == TipoItem.Secundaria)
            {
                ItemBase arma = Arma(c);
                if (arma != null && arma.DuasMaos)
                {
                    motivo = T("A arma usa as duas mãos.");
                    return false;
                }
            }
            Mochila.Espacos[espaco] = Equipado[(int)slot.Value];
            Equipado[(int)slot.Value] = item;
            if (b.DuasMaos && Equipado[(int)SlotEquip.Secundaria] != null)
            {
                Item sec = Equipado[(int)SlotEquip.Secundaria];
                Equipado[(int)SlotEquip.Secundaria] = null;
                if (Mochila.Adicionar(sec, c) != null)
                {
                    // Sem espaco: desfaz tudo em vez de perder o escudo.
                    Equipado[(int)SlotEquip.Secundaria] = sec;
                    Equipado[(int)slot.Value] = Mochila.Espacos[espaco];
                    Mochila.Espacos[espaco] = item;
                    motivo = T("Mochila cheia.");
                    return false;
                }
            }
            return true;
        }

        public bool Desequipar(SlotEquip slot, Catalogo c)
        {
            Item item = Equipado[(int)slot];
            if (item == null) return false;
            if (Mochila.Adicionar(item, c) != null) return false;
            Equipado[(int)slot] = null;
            return true;
        }

        /// <summary>Devolve todos os pontos gastos em atributos, sem mexer nos da classe.</summary>
        public void RedistribuirAtributos(Catalogo c)
        {
            Classe classe = c.Classe(Classe);
            int gastos = 0;
            for (int i = 0; i < 4; i++)
            {
                gastos += Atributos[i] - classe.Atributos[i];
                Atributos[i] = classe.Atributos[i];
            }
            PontosDeAtributo += gastos;
        }
    }
}
