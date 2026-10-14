using System;
using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Dados;

namespace Esquecidos.Nucleo.Progresso
{
    [Serializable]
    public sealed class EstadoDeMissao
    {
        public string Id;
        public int Etapa;
        public int Contador;
        public bool Concluida;
    }

    /// <summary>
    /// Memoria da historia: os "status" (fatos que aconteceram, como no
    /// Flare) e o andamento de cada missao. As etapas andam sozinhas quando o
    /// fato que elas esperam acontece: um bicho morto, um item na mochila, um
    /// mapa visitado, um status ligado por um roteiro.
    /// </summary>
    public sealed class Diario
    {
        private readonly Catalogo _c;
        public HashSet<string> Status { get; } = new HashSet<string>();
        public Dictionary<string, EstadoDeMissao> Missoes { get; } = new Dictionary<string, EstadoDeMissao>();
        public List<string> Ordem { get; } = new List<string>();

        /// <summary>Como contar itens na mochila; quem cria o diario e quem sabe.</summary>
        public Func<string, int> ContarItem = _ => 0;
        public Func<int> NivelAtual = () => 1;

        public event Action<MissaoDef> MissaoIniciada;
        public event Action<MissaoDef, int> EtapaAvancou;
        public event Action<MissaoDef> MissaoConcluida;

        public Diario(Catalogo catalogo)
        {
            _c = catalogo;
        }

        public bool TemStatus(string s) => s != null && Status.Contains(s);

        public void DefinirStatus(string s)
        {
            if (string.IsNullOrEmpty(s) || !Status.Add(s)) return;
            Reavaliar();
        }

        public void RemoverStatus(string s) => Status.Remove(s);

        public bool Ativa(string id) => Missoes.TryGetValue(id, out EstadoDeMissao m) && !m.Concluida;
        public bool Concluida(string id) => Missoes.TryGetValue(id, out EstadoDeMissao m) && m.Concluida;
        public int Etapa(string id) => Missoes.TryGetValue(id, out EstadoDeMissao m) ? m.Etapa : -1;

        public EtapaDeMissao EtapaAtual(string id)
        {
            if (!Missoes.TryGetValue(id, out EstadoDeMissao m) || m.Concluida) return null;
            MissaoDef def = _c.Missao(id);
            return m.Etapa < def.Etapas.Count ? def.Etapas[m.Etapa] : null;
        }

        public bool Iniciar(string id)
        {
            if (Missoes.ContainsKey(id)) return false;
            MissaoDef def = _c.Missao(id);
            Missoes[id] = new EstadoDeMissao { Id = id };
            Ordem.Add(id);
            MissaoIniciada?.Invoke(def);
            // A etapa pode ja estar cumprida: o item ja esta na mochila.
            Reavaliar();
            return true;
        }

        public void IrParaEtapa(string id, int etapa)
        {
            if (!Missoes.TryGetValue(id, out EstadoDeMissao m) || m.Concluida) return;
            MissaoDef def = _c.Missao(id);
            if (etapa >= def.Etapas.Count)
            {
                Concluir(id);
                return;
            }
            m.Etapa = Math.Max(0, etapa);
            m.Contador = 0;
            EtapaAvancou?.Invoke(def, m.Etapa);
            Reavaliar();
        }

        public void Concluir(string id)
        {
            if (!Missoes.TryGetValue(id, out EstadoDeMissao m))
            {
                m = new EstadoDeMissao { Id = id };
                Missoes[id] = m;
                Ordem.Add(id);
            }
            if (m.Concluida) return;
            MissaoDef def = _c.Missao(id);
            m.Concluida = true;
            m.Etapa = def.Etapas.Count;
            if (def.StatusAoConcluir != null) Status.Add(def.StatusAoConcluir);
            MissaoConcluida?.Invoke(def);
            Reavaliar();
        }

        public void AoMatar(string tipoInimigo)
        {
            foreach (EstadoDeMissao m in Missoes.Values.Where(x => !x.Concluida).ToList())
            {
                EtapaDeMissao e = EtapaAtual(m.Id);
                if (e == null || e.Tipo != TipoObjetivo.Matar) continue;
                if (e.Alvo != "*" && e.Alvo != tipoInimigo) continue;
                m.Contador++;
                if (m.Contador >= e.Quantidade) Avancar(m);
            }
        }

        public void AoEntrarNoMapa(string mapa)
        {
            foreach (EstadoDeMissao m in Missoes.Values.Where(x => !x.Concluida).ToList())
            {
                EtapaDeMissao e = EtapaAtual(m.Id);
                if (e != null && e.Tipo == TipoObjetivo.Mapa && (e.Alvo == mapa || mapa.StartsWith(e.Alvo + ":", StringComparison.Ordinal)))
                    Avancar(m);
            }
        }

        /// <summary>Confere as etapas que dependem de estado: status, item, nivel.</summary>
        public void Reavaliar()
        {
            bool mudou = true;
            int seguranca = 64;
            while (mudou && seguranca-- > 0)
            {
                mudou = false;
                foreach (EstadoDeMissao m in Missoes.Values.Where(x => !x.Concluida).ToList())
                {
                    EtapaDeMissao e = EtapaAtual(m.Id);
                    if (e == null) continue;
                    bool cumprida = false;
                    switch (e.Tipo)
                    {
                        case TipoObjetivo.Status: cumprida = Status.Contains(e.Alvo); break;
                        case TipoObjetivo.Item: cumprida = ContarItem(e.Alvo) >= e.Quantidade; break;
                        case TipoObjetivo.Nivel: cumprida = NivelAtual() >= e.Quantidade; break;
                    }
                    if (!cumprida) continue;
                    Avancar(m);
                    mudou = true;
                }
            }
        }

        private void Avancar(EstadoDeMissao m)
        {
            MissaoDef def = _c.Missao(m.Id);
            m.Etapa++;
            m.Contador = 0;
            if (m.Etapa >= def.Etapas.Count) Concluir(m.Id);
            else EtapaAvancou?.Invoke(def, m.Etapa);
        }

        /// <summary>
        /// "id:etapa" (ativa naquela etapa), "id:ativa", "id:concluida",
        /// "id:nova" (ainda nao comecou). E o requer_missao dos dialogos.
        /// </summary>
        public bool AtendeMissao(string requisito)
        {
            string[] p = requisito.Split(':');
            string id = p[0];
            string cond = p.Length > 1 ? p[1] : "ativa";
            switch (cond)
            {
                case "ativa": return Ativa(id);
                case "concluida": return Concluida(id);
                case "nova": return !Missoes.ContainsKey(id);
                case "nao_concluida": return !Concluida(id);
            }
            return Ativa(id) && Etapa(id) == int.Parse(cond, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Assuntos que o NPC tem agora, na ordem de prioridade.</summary>
        public List<DialogoDef> Topicos(string npc)
        {
            return _c.Dialogos
                .Where(d => d.Npc == npc
                            && d.RequerStatus.All(Status.Contains)
                            && !d.RequerNaoStatus.Any(Status.Contains)
                            && d.RequerMissao.All(AtendeMissao))
                .OrderByDescending(d => d.Prioridade)
                .ToList();
        }

        /// <summary>Texto da etapa atual, com o contador quando a etapa conta algo.</summary>
        public string TextoDaEtapa(string id)
        {
            EtapaDeMissao e = EtapaAtual(id);
            if (e == null) return Concluida(id) ? "Concluída." : "";
            if (e.Tipo == TipoObjetivo.Matar && e.Quantidade > 1) return $"{e.Texto} ({Missoes[id].Contador}/{e.Quantidade})";
            if (e.Tipo == TipoObjetivo.Item && e.Quantidade > 1) return $"{e.Texto} ({Math.Min(ContarItem(e.Alvo), e.Quantidade)}/{e.Quantidade})";
            return e.Texto;
        }
    }
}
