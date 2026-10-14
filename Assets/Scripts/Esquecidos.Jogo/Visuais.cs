using System.Collections.Generic;
using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Cena;
using Esquecidos.Jogo.Efeitos;
using Esquecidos.Jogo.Visual;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Mantem um corpo visivel para cada entidade e cada item no chao da
    /// simulacao, criando e destruindo conforme as notificacoes chegam.
    /// </summary>
    public sealed class Visuais :
        IExecutor<NotEntidadeCriada>,
        IExecutor<NotEntidadeRemovida>,
        IExecutor<NotLootCriado>,
        IExecutor<NotLootRemovido>,
        IExecutor<NotFichaMudou>
    {
        private readonly Principal _p;
        private readonly Dictionary<int, VisualDeEntidade> _entidades = new Dictionary<int, VisualDeEntidade>();
        private readonly Dictionary<int, VisualDeLoot> _loot = new Dictionary<int, VisualDeLoot>();
        public Transform Raiz { get; private set; }

        public Visuais(Principal p)
        {
            _p = p;
        }

        public IEnumerable<VisualDeEntidade> Entidades => _entidades.Values;
        public IEnumerable<VisualDeLoot> Loot => _loot.Values;

        /// <summary>Recomeca do zero (mapa novo): derruba tudo e cria os que ja existem no mundo.</summary>
        public void Reconstruir(Mundo mundo, Transform pai)
        {
            if (Raiz != null) Object.Destroy(Raiz.gameObject);
            _entidades.Clear();
            _loot.Clear();
            Raiz = new GameObject("Atores").transform;
            Raiz.SetParent(pai, false);
            foreach (Entidade e in mundo.Entidades) Criar(e);
            foreach (ItemNoChao l in mundo.Loot) CriarLoot(l);
        }

        public VisualDeEntidade De(Entidade e) => e != null && _entidades.TryGetValue(e.Id, out VisualDeEntidade v) ? v : null;
        public VisualDeEntidade De(int id) => _entidades.TryGetValue(id, out VisualDeEntidade v) ? v : null;

        private void Criar(Entidade e)
        {
            if (Raiz == null || _entidades.ContainsKey(e.Id)) return;
            VisualDeEntidade v = FabricaDeAtores.Criar(e, _p.Partida, Raiz);
            _entidades[e.Id] = v;
            if (e == _p.Partida.Jogador) _p.Camera.Alvo = v.transform;
        }

        public void Executar(NotEntidadeCriada n) => Criar(n.Entidade);

        public void Executar(NotEntidadeRemovida n)
        {
            if (!_entidades.TryGetValue(n.Entidade.Id, out VisualDeEntidade v)) return;
            _entidades.Remove(n.Entidade.Id);
            if (v != null) Object.Destroy(v.gameObject);
        }

        private void CriarLoot(ItemNoChao l)
        {
            if (Raiz == null || _loot.ContainsKey(l.Id)) return;
            _loot[l.Id] = VisualDeLoot.Criar(l, _p.Partida.Catalogo, Raiz);
        }

        public void Executar(NotLootCriado n) => CriarLoot(n.Loot);

        public void Executar(NotLootRemovido n)
        {
            if (!_loot.TryGetValue(n.Loot.Id, out VisualDeLoot v)) return;
            _loot.Remove(n.Loot.Id);
            if (v == null) return;
            if (n.Pego) Particulas.Brilhos(v.transform.position + Vector3.up * 0.4f, 10, v.Cor, 0.3f, 2);
            Object.Destroy(v.gameObject);
        }

        /// <summary>Equipamento mudou: o boneco do jogador troca roupa e arma.</summary>
        public void Executar(NotFichaMudou n)
        {
            Entidade j = _p.Partida.Jogador;
            if (j == null || !(De(j) is Humanoide h)) return;
            DadosDeHumano d = FabricaDeAtores.DadosDoJogador(_p.Partida);
            if (d.Chave() == h.Dados.Chave() && d.Arma == h.Dados.Arma && d.Escudo == h.Dados.Escudo) return;
            h.Dados = d;
            h.Aplicar();
        }
    }

    /// <summary>Um item no chao: o modelo dele girando devagar e o facho da cor da raridade.</summary>
    public sealed class VisualDeLoot : MonoBehaviour
    {
        public ItemNoChao L { get; private set; }
        public Color Cor { get; private set; }
        public string Nome { get; private set; }
        private Transform _modelo;
        private float _t;
        private Vector3 _alvo;
        private Vector3 _inicio;

        public static VisualDeLoot Criar(ItemNoChao l, Nucleo.Dados.Catalogo c, Transform pai)
        {
            var g = new GameObject("loot");
            g.transform.SetParent(pai, false);
            var v = g.AddComponent<VisualDeLoot>();
            v.L = l;
            v._alvo = Util.Mundo(l.Pos);
            v.transform.position = v._alvo;
            v._inicio = v._alvo + Vector3.up * 1.2f;
            var cm = new ConstrutorDeMalha();
            if (l.Item == null)
            {
                v.Cor = new Color(0.95f, 0.8f, 0.45f);
                v.Nome = $"{l.Sucata} de sucata";
                var rng = new System.Random(l.Id);
                for (int i = 0; i < 6; i++)
                {
                    cm.Empurrar(new Vector3((float)rng.NextDouble() * 0.4f - 0.2f, 0.04f, (float)rng.NextDouble() * 0.4f - 0.2f), Quaternion.Euler(0, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 30));
                    cm.Cor(Color.Lerp(new Color(0.75f, 0.6f, 0.3f), new Color(0.6f, 0.6f, 0.62f), (float)rng.NextDouble())).Caixa(Vector3.zero, new Vector3(0.12f, 0.03f, 0.08f), 0.01f);
                    cm.Puxar();
                }
            }
            else
            {
                ItemBase b = c.Item(l.Item.Base);
                v.Cor = Util.Cor(DescricaoDeItem.CorDaRaridade(l.Item.Raridade));
                v.Nome = DescricaoDeItem.Nome(l.Item, c) + (l.Item.Quantidade > 1 ? $" ×{l.Item.Quantidade}" : "");
                Color corItem = Util.Cor(b.Cor, Color.gray);
                if (b.Tipo == TipoItem.Arma)
                {
                    cm.Juntar(ModelosDeArma.Arma(b.Modelo, corItem), Matrix4x4.TRS(new Vector3(0, 0.08f, -0.2f), Quaternion.Euler(0, 0, 90), Vector3.one), true);
                }
                else if (b.Tipo == TipoItem.Secundaria)
                {
                    cm.Juntar(ModelosDeArma.Escudo(b.Modelo, corItem), Matrix4x4.TRS(new Vector3(0, 0.05f, 0), Quaternion.identity, Vector3.one * 0.8f), true);
                }
                else if (b.Tipo == TipoItem.Consumivel)
                {
                    cm.Cor(corItem).Tubo(new Vector3(0, 0, 0), new Vector3(0, 0.22f, 0), 0.07f, 0.06f, 8);
                    cm.Cor(new Color(0.85f, 0.82f, 0.75f)).Tubo(new Vector3(0, 0.22f, 0), new Vector3(0, 0.3f, 0), 0.03f, 0.03f, 6);
                }
                else if (b.Equipavel)
                {
                    cm.Cor(corItem).Caixa(new Vector3(0, 0.08f, 0), new Vector3(0.38f, 0.16f, 0.3f), 0.05f);
                    cm.Cor(Util.Escurecer(corItem, 0.6f)).Caixa(new Vector3(0, 0.17f, 0), new Vector3(0.3f, 0.03f, 0.2f), 0.01f);
                }
                else
                {
                    cm.Cor(corItem).Esfera(new Vector3(0, 0.1f, 0), new Vector3(0.14f, 0.1f, 0.14f), 5, 7);
                }
                if (l.Item.Raridade >= Raridade.Reforcado) Particulas.Facho(g.transform, v.Cor, l.Item.Raridade >= Raridade.Raro ? 4.5f : 2.2f, l.Item.Raridade >= Raridade.Raro ? 0.6f : 0.35f);
            }
            v._modelo = Util.Filho(g.transform, "modelo").transform;
            Util.ComMalha(v._modelo.gameObject, cm.Gerar("loot"), Paleta.Toon);
            v._modelo.position = v._inicio;
            return v;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float queda = Mathf.Clamp01(_t / 0.45f);
            Vector3 p = Vector3.Lerp(_inicio, _alvo, queda) + Vector3.up * Mathf.Sin(queda * Mathf.PI) * 0.6f;
            if (queda >= 1) p = _alvo + Vector3.up * (0.04f + Mathf.Sin(_t * 2.2f) * 0.03f);
            _modelo.position = p;
            _modelo.rotation = Quaternion.Euler(0, _t * 50, 0);
        }
    }
}
