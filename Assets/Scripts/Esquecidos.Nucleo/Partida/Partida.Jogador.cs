using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Nucleo.Partidas
{
    /// <summary>Tudo que o jogador pode pedir. A camada de entrada so traduz tecla e clique para isto.</summary>
    public sealed partial class Partida
    {
        private bool PodeAgir => Jogador != null && Jogador.Vivo && !EmCena && !Pausada && !JogadorMorto;

        /// <summary>WASD: direcao relativa a camera, ja convertida para o plano do mapa.</summary>
        public void MoverDireto(Vector2 direcao)
        {
            if (!PodeAgir)
            {
                if (Jogador != null) Jogador.MovimentoDireto = Vector2.Zero;
                return;
            }
            Jogador.MovimentoDireto = direcao.LengthSquared() > 1 ? Vector2.Normalize(direcao) : direcao;
            if (direcao.LengthSquared() > 0.01f) _lootPendente = 0;
        }

        public void MoverPara(Vector2 ponto)
        {
            if (!PodeAgir) return;
            _lootPendente = 0;
            Jogador.Ordem.Tipo = TipoOrdem.Mover;
            Jogador.Ordem.Ponto = ponto;
            Jogador.Ordem.Repetir = false;
        }

        public void Parar()
        {
            if (Jogador == null) return;
            Jogador.LimparOrdem();
            Jogador.MovimentoDireto = Vector2.Zero;
        }

        /// <summary>Clique em inimigo: anda ate o alcance e bate, e continua batendo enquanto segurar.</summary>
        public void Atacar(int alvoId, bool repetir, string poderId = null)
        {
            if (!PodeAgir) return;
            Entidade alvo = Mundo.Buscar(alvoId);
            if (alvo == null || !alvo.Vivo || !Jogador.HostilA(alvo)) return;
            Poder p = poderId == null ? PoderBasico() : PoderDoHeroi(poderId);
            _lootPendente = 0;
            // So a ordem: ela anda ate o alcance e bate assim que o golpe atual
            // termina. Enfileirar aqui faria o proximo golpe sair no ar, porque o
            // empurrao do primeiro tira o alvo do alcance.
            Jogador.Ordem.Tipo = TipoOrdem.Atacar;
            Jogador.Ordem.AlvoId = alvoId;
            Jogador.Ordem.Poder = p;
            Jogador.Ordem.Repetir = repetir;
        }

        public void PararDeRepetir()
        {
            if (Jogador != null) Jogador.Ordem.Repetir = false;
        }

        /// <summary>Ataque basico no lugar, na direcao do cursor (Shift + clique, como no Flare).</summary>
        public void AtacarNoLugar(Vector2 ponto)
        {
            if (!PodeAgir) return;
            Jogador.LimparOrdem();
            Mundo.Pedir(Jogador, PoderBasico(), ponto, 0);
        }

        /// <summary>
        /// Usa o que estiver no espaco da barra. Poder de mirar em inimigo com
        /// alvo sob o cursor vira ordem de ataque (anda ate o alcance); o resto
        /// sai na hora, na direcao do cursor.
        /// </summary>
        public void UsarBarra(int espaco, Vector2 ponto, int alvoId)
        {
            if (!PodeAgir || espaco < 0 || espaco >= Heroi.Barra.Length) return;
            string id = Heroi.Barra[espaco];
            if (id == null) return;
            if (id == "ataque")
            {
                if (alvoId != 0) Atacar(alvoId, false);
                else AtacarNoLugar(ponto);
                return;
            }
            if (Catalogo.Itens.ContainsKey(id))
            {
                UsarItemPorBase(id);
                return;
            }
            if (Heroi.RankDe(id) <= 0) return;
            UsarPoder(id, ponto, alvoId);
        }

        public void UsarPoder(string id, Vector2 ponto, int alvoId)
        {
            if (!PodeAgir) return;
            Poder p = PoderDoHeroi(id);
            string motivo = Mundo.MotivoParaNaoUsar(Jogador, p);
            if (motivo != null)
            {
                if (motivo == "arma errada")
                {
                    string armas = string.Join(T(" ou "), p.ArmasExigidas.Select(NomeDaClasseDeArma));
                    Mensagem(F("{0} precisa de arma {1}.", p.Nome, armas), "#ff9a6a");
                }
                else if (motivo == "fôlego insuficiente") Mensagem(T("Sem fôlego."), "#7fb6ff");
                return;
            }
            Entidade alvo = Mundo.Buscar(alvoId);
            bool miraInimigo = p.Alvo == TipoDeAlvo.Inimigo && (p.Tipo == TipoPoder.Corpo || p.Tipo == TipoPoder.Investida);
            if (miraInimigo && alvo != null && alvo.Vivo && Jogador.HostilA(alvo))
            {
                float dist = Vector2.Distance(alvo.Pos, Jogador.Pos) - alvo.Raio;
                if (dist > Mundo.AlcanceEfetivo(Jogador, p))
                {
                    Jogador.Ordem.Tipo = TipoOrdem.Atacar;
                    Jogador.Ordem.AlvoId = alvoId;
                    Jogador.Ordem.Poder = p;
                    Jogador.Ordem.Repetir = false;
                    return;
                }
                ponto = alvo.Pos;
            }
            else if (alvo != null && alvo.Vivo && Jogador.HostilA(alvo) && p.Tipo == TipoPoder.Projetil)
            {
                ponto = alvo.Pos;
            }
            Jogador.LimparOrdem();
            Mundo.Pedir(Jogador, p, ponto, alvo?.Id ?? 0);
        }

        public static string NomeDaClasseDeArma(ClasseArma c)
        {
            switch (c)
            {
                case ClasseArma.Leve: return T("leve");
                case ClasseArma.Pesada: return T("pesada");
                case ClasseArma.Haste: return T("de haste");
                case ClasseArma.Distancia: return T("de arremesso");
                case ClasseArma.Choque: return T("de choque");
                default: return T("nenhuma");
            }
        }

        public void Esquivar(Vector2 ponto)
        {
            if (!PodeAgir) return;
            string id = Heroi.Poderes.ContainsKey("rolamento") ? "rolamento" : null;
            if (id == null) return;
            Mundo.Pedir(Jogador, PoderDoHeroi(id), ponto, 0);
        }

        public void InteragirCom(int objetoId)
        {
            if (!PodeAgir) return;
            _lootPendente = 0;
            Jogador.Ordem.Tipo = TipoOrdem.Interagir;
            Jogador.Ordem.ObjetoId = objetoId;
            Jogador.Ordem.AlvoId = 0;
        }

        public void FalarCom(int npcId)
        {
            if (!PodeAgir) return;
            _lootPendente = 0;
            Jogador.Ordem.Tipo = TipoOrdem.Interagir;
            Jogador.Ordem.ObjetoId = -1;
            Jogador.Ordem.AlvoId = npcId;
        }

        /// <summary>Tecla de interagir: o que estiver mais perto entre NPC, objeto e item no chao.</summary>
        public bool InteragirComOMaisProximo()
        {
            if (!PodeAgir) return false;
            const float raio = 3.2f;
            float melhor = raio;
            Action acao = null;
            foreach (Entidade n in Mundo.Entidades)
            {
                if (n.NpcId == null || !n.Vivo || n == Companheiro) continue;
                float d = Vector2.Distance(n.Pos, Jogador.Pos);
                if (d < melhor)
                {
                    melhor = d;
                    int id = n.Id;
                    acao = () => FalarCom(id);
                }
            }
            foreach (ObjetoDeCena o in Mundo.Objetos)
            {
                if (!o.Interativo || o.Tipo == TipoObjeto.Saida || o.Tipo == TipoObjeto.Gatilho || o.Tipo == TipoObjeto.MuroMovel) continue;
                if (o.Tipo == TipoObjeto.Bau && o.Usado) continue;
                if (o.Tipo == TipoObjeto.Porta && o.Ligado) continue;
                float d = Vector2.Distance(o.Pos, Jogador.Pos) - (o.RaioInteracao - 1.6f);
                if (d < melhor)
                {
                    melhor = d;
                    int id = o.Id;
                    acao = () => InteragirCom(id);
                }
            }
            foreach (ItemNoChao l in Mundo.Loot)
            {
                float d = Vector2.Distance(l.Pos, Jogador.Pos);
                if (d < melhor)
                {
                    melhor = d;
                    int id = l.Id;
                    acao = () => PedirPegar(id);
                }
            }
            acao?.Invoke();
            return acao != null;
        }

        // ------------------------------------------------------------------
        // Itens
        // ------------------------------------------------------------------

        public bool UsarItemPorBase(string baseId)
        {
            for (int i = 0; i < Heroi.Mochila.Tamanho; i++)
            {
                Item it = Heroi.Mochila.Espacos[i];
                if (it != null && it.Base == baseId) return UsarItem(i);
            }
            Mensagem($"Acabou: {Catalogo.Item(baseId).Nome}.", "#ff9a6a");
            return false;
        }

        /// <summary>Usar um item da mochila: equipavel equipa, consumivel consome.</summary>
        public bool UsarItem(int espaco)
        {
            Item item = Heroi.Mochila.Espacos[espaco];
            if (item == null) return false;
            ItemBase b = Catalogo.Item(item.Base);
            if (b.Equipavel) return Equipar(espaco);
            if (b.Uso == null || Jogador == null || !Jogador.Vivo) return false;
            if (_recargaDeConsumivel > 0) return false;
            if (!AplicarUso(b.Uso)) return false;
            _recargaDeConsumivel = 0.6f;
            item.Quantidade--;
            if (item.Quantidade <= 0) Heroi.Mochila.Espacos[espaco] = null;
            Mundo.Despachante.Despachar(new NotSom("usar_" + b.Uso.Efeito, Jogador.Pos));
            Diario.Reavaliar();
            Despachante.Despachar(new NotFichaMudou());
            return true;
        }

        private bool AplicarUso(UsoConsumivel u)
        {
            Entidade j = Jogador;
            switch (u.Efeito)
            {
                case "cura":
                    if (j.Vida >= j.VidaMax - 0.5f)
                    {
                        Mensagem(T("Você já está inteiro."));
                        return false;
                    }
                    if (u.Duracao > 0) Mundo.AplicarEfeito(j, "regeneracao", u.Duracao, u.Valor / u.Duracao, j.Id);
                    else Mundo.Curar(j, u.Valor);
                    return true;
                case "energia":
                    j.Energia = MathF.Min(j.EnergiaMax, j.Energia + u.Valor);
                    return true;
                case "antidoto":
                    Mundo.RemoverEfeito(j, "veneno");
                    Mundo.RemoverEfeito(j, "sangramento");
                    Mundo.RemoverEfeito(j, "queimadura");
                    Mundo.Curar(j, u.Valor);
                    return true;
                case "furia":
                case "pele":
                case "rapidez":
                    Mundo.AplicarEfeito(j, u.Efeito, u.Duracao, u.Valor, j.Id);
                    return true;
                case "mapa":
                    if (Mapa.Planta == null)
                    {
                        Mensagem(T("Aqui não há o que mapear."));
                        return false;
                    }
                    RevelarSetor();
                    return true;
                case "retorno":
                    if (Mapa.Seguro || Ciclo.Noite)
                    {
                        Mensagem(Ciclo.Noite ? T("De noite o sinalizador não chama ninguém.") : T("Você já está no Pátio."));
                        return false;
                    }
                    IrPara("patio", "sul");
                    return true;
            }
            return false;
        }

        public bool Equipar(int espaco, SlotEquip? slot = null)
        {
            if (!Heroi.Equipar(espaco, Catalogo, out string motivo, slot))
            {
                if (motivo != null) Mensagem(motivo, "#ff9a6a");
                return false;
            }
            RecalcularFicha();
            Despachante.Despachar(new Simulacao.NotSom("equipar", Jogador?.Pos ?? Vector2.Zero));
            return true;
        }

        public bool Desequipar(SlotEquip slot)
        {
            if (!Heroi.Desequipar(slot, Catalogo))
            {
                Mensagem(T("Mochila cheia."), "#ff9a6a");
                return false;
            }
            RecalcularFicha();
            return true;
        }

        public void JogarFora(int espaco)
        {
            Item item = Heroi.Mochila.Espacos[espaco];
            if (item == null || Jogador == null) return;
            if (Catalogo.Item(item.Base).Tipo == TipoItem.Missao)
            {
                Mensagem(T("Isso é importante demais para jogar fora."));
                return;
            }
            Heroi.Mochila.Espacos[espaco] = null;
            Mundo.SoltarItem(Jogador.Pos, item);
            Diario.Reavaliar();
        }

        public void GuardarNaGuarda(int espaco)
        {
            Item item = Heroi.Mochila.Espacos[espaco];
            if (item == null) return;
            Item sobra = Guarda.Adicionar(item, Catalogo);
            Heroi.Mochila.Espacos[espaco] = sobra;
            if (sobra != null) Mensagem(T("O baú está cheio."));
            Diario.Reavaliar();
        }

        public void TirarDaGuarda(int espaco)
        {
            Item item = Guarda.Espacos[espaco];
            if (item == null) return;
            Item sobra = Heroi.Mochila.Adicionar(item, Catalogo);
            Guarda.Espacos[espaco] = sobra;
            if (sobra != null) Mensagem(T("Mochila cheia."), "#ff9a6a");
            Diario.Reavaliar();
        }

        public void ColocarNaBarra(int espaco, string poderOuItem)
        {
            if (espaco < 0 || espaco >= Heroi.Barra.Length) return;
            for (int i = 0; i < Heroi.Barra.Length; i++)
                if (Heroi.Barra[i] == poderOuItem) Heroi.Barra[i] = null;
            Heroi.Barra[espaco] = poderOuItem;
        }

        // ------------------------------------------------------------------
        // Atributos e poderes
        // ------------------------------------------------------------------

        public bool GastarAtributo(Atributo a)
        {
            if (!Heroi.GastarAtributo(a, Catalogo.Regras)) return false;
            RecalcularFicha();
            return true;
        }

        public bool AprenderPoder(string id)
        {
            Poder p = Catalogo.Poder(id);
            string motivo = Heroi.MotivoParaNaoAprender(p);
            if (motivo != null)
            {
                Mensagem($"{p.Nome}: {motivo}.", "#ff9a6a");
                return false;
            }
            bool novo = Heroi.RankDe(id) == 0;
            Heroi.Aprender(p);
            if (novo && p.Tipo != TipoPoder.Esquiva)
            {
                // Poder novo vai para o primeiro espaco livre da barra.
                for (int i = 1; i < Heroi.Barra.Length; i++)
                {
                    if (Heroi.Barra[i] != null) continue;
                    Heroi.Barra[i] = id;
                    break;
                }
            }
            Despachante.Despachar(new NotFichaMudou());
            return true;
        }

        // ------------------------------------------------------------------
        // Lojas e oficina
        // ------------------------------------------------------------------

        private readonly Dictionary<string, (int dia, List<Item> itens)> _estoques = new Dictionary<string, (int, List<Item>)>();

        /// <summary>Estoque de uma loja: fixos sempre, e um sortimento que muda a cada dia.</summary>
        public List<Item> Estoque(string loja)
        {
            if (_estoques.TryGetValue(loja, out var e) && e.dia == Ciclo.Dia) return e.itens;
            LojaDef def = Catalogo.Lojas[loja];
            var rng = new Random(SementeDoMundo ^ Hash.Estavel(loja) ^ (Ciclo.Dia * 104729));
            var itens = new List<Item>();
            foreach (string id in def.Fixos)
            {
                ItemBase b = Catalogo.Item(id);
                itens.Add(new Item(id, b.Empilhavel ? Math.Min(b.Pilha, 10) : 1) { Nivel = b.Nivel });
            }
            for (int i = 0; i < def.Aleatorios; i++)
            {
                var tipos = new List<TipoItem>();
                foreach (string c in def.Categorias)
                {
                    if (c == "arma") tipos.Add(TipoItem.Arma);
                    if (c == "armadura") tipos.AddRange(new[] { TipoItem.Cabeca, TipoItem.Corpo, TipoItem.Maos, TipoItem.Pes, TipoItem.Secundaria });
                    if (c == "joia") tipos.AddRange(new[] { TipoItem.Pingente, TipoItem.Bugiganga });
                }
                Item item = Loot.Equipamento(Math.Max(1, Heroi.Nivel), rng, 40, tipos.ToArray());
                if (item != null) itens.Add(item);
            }
            _estoques[loja] = (Ciclo.Dia, itens);
            return itens;
        }

        public bool Comprar(string loja, int indice)
        {
            List<Item> estoque = Estoque(loja);
            if (indice < 0 || indice >= estoque.Count) return false;
            Item item = estoque[indice];
            ItemBase b = Catalogo.Item(item.Base);
            Item unidade = item.Clonar();
            if (b.Empilhavel) unidade.Quantidade = 1;
            int preco = DescricaoDeItem.PrecoDeCompra(unidade, Catalogo);
            if (Heroi.Sucata < preco)
            {
                Mensagem(T("Sucata insuficiente."), "#ff9a6a");
                return false;
            }
            if (Heroi.Mochila.Cabe(unidade, Catalogo) < unidade.Quantidade)
            {
                Mensagem(T("Mochila cheia."), "#ff9a6a");
                return false;
            }
            GanharSucata(-preco);
            Heroi.Mochila.Adicionar(unidade, Catalogo);
            if (b.Empilhavel && !Catalogo.Lojas[loja].Fixos.Contains(b.Id))
            {
                item.Quantidade--;
                if (item.Quantidade <= 0) estoque.RemoveAt(indice);
            }
            else if (!b.Empilhavel)
            {
                estoque.RemoveAt(indice);
            }
            Despachante.Despachar(new Simulacao.NotSom("comprar", Vector2.Zero));
            Diario.Reavaliar();
            return true;
        }

        public bool Vender(string loja, int espaco)
        {
            Item item = Heroi.Mochila.Espacos[espaco];
            if (item == null) return false;
            ItemBase b = Catalogo.Item(item.Base);
            if (b.Tipo == TipoItem.Missao)
            {
                Mensagem(T("Ninguém compra isso. E você precisa disso."));
                return false;
            }
            int valor = DescricaoDeItem.PrecoDeVenda(item, Catalogo);
            Heroi.Mochila.Espacos[espaco] = null;
            GanharSucata(valor);
            // O que se vende fica no balcao: da para recomprar no mesmo dia.
            if (!b.Empilhavel) Estoque(loja).Add(item);
            Despachante.Despachar(new Simulacao.NotSom("vender", Vector2.Zero));
            Diario.Reavaliar();
            return true;
        }

        public List<Receita> ReceitasDa(string estacao)
        {
            return Catalogo.Receitas.Values
                .Where(r => r.Estacao == estacao && (r.RequerStatus == null || Diario.TemStatus(r.RequerStatus)))
                .OrderBy(r => r.Nivel).ThenBy(r => r.Id, StringComparer.Ordinal)
                .ToList();
        }

        public string MotivoParaNaoFabricar(Receita r)
        {
            if (Heroi.Nivel < r.Nivel) return F("requer nível {0}", r.Nivel);
            if (Heroi.Sucata < r.Sucata) return T("sucata insuficiente");
            foreach ((string item, int qtd) in r.Ingredientes)
                if (Heroi.Mochila.Contar(item) < qtd) return $"falta {Catalogo.Item(item).Nome}";
            return null;
        }

        public bool Fabricar(Receita r)
        {
            string motivo = MotivoParaNaoFabricar(r);
            if (motivo != null)
            {
                Mensagem(motivo, "#ff9a6a");
                return false;
            }
            ItemBase b = Catalogo.Item(r.Resultado);
            Item resultado = b.Equipavel
                ? Loot.Criar(b, Math.Max(b.Nivel, Heroi.Nivel), GeradorDeLoot.SortearRaridade(Rng, 120, b) == Raridade.Comum ? Raridade.Reforcado : Raridade.Raro, Rng)
                : new Item(b.Id, r.QuantidadeResultado);
            if (Heroi.Mochila.Cabe(resultado, Catalogo) < resultado.Quantidade && Heroi.Mochila.Livres() == 0)
            {
                Mensagem(T("Mochila cheia."), "#ff9a6a");
                return false;
            }
            foreach ((string item, int qtd) in r.Ingredientes) Heroi.Mochila.Remover(item, qtd);
            GanharSucata(-r.Sucata);
            DarItemAoHeroi(resultado);
            Contar("fabricados");
            Despachante.Despachar(new Simulacao.NotSom("fabricar", Vector2.Zero));
            return true;
        }

        // ------------------------------------------------------------------
        // Descanso e mapeamento
        // ------------------------------------------------------------------

        public void Descansar()
        {
            Ciclo.DormirAteAmanhecer();
            if (Jogador != null)
            {
                Jogador.Vida = Jogador.VidaMax;
                Jogador.Energia = Jogador.EnergiaMax;
            }
            Contar("noites_dormidas");
        }

        public string ChaveDeExploracao() => Mapa?.Planta == null ? null : $"{Mapa.Id}:{Ciclo.Dia}";

        private void MarcarExplorado()
        {
            if (Mapa?.Planta == null || Jogador == null) return;
            (int x, int z) = Mapa.Planta.CelulaEm(Jogador.Pos);
            if (!Mapa.Planta.Dentro(x, z)) return;
            string chave = ChaveDeExploracao();
            if (!Explorado.TryGetValue(chave, out HashSet<int> celulas))
            {
                celulas = new HashSet<int>();
                Explorado[chave] = celulas;
            }
            // O corredor ve um pouco alem de onde pisa: a celula e as vizinhas abertas.
            celulas.Add(z * Mapa.Planta.Colunas + x);
        }

        public void RevelarSetor()
        {
            string chave = ChaveDeExploracao();
            if (chave == null) return;
            if (!Explorado.TryGetValue(chave, out HashSet<int> celulas))
            {
                celulas = new HashSet<int>();
                Explorado[chave] = celulas;
            }
            for (int i = 0; i < Mapa.Planta.Colunas * Mapa.Planta.Linhas; i++) celulas.Add(i);
            Mensagem(T("O setor inteiro está no seu mapa agora."), "#ffe9b0");
        }

        public HashSet<int> CelulasExploradas(string mapa, int dia)
        {
            return Explorado.TryGetValue($"{mapa}:{dia}", out HashSet<int> c) ? c : new HashSet<int>();
        }

        /// <summary>
        /// Mapas entregues na Mesa dos Mapas viram experiencia e alimentam a
        /// missao do cartografo. Cada celula so paga uma vez por dia de setor.
        /// </summary>
        public int EntregarMapas()
        {
            int novas = 0;
            foreach (KeyValuePair<string, HashSet<int>> kv in Explorado)
            {
                Entregue.TryGetValue(kv.Key, out int ja);
                int agora = kv.Value.Count;
                if (agora <= ja) continue;
                novas += agora - ja;
                Entregue[kv.Key] = agora;
            }
            if (novas == 0) return 0;
            Contar("celulas_mapeadas", novas);
            GanharXp(novas * 4);
            int total = Contador("celulas_mapeadas");
            if (total >= 40) Diario.DefinirStatus("mapeou_40");
            if (total >= 120) Diario.DefinirStatus("mapeou_120");
            Mensagem(F("Caio passou {0} trechos para o mapa grande. (+{1} XP)", novas, novas * 4), "#ffe9b0");
            return novas;
        }
    }
}
