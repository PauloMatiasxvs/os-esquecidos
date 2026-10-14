using System;
using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Itens
{
    public enum TipoItem
    {
        Arma,
        Secundaria,
        Cabeca,
        Corpo,
        Maos,
        Pes,
        Pingente,
        Bugiganga,
        Consumivel,
        Material,
        Missao,
    }

    /// <summary>Como a arma ataca. Decide animacao, som e quais poderes ela libera.</summary>
    public enum ClasseArma
    {
        Nenhuma,
        Leve,
        Pesada,
        Haste,
        Distancia,
        Choque,
    }

    public enum SlotEquip
    {
        Arma,
        Secundaria,
        Cabeca,
        Corpo,
        Maos,
        Pes,
        Pingente,
        Bugiganga1,
        Bugiganga2,
    }

    public enum Raridade
    {
        Comum,
        Reforcado,
        Raro,
        Reliquia,
        Unico,
    }

    public sealed class UsoConsumivel
    {
        public string Efeito;
        public float Valor;
        public float Duracao;
    }

    /// <summary>Um item como escrito em dados/itens.txt, antes de ganhar afixos.</summary>
    public sealed class ItemBase
    {
        public string Id;
        public string Nome;
        public bool Feminino;
        public TipoItem Tipo;
        public ClasseArma Arma;
        public int Nivel;
        public float DanoMin, DanoMax;
        public float VelocidadeAtaque = 1f;
        public float Alcance = 2f;
        public float ArmaduraMin, ArmaduraMax;
        public int Preco;
        public int Pilha = 1;
        public string Modelo;
        public string Cor;
        public string Descricao;
        public bool Unico;
        public bool DuasMaos;
        public float Peso = 1f;
        public List<(Estat estat, float valor)> Bonus = new();
        public List<(Atributo atributo, int valor)> Requisitos = new();
        public UsoConsumivel Uso;

        public bool Equipavel => Tipo <= TipoItem.Bugiganga;
        public bool Empilhavel => Pilha > 1;

        public string CategoriaDeAfixo
        {
            get
            {
                switch (Tipo)
                {
                    case TipoItem.Arma: return "arma";
                    case TipoItem.Pingente:
                    case TipoItem.Bugiganga: return "joia";
                    case TipoItem.Secundaria:
                    case TipoItem.Cabeca:
                    case TipoItem.Corpo:
                    case TipoItem.Maos:
                    case TipoItem.Pes: return "armadura";
                }
                return null;
            }
        }

        public static ItemBase Ler(Secao s)
        {
            var b = new ItemBase
            {
                Id = s.TextoObrigatorio("id"),
                Nome = s.TextoObrigatorio("nome"),
                Feminino = s.Texto("genero", "m") == "f",
                Tipo = LerTipo(s.TextoObrigatorio("tipo"), s),
                Arma = LerClasseArma(s.Texto("arma", "nenhuma"), s),
                Nivel = s.Inteiro("nivel", 1),
                VelocidadeAtaque = s.Real("velocidade", 1f),
                Alcance = s.Real("alcance", 2f),
                Preco = s.Inteiro("preco", 1),
                Pilha = s.Inteiro("pilha", 1),
                Modelo = s.Texto("modelo", s.Texto("id")),
                Cor = s.Texto("cor", "#9a9a9a"),
                Descricao = s.Texto("descricao", ""),
                Unico = s.Logico("unico"),
                DuasMaos = s.Logico("duas_maos"),
            };
            (b.DanoMin, b.DanoMax) = s.Par("dano");
            (b.ArmaduraMin, b.ArmaduraMax) = s.Par("armadura");
            foreach (string linha in s.Todos("bonus"))
            {
                string[] p = Secao.Partir(linha);
                b.Bonus.Add((Nomes.EstatDe(p[0]), s.LerReal(p[1], "bonus")));
            }
            foreach (string linha in s.Todos("requer"))
            {
                string[] p = Secao.Partir(linha);
                b.Requisitos.Add((Nomes.AtributoDe(p[0]), s.LerInteiro(p[1], "requer")));
            }
            if (s.Tem("uso"))
            {
                string[] p = s.Lista("uso");
                b.Uso = new UsoConsumivel
                {
                    Efeito = p[0],
                    Valor = p.Length > 1 ? s.LerReal(p[1], "uso") : 0,
                    Duracao = p.Length > 2 ? s.LerReal(p[2], "uso") : 0,
                };
            }
            if (b.Tipo == TipoItem.Arma && b.Arma == ClasseArma.Nenhuma)
                throw new FormatException($"{s.Arquivo}:{s.Linha} arma '{b.Id}' sem 'arma='");
            return b;
        }

        private static TipoItem LerTipo(string v, Secao s)
        {
            switch (v)
            {
                case "arma": return TipoItem.Arma;
                case "secundaria": return TipoItem.Secundaria;
                case "cabeca": return TipoItem.Cabeca;
                case "corpo": return TipoItem.Corpo;
                case "maos": return TipoItem.Maos;
                case "pes": return TipoItem.Pes;
                case "pingente": return TipoItem.Pingente;
                case "bugiganga": return TipoItem.Bugiganga;
                case "consumivel": return TipoItem.Consumivel;
                case "material": return TipoItem.Material;
                case "missao": return TipoItem.Missao;
            }
            throw new FormatException($"{s.Arquivo}:{s.Linha} tipo de item desconhecido '{v}'");
        }

        public static ClasseArma LerClasseArma(string v, Secao s = null)
        {
            switch (v)
            {
                case "nenhuma": return ClasseArma.Nenhuma;
                case "leve": return ClasseArma.Leve;
                case "pesada": return ClasseArma.Pesada;
                case "haste": return ClasseArma.Haste;
                case "distancia": return ClasseArma.Distancia;
                case "choque": return ClasseArma.Choque;
            }
            throw new FormatException($"{s?.Arquivo}:{s?.Linha} classe de arma desconhecida '{v}'");
        }

        public static string ExibirTipo(TipoItem t)
        {
            switch (t)
            {
                case TipoItem.Arma: return "Arma";
                case TipoItem.Secundaria: return "Mão secundária";
                case TipoItem.Cabeca: return "Cabeça";
                case TipoItem.Corpo: return "Corpo";
                case TipoItem.Maos: return "Mãos";
                case TipoItem.Pes: return "Pés";
                case TipoItem.Pingente: return "Pingente";
                case TipoItem.Bugiganga: return "Bugiganga";
                case TipoItem.Consumivel: return "Consumível";
                case TipoItem.Material: return "Material";
                default: return "Item de missão";
            }
        }
    }

    /// <summary>
    /// Um afixo aleatorio. "Prefixo" e o adjetivo (vem depois do nome em
    /// portugues: Machete Serrilhado); "sufixo" e o "do Corredor" do fim.
    /// </summary>
    public sealed class Afixo
    {
        public string Id;
        public bool Prefixo;
        public string NomeMasculino;
        public string NomeFeminino;
        public int Nivel;
        public int Peso = 10;
        public HashSet<string> Categorias = new();
        public Estat Estat;
        public float Min, Max;
        public float PorNivel;

        public static Afixo Ler(Secao s)
        {
            string nome = s.TextoObrigatorio("nome");
            string[] formas = nome.Split('|');
            var a = new Afixo
            {
                Id = s.TextoObrigatorio("id"),
                Prefixo = s.Texto("tipo", "prefixo") == "prefixo",
                NomeMasculino = formas[0].Trim(),
                NomeFeminino = (formas.Length > 1 ? formas[1] : formas[0]).Trim(),
                Nivel = s.Inteiro("nivel", 1),
                Peso = s.Inteiro("peso", 10),
                Estat = Nomes.EstatDe(s.TextoObrigatorio("estat")),
                PorNivel = s.Real("por_nivel", 0),
            };
            (a.Min, a.Max) = s.Par("valor");
            foreach (string c in s.Lista("categorias")) a.Categorias.Add(c);
            return a;
        }

        public string NomePara(ItemBase b) => b.Feminino ? NomeFeminino : NomeMasculino;
    }

    [Serializable]
    public sealed class AfixoRolado
    {
        public string Afixo;
        public float Valor;
    }

    /// <summary>
    /// Um item que existe no mundo. Guarda so o que nao da para recalcular a
    /// partir dos dados: base, raridade, nivel, afixos rolados e quantidade.
    /// </summary>
    [Serializable]
    public sealed class Item
    {
        public string Base;
        public Raridade Raridade;
        public int Nivel = 1;
        public int Quantidade = 1;
        public List<AfixoRolado> Afixos = new();

        public Item()
        {
        }

        public Item(string baseId, int quantidade = 1)
        {
            Base = baseId;
            Quantidade = quantidade;
        }

        public Item Clonar()
        {
            return new Item
            {
                Base = Base,
                Raridade = Raridade,
                Nivel = Nivel,
                Quantidade = Quantidade,
                Afixos = Afixos.Select(a => new AfixoRolado { Afixo = a.Afixo, Valor = a.Valor }).ToList(),
            };
        }

        /// <summary>Empilha so o que e identico: mesma base, raridade comum, sem afixo.</summary>
        public bool PodeEmpilharCom(Item outro, Catalogo c)
        {
            return outro != null && outro.Base == Base && Afixos.Count == 0 && outro.Afixos.Count == 0
                   && outro.Raridade == Raridade && c.Item(Base).Empilhavel;
        }
    }

    public static class DescricaoDeItem
    {
        public static string Nome(Item item, Catalogo c)
        {
            ItemBase b = c.Item(item.Base);
            if (b.Unico || item.Afixos.Count == 0) return b.Nome;
            string adjetivo = null, complemento = null;
            foreach (AfixoRolado r in item.Afixos)
            {
                Afixo a = c.Afixo(r.Afixo);
                if (a.Prefixo && adjetivo == null) adjetivo = a.NomePara(b);
                else if (!a.Prefixo && complemento == null) complemento = a.NomePara(b);
            }
            string nome = b.Nome;
            if (adjetivo != null) nome += " " + adjetivo;
            if (complemento != null) nome += " " + complemento;
            return nome;
        }

        public static string NomeDaRaridade(Raridade r)
        {
            switch (r)
            {
                case Raridade.Comum: return "Comum";
                case Raridade.Reforcado: return "Reforçado";
                case Raridade.Raro: return "Raro";
                case Raridade.Reliquia: return "Relíquia";
                default: return "Único";
            }
        }

        /// <summary>Cor de exibicao em hexadecimal, a mesma do facho de luz do chao.</summary>
        public static string CorDaRaridade(Raridade r)
        {
            switch (r)
            {
                case Raridade.Comum: return "#d8d8d0";
                case Raridade.Reforcado: return "#6fd36a";
                case Raridade.Raro: return "#5aa8ff";
                case Raridade.Reliquia: return "#c070ff";
                default: return "#ffb53a";
            }
        }

        public static int PrecoDeVenda(Item item, Catalogo c)
        {
            return Math.Max(1, PrecoDeCompra(item, c) / 4);
        }

        public static int PrecoDeCompra(Item item, Catalogo c)
        {
            ItemBase b = c.Item(item.Base);
            float mult = 1f + 0.35f * (int)item.Raridade + 0.08f * Math.Max(0, item.Nivel - b.Nivel);
            return Math.Max(1, (int)MathF.Round(b.Preco * mult)) * Math.Max(1, item.Quantidade);
        }

        /// <summary>Bonus que o item soma na ficha de quem o equipa.</summary>
        public static void SomarBonus(Item item, Catalogo c, Ficha destino)
        {
            ItemBase b = c.Item(item.Base);
            foreach ((Estat estat, float valor) in b.Bonus) destino.Somar(estat, valor);
            foreach (AfixoRolado r in item.Afixos) destino.Somar(c.Afixo(r.Afixo).Estat, r.Valor);
            if (b.Tipo != TipoItem.Arma)
            {
                destino.Somar(Estat.AbsorcaoMin, b.ArmaduraMin);
                destino.Somar(Estat.AbsorcaoMax, b.ArmaduraMax);
            }
        }

        public static List<string> Linhas(Item item, Catalogo c)
        {
            ItemBase b = c.Item(item.Base);
            var linhas = new List<string>();
            string tipo = ItemBase.ExibirTipo(b.Tipo);
            if (b.Tipo == TipoItem.Arma) tipo += b.DuasMaos ? " de duas mãos" : "";
            linhas.Add(b.Equipavel ? $"{NomeDaRaridade(item.Raridade)} · {tipo} · nível {item.Nivel}" : tipo);
            if (b.Tipo == TipoItem.Arma)
            {
                linhas.Add($"Dano {Nomes.Formatar(b.DanoMin)}–{Nomes.Formatar(b.DanoMax)} · {b.VelocidadeAtaque:0.0#} golpes/s");
            }
            if (b.ArmaduraMax > 0) linhas.Add($"Absorção {Nomes.Formatar(b.ArmaduraMin)}–{Nomes.Formatar(b.ArmaduraMax)}");
            foreach ((Estat estat, float valor) in b.Bonus) linhas.Add(Nomes.Exibir(estat, valor));
            foreach (AfixoRolado r in item.Afixos) linhas.Add(Nomes.Exibir(c.Afixo(r.Afixo).Estat, r.Valor));
            if (b.Uso != null) linhas.Add(DescreverUso(b.Uso));
            foreach ((Atributo atributo, int valor) in b.Requisitos) linhas.Add($"Requer {Nomes.Exibir(atributo)} {valor}");
            if (!string.IsNullOrEmpty(b.Descricao)) linhas.Add("“" + b.Descricao + "”");
            return linhas;
        }

        public static string DescreverUso(UsoConsumivel u)
        {
            switch (u.Efeito)
            {
                case "cura": return u.Duracao > 0 ? $"Usar: recupera {u.Valor:0} de vida em {u.Duracao:0} s" : $"Usar: recupera {u.Valor:0} de vida";
                case "energia": return $"Usar: recupera {u.Valor:0} de fôlego";
                case "antidoto": return "Usar: remove veneno e sangramento";
                case "furia": return $"Usar: +{u.Valor:0}% dano por {u.Duracao:0} s";
                case "pele": return $"Usar: +{u.Valor:0} absorção por {u.Duracao:0} s";
                case "mapa": return "Usar: revela o setor atual no mapa";
                case "retorno": return "Usar: volta ao Pátio (não funciona de noite)";
            }
            return "Usar: " + u.Efeito;
        }
    }
}
