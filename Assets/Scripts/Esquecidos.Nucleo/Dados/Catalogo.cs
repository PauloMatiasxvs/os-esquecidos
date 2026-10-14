using System;
using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Dados
{
    /// <summary>
    /// Todo o conteudo do jogo depois de lido: um dicionario por tipo e as
    /// regras. Nada aqui muda durante a partida.
    /// </summary>
    public sealed class Catalogo
    {
        public Regras.Regras Regras { get; private set; } = new();
        public Dictionary<string, Classe> Classes { get; } = new();
        public Dictionary<string, ItemBase> Itens { get; } = new();
        public Dictionary<string, Afixo> Afixos { get; } = new();
        public Dictionary<string, Poder> Poderes { get; } = new();
        public Dictionary<string, InimigoDef> Inimigos { get; } = new();
        public Dictionary<string, NpcDef> Npcs { get; } = new();
        public List<DialogoDef> Dialogos { get; } = new();
        public Dictionary<string, MissaoDef> Missoes { get; } = new();
        public Dictionary<string, TabelaDeLoot> Loot { get; } = new();
        public Dictionary<string, LojaDef> Lojas { get; } = new();
        public Dictionary<string, Receita> Receitas { get; } = new();
        public Dictionary<string, SetorDef> Setores { get; } = new();
        public Dictionary<string, RegistroDef> Registros { get; } = new();
        public List<string> OrdemDasClasses { get; } = new();
        public List<string> OrdemDosSetores { get; } = new();

        public ItemBase Item(string id) => Buscar(Itens, id, "item");
        public Afixo Afixo(string id) => Buscar(Afixos, id, "afixo");
        public Poder Poder(string id) => Buscar(Poderes, id, "poder");
        public InimigoDef Inimigo(string id) => Buscar(Inimigos, id, "inimigo");
        public Classe Classe(string id) => Buscar(Classes, id, "classe");
        public MissaoDef Missao(string id) => Buscar(Missoes, id, "missao");
        public NpcDef Npc(string id) => Buscar(Npcs, id, "npc");
        public SetorDef Setor(string id) => Buscar(Setores, id, "setor");

        private static T Buscar<T>(Dictionary<string, T> d, string id, string tipo)
        {
            if (id != null && d.TryGetValue(id, out T v)) return v;
            throw new KeyNotFoundException($"{tipo} desconhecido: '{id}'");
        }

        /// <summary>
        /// Le um arquivo de dados e distribui cada secao pelo nome dela. Um
        /// arquivo pode misturar tipos: um NPC e seus dialogos ficam juntos.
        /// </summary>
        public void Carregar(string conteudo, string arquivo)
        {
            foreach (Secao s in LeitorIni.Ler(conteudo, arquivo))
            {
                switch (s.Nome)
                {
                    case "regras": Regras = Nucleo.Regras.Regras.Ler(s); break;
                    case "classe":
                        var c = Nucleo.Regras.Classe.Ler(s);
                        Adicionar(Classes, c.Id, c, s);
                        OrdemDasClasses.Add(c.Id);
                        break;
                    case "item": { var v = ItemBase.Ler(s); Adicionar(Itens, v.Id, v, s); break; }
                    case "afixo": { var v = Nucleo.Itens.Afixo.Ler(s); Adicionar(Afixos, v.Id, v, s); break; }
                    case "poder": { var v = Nucleo.Regras.Poder.Ler(s); Adicionar(Poderes, v.Id, v, s); break; }
                    case "inimigo": { var v = InimigoDef.Ler(s); Adicionar(Inimigos, v.Id, v, s); break; }
                    case "npc": { var v = NpcDef.Ler(s); Adicionar(Npcs, v.Id, v, s); break; }
                    case "dialogo": Dialogos.Add(DialogoDef.Ler(s)); break;
                    case "missao": { var v = MissaoDef.Ler(s); Adicionar(Missoes, v.Id, v, s); break; }
                    case "loot": { var v = TabelaDeLoot.Ler(s); Adicionar(Loot, v.Id, v, s); break; }
                    case "loja": { var v = LojaDef.Ler(s); Adicionar(Lojas, v.Id, v, s); break; }
                    case "receita": { var v = Nucleo.Dados.Receita.Ler(s); Adicionar(Receitas, v.Id, v, s); break; }
                    case "setor":
                        var st = SetorDef.Ler(s);
                        Adicionar(Setores, st.Id, st, s);
                        OrdemDosSetores.Add(st.Id);
                        break;
                    case "registro": { var v = RegistroDef.Ler(s); Adicionar(Registros, v.Id, v, s); break; }
                    case "":
                        break;
                    default:
                        throw new FormatException($"{arquivo}:{s.Linha} secao desconhecida [{s.Nome}]");
                }
            }
        }

        private static void Adicionar<T>(Dictionary<string, T> d, string id, T valor, Secao s)
        {
            if (d.ContainsKey(id)) throw new FormatException($"{s.Arquivo}:{s.Linha} id repetido '{id}'");
            d[id] = valor;
        }

        /// <summary>
        /// Confere toda referencia cruzada. Um id errado num arquivo de texto e
        /// o tipo de erro que so apareceria no meio da partida; aqui ele aparece
        /// na carga, com o nome do que falta.
        /// </summary>
        public List<string> Validar(Func<string, bool> roteiroExiste = null)
        {
            var erros = new List<string>();
            void Item(string id, string onde)
            {
                if (id != null && !Itens.ContainsKey(id)) erros.Add($"{onde}: item '{id}' nao existe");
            }
            void PoderRef(string id, string onde)
            {
                if (id != null && !Poderes.ContainsKey(id)) erros.Add($"{onde}: poder '{id}' nao existe");
            }

            foreach (Classe c in Classes.Values)
            {
                foreach (string p in c.Poderes) PoderRef(p, $"classe {c.Id}");
                foreach (string i in c.Equipamento) Item(i, $"classe {c.Id}");
                foreach ((string i, _) in c.Mochila) Item(i, $"classe {c.Id}");
                foreach (string b in c.Barra)
                    if (b != null && !Poderes.ContainsKey(b) && !Itens.ContainsKey(b))
                        erros.Add($"classe {c.Id}: barra '{b}' nao e poder nem item");
            }
            foreach (Poder p in Poderes.Values)
            {
                PoderRef(p.RequerPoder, $"poder {p.Id}");
                if (p.Invoca != null && !Inimigos.ContainsKey(p.Invoca)) erros.Add($"poder {p.Id}: invoca '{p.Invoca}' nao existe");
            }
            foreach (InimigoDef d in Inimigos.Values)
            {
                foreach (UsoDePoderDoInimigo u in d.Poderes) PoderRef(u.Poder, $"inimigo {d.Id}");
                if (d.Loot != null && !Loot.ContainsKey(d.Loot)) erros.Add($"inimigo {d.Id}: loot '{d.Loot}' nao existe");
            }
            foreach (TabelaDeLoot t in Loot.Values)
                foreach ((string e, _) in t.Entradas)
                    if (!e.StartsWith("@")) Item(e, $"loot {t.Id}");
            foreach (LojaDef l in Lojas.Values)
                foreach (string i in l.Fixos) Item(i, $"loja {l.Id}");
            foreach (NpcDef n in Npcs.Values)
                if (n.Loja != null && !Lojas.ContainsKey(n.Loja)) erros.Add($"npc {n.Id}: loja '{n.Loja}' nao existe");
            foreach (Receita r in Receitas.Values)
            {
                Item(r.Resultado, $"receita {r.Id}");
                foreach ((string i, _) in r.Ingredientes) Item(i, $"receita {r.Id}");
            }
            foreach (MissaoDef m in Missoes.Values)
            {
                foreach (string i in m.RecompensaItens) Item(i, $"missao {m.Id}");
                foreach (EtapaDeMissao e in m.Etapas)
                {
                    if (e.Tipo == TipoObjetivo.Matar && e.Alvo != "*" && !Inimigos.ContainsKey(e.Alvo))
                        erros.Add($"missao {m.Id}: inimigo '{e.Alvo}' nao existe");
                    if (e.Tipo == TipoObjetivo.Item) Item(e.Alvo, $"missao {m.Id}");
                }
                if (roteiroExiste != null && m.RoteiroAoConcluir != null && !roteiroExiste(m.RoteiroAoConcluir))
                    erros.Add($"missao {m.Id}: roteiro '{m.RoteiroAoConcluir}' nao existe");
            }
            foreach (DialogoDef d in Dialogos)
            {
                if (!Npcs.ContainsKey(d.Npc)) erros.Add($"dialogo '{d.Topico}': npc '{d.Npc}' nao existe");
                if (roteiroExiste != null && !roteiroExiste(d.Roteiro)) erros.Add($"dialogo {d.Npc}/'{d.Topico}': roteiro '{d.Roteiro}' nao existe");
                foreach (string rm in d.RequerMissao)
                {
                    string id = rm.Split(':')[0];
                    if (!Missoes.ContainsKey(id)) erros.Add($"dialogo {d.Npc}: missao '{id}' nao existe");
                }
            }
            foreach (SetorDef s in Setores.Values)
            {
                foreach ((string i, _) in s.Inimigos.Concat(s.InimigosNoturnos))
                    if (!Inimigos.ContainsKey(i)) erros.Add($"setor {s.Id}: inimigo '{i}' nao existe");
                if (s.Chefe != null && !Inimigos.ContainsKey(s.Chefe)) erros.Add($"setor {s.Id}: chefe '{s.Chefe}' nao existe");
                if (s.Proximo != null && !Setores.ContainsKey(s.Proximo))
                    erros.Add($"setor {s.Id}: proximo '{s.Proximo}' nao existe");
                (string volta, _) = s.Retorno();
                if (volta != "patio" && !Setores.ContainsKey(volta)) erros.Add($"setor {s.Id}: volta '{volta}' nao existe");
                foreach (string t in s.Terminal)
                    if (!Registros.ContainsKey(t)) erros.Add($"setor {s.Id}: registro '{t}' nao existe");
            }
            foreach (Afixo a in Afixos.Values)
                if (a.Categorias.Count == 0) erros.Add($"afixo {a.Id}: sem categorias");
            return erros;
        }
    }
}
