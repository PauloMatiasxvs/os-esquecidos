using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Roteiro;
using Esquecidos.Nucleo.Simulacao;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Nucleo.Partidas
{
    /// <summary>Os comandos de roteiro que mexem em regra e historia, e as condicoes do "se".</summary>
    public sealed partial class Partida :
        IExecutor<DefinirStatus>,
        IExecutor<RemoverStatus>,
        IExecutor<IniciarMissao>,
        IExecutor<EtapaDaMissao>,
        IExecutor<ConcluirMissao>,
        IExecutor<DarItem>,
        IExecutor<TirarItem>,
        IExecutor<DarXp>,
        IExecutor<DarSucata>,
        IExecutor<TirarSucata>,
        IExecutor<EnsinarPoder>,
        IExecutor<CurarTudo>,
        IExecutor<IrParaMapa>,
        IExecutor<AtorAnda>,
        IExecutor<AtorOlha>,
        IExecutor<AtorPoe>,
        IExecutor<NpcAparece>,
        IExecutor<NpcSome>,
        IExecutor<CriarInimigo>,
        IExecutor<EstadoDoObjeto>,
        IExecutor<Descansar>,
        IExecutor<AvancarHoras>,
        IExecutor<RevelarMapa>,
        IExecutor<DefinirCompanheiro>,
        IExecutor<MudarAfinidade>,
        IExecutor<EsconderItem>,
        IExecutor<EntregarMapas>
    {
        public void Executar(EntregarMapas c)
        {
            if (EntregarMapas() == 0) Mensagem(T("Nenhum trecho novo para entregar. Vá mapear o labirinto."), "#d0c0a0");
        }

        public void Executar(DefinirStatus c)
        {
            Diario.DefinirStatus(c.Status);
            AtualizarPortoes();
        }

        public void Executar(RemoverStatus c) => Diario.RemoverStatus(c.Status);

        public void Executar(IniciarMissao c) => Diario.Iniciar(c.Missao);

        public void Executar(EtapaDaMissao c) => Diario.IrParaEtapa(c.Missao, c.Etapa);

        public void Executar(ConcluirMissao c) => Diario.Concluir(c.Missao);

        public void Executar(DarItem c)
        {
            ItemBase b = Catalogo.Item(c.Item);
            Item item = b.Equipavel ? Loot.Criar(b, Math.Max(b.Nivel, Heroi.Nivel), b.Unico ? Raridade.Unico : Raridade.Reforcado, Rng) : new Item(c.Item, c.Quantidade);
            DarItemAoHeroi(item);
            Mensagem(F("Recebeu: {0}", DescricaoDeItem.Nome(item, Catalogo)) + (item.Quantidade > 1 ? " ×" + item.Quantidade : ""), DescricaoDeItem.CorDaRaridade(item.Raridade));
        }

        public void Executar(TirarItem c)
        {
            if (Heroi.Mochila.Remover(c.Item, c.Quantidade))
                Mensagem(F("Entregou: {0}", Catalogo.Item(c.Item).Nome) + (c.Quantidade > 1 ? " ×" + c.Quantidade : ""), "#c8c0b0");
            Diario.Reavaliar();
        }

        public void Executar(DarXp c)
        {
            GanharXp(c.Quantidade);
            Mensagem(F("+{0} de experiência", c.Quantidade), "#c8e0ff");
        }

        public void Executar(DarSucata c)
        {
            GanharSucata(c.Quantidade);
            Mensagem(F("+{0} de sucata", c.Quantidade), "#e0c890");
        }

        public void Executar(TirarSucata c) => GanharSucata(-Math.Min(c.Quantidade, Heroi.Sucata));

        public void Executar(EnsinarPoder c)
        {
            if (Heroi.RankDe(c.Poder) > 0) return;
            Heroi.Poderes[c.Poder] = 1;
            for (int i = 1; i < Heroi.Barra.Length; i++)
            {
                if (Heroi.Barra[i] != null) continue;
                Heroi.Barra[i] = c.Poder;
                break;
            }
            Mensagem($"Aprendeu: {Catalogo.Poder(c.Poder).Nome}", "#ffd25a");
            Despachante.Despachar(new NotFichaMudou());
        }

        public void Executar(CurarTudo c)
        {
            if (Jogador == null) return;
            Jogador.Vida = Jogador.VidaMax;
            Jogador.Energia = Jogador.EnergiaMax;
            foreach (EfeitoAtivo e in Jogador.Efeitos.ToList())
                if (e.Nome == "veneno" || e.Nome == "sangramento" || e.Nome == "queimadura") Mundo.RemoverEfeito(Jogador, e.Nome);
        }

        public void Executar(IrParaMapa c)
        {
            IrPara(c.Mapa, c.Entrada);
        }

        /// <summary>"jogador", id de NPC, ou "companheiro".</summary>
        public Entidade Ator(string nome)
        {
            if (Mundo == null || nome == null) return null;
            if (nome == "jogador") return Jogador;
            if (nome == "companheiro") return Companheiro;
            return Mundo.Entidades.FirstOrDefault(e => e.NpcId == nome && e != Companheiro) ?? Mundo.Entidades.FirstOrDefault(e => e.NpcId == nome);
        }

        public void Executar(AtorAnda c)
        {
            Entidade e = Ator(c.Ator);
            if (e == null) return;
            var destino = new Vector2(c.X, c.Z);
            e.Ordem.Tipo = TipoOrdem.Mover;
            e.Ordem.Ponto = destino;
            e.Origem = destino;
            Despachante.Despachar(new PedidoDeEspera(new EsperaAte(() => !e.Vivo || Vector2.Distance(e.Pos, destino) < 0.6f, 10f)));
        }

        public void Executar(AtorOlha c)
        {
            Entidade e = Ator(c.Ator), alvo = Ator(c.Alvo);
            if (e != null && alvo != null) e.OlharPara(alvo.Pos);
        }

        public void Executar(AtorPoe c)
        {
            Entidade e = Ator(c.Ator);
            if (e == null) return;
            e.Pos = e.PosAnterior = new Vector2(c.X, c.Z);
            e.Origem = e.Pos;
            e.LimparOrdem();
            if (c.Graus > -900) e.Direcao = c.Graus * MathF.PI / 180f;
        }

        public void Executar(NpcAparece c)
        {
            Entidade e = Ator(c.Npc);
            if (e != null)
            {
                e.Pos = e.PosAnterior = e.Origem = new Vector2(c.X, c.Z);
                return;
            }
            CriarNpc(c.Npc, new Vector2(c.X, c.Z), 0);
        }

        public void Executar(NpcSome c)
        {
            Entidade e = Ator(c.Npc);
            if (e != null && e != Companheiro) Mundo.Remover(e);
        }

        public void Executar(CriarInimigo c)
        {
            InimigoDef def = Catalogo.Inimigo(c.Inimigo);
            int nivel = c.Nivel > 0 ? c.Nivel : Math.Max(def.Nivel, Mapa.Nivel);
            Mundo.CriarInimigo(def, nivel, new Vector2(c.X, c.Z), c.Elite);
        }

        public void Executar(EstadoDoObjeto c)
        {
            ObjetoDeCena o = Mundo.Objetos.FirstOrDefault(x => x.Chave == c.Chave);
            if (o != null)
            {
                Mundo.DefinirLigado(o, c.Ligado);
                Guardar(o);
            }
            else
            {
                EstadosDeObjetos[c.Chave] = c.Ligado;
            }
        }

        public void Executar(Descansar c) => Descansar();

        public void Executar(AvancarHoras c) => Ciclo.AvancarHoras(c.Horas);

        public void Executar(RevelarMapa c) => RevelarSetor();

        public void Executar(DefinirCompanheiro c)
        {
            CompanheiroNpc = c.Npc == "nenhum" ? null : c.Npc;
        }

        /// <summary>
        /// Poe um item de missao num beco longe da entrada do setor atual, se
        /// ele ainda nao esta no mapa nem na mochila.
        /// </summary>
        public void Executar(EsconderItem c)
        {
            if (Mapa?.Planta == null || Heroi.Mochila.Contar(c.Item) > 0) return;
            if (Mundo.Objetos.Any(o => o.Tipo == TipoObjeto.Coletavel && o.Item == c.Item && !o.Usado)) return;
            Mapas.PlantaDoLabirinto pl = Mapa.Planta;
            var rng = new Random(SementeDoSetor(Mapa.Id, Ciclo.Dia) ^ Hash.Estavel(c.Item));
            (int ex, int ez) = pl.Entrada;
            Vector2 melhor = Jogador?.Pos ?? Vector2.Zero;
            float melhorNota = float.MinValue;
            for (int tentativa = 0; tentativa < 80; tentativa++)
            {
                int x = rng.Next(pl.Colunas), z = rng.Next(pl.Linhas);
                if (x >= pl.Arena.x && x < pl.Arena.x + 3 && z >= pl.Arena.z) continue;
                Vector2 centro = pl.CentroDaCelula(x, z);
                if (Mundo.Grade.FolgaEm(centro) < 1f) continue;
                float nota = Math.Abs(x - ex) + Math.Abs(z - ez) + (float)rng.NextDouble();
                if (nota <= melhorNota) continue;
                melhorNota = nota;
                melhor = centro;
            }
            var o = new ObjetoDeCena
            {
                Id = Mapas.GeradorDeSetor.IdsDeObjeto - 1 - Mundo.Objetos.Count,
                Tipo = TipoObjeto.Coletavel,
                Nome = Catalogo.Item(c.Item).Nome,
                Pos = melhor,
                RaioInteracao = 1.8f,
                Item = c.Item,
                Modelo = "brilho",
            };
            Mundo.AdicionarObjeto(o);
            Despachante.Despachar(new NotObjetoMudou(o));
        }

        public void Executar(MudarAfinidade c)
        {
            Afinidade.TryGetValue(c.Npc, out int v);
            Afinidade[c.Npc] = v + c.Delta;
        }

        /// <summary>
        /// Condicoes do "se": status, item, sucata, nivel, missao, classe,
        /// noite, afinidade, contador, registro e sorte.
        /// </summary>
        public bool Avaliar(string tipo, string[] args)
        {
            int N(int i) => int.Parse(args[i], CultureInfo.InvariantCulture);
            switch (tipo)
            {
                case "status": return Diario.TemStatus(args[0]);
                case "item": return Heroi.Mochila.Contar(args[0]) >= (args.Length > 1 ? N(1) : 1);
                case "sucata": return Heroi.Sucata >= N(0);
                case "nivel": return Heroi.Nivel >= N(0);
                case "classe": return Heroi.Classe == args[0];
                case "noite": return Ciclo.Noite;
                case "portoes_abertos": return Ciclo.PortoesAbertos;
                case "missao": return Diario.AtendeMissao(args.Length > 1 ? args[0] + ":" + args[1] : args[0]);
                case "afinidade":
                    Afinidade.TryGetValue(args[0], out int a);
                    return a >= N(1);
                case "contador": return Contador(args[0]) >= N(1);
                case "registro": return RegistrosLidos.Contains(args[0]);
                case "companheiro": return CompanheiroNpc == args[0];
                case "mapa": return Mapa?.Id == args[0];
                case "sorte": return Rng.NextDouble() < double.Parse(args[0], CultureInfo.InvariantCulture);
                case "chefe": return ChefeDerrotado(args[0]);
            }
            throw new FormatException($"condicao desconhecida '{tipo}'");
        }

        private void AoConcluirMissao(MissaoDef m)
        {
            Despachante.Despachar(new NotMissao(m, T("Concluída"), false, true));
            if (m.RecompensaXp > 0) GanharXp(m.RecompensaXp);
            if (m.RecompensaSucata > 0) GanharSucata(m.RecompensaSucata);
            foreach (string id in m.RecompensaItens) Executar(new DarItem(id, 1));
            Contar("missoes");
            AtualizarPortoes();
            if (m.RoteiroAoConcluir != null && Roteiros.Existe(m.RoteiroAoConcluir)) Roteiros.Executar(m.RoteiroAoConcluir);
        }
    }
}
