using System;
using System.Collections.Generic;
using Esquecidos.Nucleo.Dados;

namespace Esquecidos.Nucleo.Itens
{
    /// <summary>
    /// Mochila de espacos fixos. Um espaco vazio e null; a posicao importa
    /// porque o jogador arruma a mochila do jeito dele.
    /// </summary>
    public sealed class Inventario
    {
        public Item[] Espacos { get; private set; }

        public Inventario(int tamanho)
        {
            Espacos = new Item[tamanho];
        }

        public int Tamanho => Espacos.Length;

        public void Redimensionar(int tamanho)
        {
            var novo = new Item[tamanho];
            Array.Copy(Espacos, novo, Math.Min(tamanho, Espacos.Length));
            Espacos = novo;
        }

        public int Contar(string baseId)
        {
            int n = 0;
            foreach (Item i in Espacos)
                if (i != null && i.Base == baseId) n += i.Quantidade;
            return n;
        }

        public int Livres()
        {
            int n = 0;
            foreach (Item i in Espacos)
                if (i == null) n++;
            return n;
        }

        /// <summary>Quanto desse item ainda cabe, somando pilhas incompletas e espacos vazios.</summary>
        public int Cabe(Item item, Catalogo c)
        {
            ItemBase b = c.Item(item.Base);
            int cabe = 0;
            foreach (Item i in Espacos)
            {
                if (i == null) cabe += b.Pilha;
                else if (i.PodeEmpilharCom(item, c)) cabe += Math.Max(0, b.Pilha - i.Quantidade);
            }
            return cabe;
        }

        /// <summary>
        /// Guarda o item, completando pilhas antes de ocupar espaco novo.
        /// Devolve o que sobrou (null se coube tudo).
        /// </summary>
        public Item Adicionar(Item item, Catalogo c)
        {
            ItemBase b = c.Item(item.Base);
            int resta = item.Quantidade;
            if (b.Empilhavel)
            {
                foreach (Item i in Espacos)
                {
                    if (resta == 0) break;
                    if (i == null || !i.PodeEmpilharCom(item, c)) continue;
                    int entra = Math.Min(resta, b.Pilha - i.Quantidade);
                    if (entra <= 0) continue;
                    i.Quantidade += entra;
                    resta -= entra;
                }
            }
            for (int k = 0; k < Espacos.Length && resta > 0; k++)
            {
                if (Espacos[k] != null) continue;
                Item novo = item.Clonar();
                novo.Quantidade = Math.Min(resta, Math.Max(1, b.Pilha));
                Espacos[k] = novo;
                resta -= novo.Quantidade;
            }
            if (resta == 0) return null;
            Item sobra = item.Clonar();
            sobra.Quantidade = resta;
            return sobra;
        }

        /// <summary>Tira N unidades de uma base, das pilhas do fim para o comeco.</summary>
        public bool Remover(string baseId, int quantidade)
        {
            if (Contar(baseId) < quantidade) return false;
            for (int k = Espacos.Length - 1; k >= 0 && quantidade > 0; k--)
            {
                Item i = Espacos[k];
                if (i == null || i.Base != baseId) continue;
                int sai = Math.Min(quantidade, i.Quantidade);
                i.Quantidade -= sai;
                quantidade -= sai;
                if (i.Quantidade <= 0) Espacos[k] = null;
            }
            return true;
        }

        public Item Tirar(int espaco)
        {
            Item i = Espacos[espaco];
            Espacos[espaco] = null;
            return i;
        }

        /// <summary>Troca dois espacos; se os itens empilham, junta no destino.</summary>
        public void Mover(int de, int para, Catalogo c)
        {
            if (de == para) return;
            Item a = Espacos[de], b = Espacos[para];
            if (a != null && b != null && a.PodeEmpilharCom(b, c))
            {
                int max = c.Item(a.Base).Pilha;
                int entra = Math.Min(a.Quantidade, max - b.Quantidade);
                b.Quantidade += entra;
                a.Quantidade -= entra;
                if (a.Quantidade <= 0) Espacos[de] = null;
                return;
            }
            Espacos[de] = b;
            Espacos[para] = a;
        }

        /// <summary>Arruma: equipamento primeiro, por raridade; depois consumiveis, materiais e missao.</summary>
        public void Organizar(Catalogo c)
        {
            var itens = new List<Item>();
            foreach (Item i in Espacos)
                if (i != null) itens.Add(i);
            // Junta pilhas espalhadas antes de ordenar.
            var juntos = new List<Item>();
            foreach (Item i in itens)
            {
                Item alvo = juntos.Find(j => j.PodeEmpilharCom(i, c) && j.Quantidade < c.Item(j.Base).Pilha);
                if (alvo == null)
                {
                    juntos.Add(i);
                    continue;
                }
                int max = c.Item(alvo.Base).Pilha;
                int entra = Math.Min(i.Quantidade, max - alvo.Quantidade);
                alvo.Quantidade += entra;
                i.Quantidade -= entra;
                if (i.Quantidade > 0) juntos.Add(i);
            }
            juntos.Sort((x, y) =>
            {
                ItemBase bx = c.Item(x.Base), by = c.Item(y.Base);
                int t = bx.Tipo.CompareTo(by.Tipo);
                if (t != 0) return t;
                int r = y.Raridade.CompareTo(x.Raridade);
                if (r != 0) return r;
                int n = y.Nivel.CompareTo(x.Nivel);
                return n != 0 ? n : string.CompareOrdinal(x.Base, y.Base);
            });
            Array.Clear(Espacos, 0, Espacos.Length);
            for (int k = 0; k < juntos.Count && k < Espacos.Length; k++) Espacos[k] = juntos[k];
        }
    }
}
