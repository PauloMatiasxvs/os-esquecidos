using System;
using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Dados;

namespace Esquecidos.Nucleo.Itens
{
    /// <summary>
    /// Sorteia itens: qual base, que raridade, quais afixos e com que valor.
    /// O nivel da area manda em tudo; o bonus de achado raro do jogador e de
    /// chefes empurra a raridade para cima.
    /// </summary>
    public sealed class GeradorDeLoot
    {
        private readonly Catalogo _c;

        public GeradorDeLoot(Catalogo catalogo)
        {
            _c = catalogo;
        }

        /// <summary>O que um bicho morto deixa cair, pela tabela dele.</summary>
        public List<Item> Rolar(TabelaDeLoot t, int nivel, Random rng, float achadoRaro = 0, bool garantido = false)
        {
            var itens = new List<Item>();
            if (t == null || t.Entradas.Count == 0) return itens;
            if (!garantido && rng.NextDouble() >= t.Chance * (1 + achadoRaro / 200f)) return itens;
            int quantos = rng.Next(t.QuantidadeMin, t.QuantidadeMax + 1);
            int total = t.Entradas.Sum(e => e.peso);
            for (int i = 0; i < quantos; i++)
            {
                int sorteio = rng.Next(total);
                foreach ((string entrada, int peso) in t.Entradas)
                {
                    if (sorteio >= peso)
                    {
                        sorteio -= peso;
                        continue;
                    }
                    Item item = Resolver(entrada, nivel, rng, achadoRaro + t.RaridadeBonus * 25);
                    if (item != null) itens.Add(item);
                    break;
                }
            }
            return itens;
        }

        private Item Resolver(string entrada, int nivel, Random rng, float achadoRaro)
        {
            switch (entrada)
            {
                case "@equip": return Equipamento(nivel, rng, achadoRaro);
                case "@arma": return Equipamento(nivel, rng, achadoRaro, TipoItem.Arma);
                case "@joia": return Equipamento(nivel, rng, achadoRaro + 20, TipoItem.Pingente, TipoItem.Bugiganga);
                case "@consumivel": return Consumivel(nivel, rng);
            }
            ItemBase b = _c.Item(entrada);
            if (b.Equipavel) return Criar(b, Math.Max(b.Nivel, nivel), SortearRaridade(rng, achadoRaro, b), rng);
            return new Item(b.Id, 1);
        }

        public Item Consumivel(int nivel, Random rng)
        {
            List<ItemBase> opcoes = _c.Itens.Values.Where(b => b.Tipo == TipoItem.Consumivel && b.Nivel <= nivel + 1 && b.Preco > 0).ToList();
            if (opcoes.Count == 0) return null;
            return new Item(opcoes[rng.Next(opcoes.Count)].Id, 1);
        }

        /// <summary>Equipamento aleatorio ate o nivel da area, preferindo os perto dele.</summary>
        public Item Equipamento(int nivel, Random rng, float achadoRaro, params TipoItem[] tipos)
        {
            List<ItemBase> opcoes = _c.Itens.Values
                .Where(b => b.Equipavel && !b.Unico && b.Nivel <= nivel + 1 && (tipos.Length == 0 || tipos.Contains(b.Tipo)))
                .OrderBy(b => b.Id, StringComparer.Ordinal)
                .ToList();
            if (opcoes.Count == 0) return null;
            // Peso maior para bases no nivel da area: lixo de nivel 1 no fim do jogo cansa.
            float total = 0;
            var pesos = new float[opcoes.Count];
            for (int i = 0; i < opcoes.Count; i++)
            {
                int diferenca = nivel - opcoes[i].Nivel;
                pesos[i] = diferenca < 0 ? 0.5f : MathF.Max(0.15f, 1f - diferenca * 0.12f);
                total += pesos[i];
            }
            float s = (float)rng.NextDouble() * total;
            ItemBase escolhida = opcoes[opcoes.Count - 1];
            for (int i = 0; i < opcoes.Count; i++)
            {
                if (s < pesos[i])
                {
                    escolhida = opcoes[i];
                    break;
                }
                s -= pesos[i];
            }
            Raridade r = SortearRaridade(rng, achadoRaro, escolhida);
            return Criar(escolhida, Math.Max(escolhida.Nivel, nivel), r, rng);
        }

        public static Raridade SortearRaridade(Random rng, float achadoRaro, ItemBase b = null)
        {
            if (b != null && b.Unico) return Raridade.Unico;
            float mult = 1 + achadoRaro / 100f;
            double x = rng.NextDouble();
            if (x < 0.012 * mult) return Raridade.Reliquia;
            if (x < 0.075 * mult) return Raridade.Raro;
            if (x < 0.30 * mult) return Raridade.Reforcado;
            return Raridade.Comum;
        }

        /// <summary>Item pronto: afixos sorteados conforme a raridade, valores escalados pelo nivel.</summary>
        public Item Criar(ItemBase b, int nivel, Raridade r, Random rng)
        {
            var item = new Item(b.Id) { Nivel = Math.Max(1, nivel), Raridade = b.Unico ? Raridade.Unico : r };
            if (!b.Equipavel || b.Unico) return item;
            int quantos;
            switch (item.Raridade)
            {
                case Raridade.Reforcado: quantos = 1; break;
                case Raridade.Raro: quantos = 2 + (rng.NextDouble() < 0.4 ? 1 : 0); break;
                case Raridade.Reliquia: quantos = 4; break;
                default: quantos = 0; break;
            }
            string categoria = b.CategoriaDeAfixo;
            List<Afixo> possiveis = _c.Afixos.Values
                .Where(a => a.Categorias.Contains(categoria) && a.Nivel <= item.Nivel)
                .OrderBy(a => a.Id, StringComparer.Ordinal)
                .ToList();
            bool temPrefixo = false, temSufixo = false;
            for (int i = 0; i < quantos && possiveis.Count > 0; i++)
            {
                int total = possiveis.Sum(a => a.Peso);
                int s = rng.Next(total);
                Afixo escolhido = possiveis[0];
                foreach (Afixo a in possiveis)
                {
                    if (s < a.Peso)
                    {
                        escolhido = a;
                        break;
                    }
                    s -= a.Peso;
                }
                // No maximo um adjetivo e um "do X" dao nome; o resto so soma.
                possiveis.RemoveAll(a => a.Estat == escolhido.Estat);
                float valor = escolhido.Min + (float)rng.NextDouble() * (escolhido.Max - escolhido.Min);
                valor += escolhido.PorNivel * (item.Nivel - 1);
                valor *= item.Raridade == Raridade.Reliquia ? 1.25f : 1f;
                valor = MathF.Round(valor * (MathF.Abs(valor) < 5 ? 10 : 1)) / (MathF.Abs(valor) < 5 ? 10 : 1);
                if (valor == 0) valor = escolhido.Min;
                item.Afixos.Add(new AfixoRolado { Afixo = escolhido.Id, Valor = valor });
                if (escolhido.Prefixo) temPrefixo = true;
                else temSufixo = true;
            }
            _ = temPrefixo || temSufixo;
            return item;
        }
    }
}
