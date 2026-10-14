using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using Xunit;
using Xunit.Abstractions;

namespace Esquecidos.Testes
{
    /// <summary>A partida jogada sem tela: o caminho que um jogador faz, verificado passo a passo.</summary>
    public class PartidaTestes
    {
        private readonly ITestOutputHelper _saida;

        public PartidaTestes(ITestOutputHelper saida)
        {
            _saida = saida;
        }

        private static Entidade Npc(Partida p, string id) => p.Mundo.Entidades.First(e => e.NpcId == id);

        private static void Falar(Partida p, string npc)
        {
            p.Executar(new NotFalarComNpc(p.Jogador, Npc(p, npc)));
            Apoio.PularCenas(p);
        }

        /// <summary>Anda ate o alvo e bate ate ele morrer, pela mesma ordem que o clique daria.</summary>
        private static void Matar(Partida p, Entidade alvo, float limite = 60)
        {
            float t = 0;
            while (alvo.Vivo && t < limite)
            {
                p.Atacar(alvo.Id, true);
                p.Passo(1f / 60f);
                t += 1f / 60f;
                if (!p.Jogador.Vivo) break;
            }
        }

        [Fact]
        public void AberturaComecaOPrimeiroDiaNoPatio()
        {
            Partida p = Apoio.Partida();
            Apoio.PularCenas(p);
            Assert.Equal("patio", p.Mapa.Id);
            Assert.True(p.Diario.Ativa("m01_primeiro_dia"));
            Assert.Equal(0, p.Diario.Etapa("m01_primeiro_dia"));
            Assert.NotNull(p.Jogador);
            ObjetoDeCena leste = p.Mundo.Objetos.First(o => o.Chave == "patio:portao_leste");
            Assert.False(leste.Ligado);
        }

        [Fact]
        public void PrimeiroDiaInteiroAbreOPortaoLesteETeoVemJunto()
        {
            Partida p = Apoio.Partida("guardiao");
            Apoio.PularCenas(p);
            Falar(p, "iara");
            Assert.Equal(1, p.Diario.Etapa("m01_primeiro_dia"));
            Falar(p, "duda");
            Falar(p, "marreta");
            Assert.Equal(3, p.Diario.Etapa("m01_primeiro_dia"));
            Assert.Equal(1, p.Heroi.Mochila.Contar("tampa_de_lixeira"));

            foreach (Entidade boneco in p.Mundo.Entidades.Where(e => e.Tipo == "boneco").ToList())
            {
                p.Jogador.Pos = boneco.Pos + new Vector2(-1.2f, 0);
                Matar(p, boneco);
                Assert.False(boneco.Vivo, "boneco nao morreu");
            }
            Assert.Equal(4, p.Diario.Etapa("m01_primeiro_dia"));
            Falar(p, "teo");
            Apoio.PularCenas(p);
            Assert.True(p.Diario.Concluida("m01_primeiro_dia"));
            Assert.True(p.Diario.Ativa("m02_hera"));
            Assert.Equal("teo", p.CompanheiroNpc);
            ObjetoDeCena leste = p.Mundo.Objetos.First(o => o.Chave == "patio:portao_leste");
            Assert.True(leste.Ligado, "portao leste deveria abrir de dia depois do primeiro dia");
        }

        [Fact]
        public void SairPeloPortaoLevaAoSetorComOCompanheiro()
        {
            Partida p = Apoio.Partida("corredor");
            Apoio.PularCenas(p);
            p.Diario.DefinirStatus("portao_leste");
            p.CompanheiroNpc = "teo";
            p.EntrarNoMapa("patio", "leste");
            ObjetoDeCena saida = p.Mundo.Objetos.First(o => o.Tipo == TipoObjeto.Saida && o.Destino == "portao:leste");
            // Anda ate a saida pelo portao aberto.
            p.MoverPara(saida.Pos);
            Apoio.Rodar(p, 6);
            Apoio.PularCenas(p);
            Assert.Equal("setor1", p.Mapa.Id);
            Assert.NotNull(p.Companheiro);
            Assert.Equal(Faccao.Jogador, p.Companheiro.Faccao);
            Assert.True(p.Mundo.Entidades.Count(e => e.Faccao == Faccao.Inimigo) >= 10);
        }

        [Fact]
        public void CombateMataTecelaoDaXpESoltaLootComOTempo()
        {
            Partida p = Apoio.Partida("guardiao", 777);
            Apoio.PularCenas(p);
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            int xpAntes = p.Heroi.Xp;
            int mortos = 0;
            foreach (Entidade e in p.Mundo.Entidades.Where(x => x.Tipo == "tecelao_jovem").Take(6).ToList())
            {
                foreach (Entidade outro in p.Mundo.Entidades.Where(x => x.Faccao == Faccao.Inimigo && x != e)) outro.Cerebro = null;
                p.Jogador.Pos = e.Pos + new Vector2(1.0f, 0);
                p.Jogador.Vida = p.Jogador.VidaMax;
                Matar(p, e, 30);
                if (!e.Vivo) mortos++;
            }
            Assert.True(mortos >= 4, $"so matou {mortos}");
            Assert.True(p.Heroi.Xp > xpAntes);
            Assert.True(p.Contador("inimigos_mortos") >= mortos);
        }

        [Fact]
        public void TecelaoPerseguePicaEEnvenena()
        {
            Partida p = Apoio.Partida("corredor", 4242);
            Apoio.PularCenas(p);
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            foreach (Entidade e in p.Mundo.Entidades.Where(x => x.Faccao == Faccao.Inimigo).ToList()) p.Mundo.Remover(e);
            Vector2 perto = p.Jogador.Pos + new Vector2(0, 4f);
            if (!p.Mundo.Grade.LivreMaisProximo(perto, 0.7f, out perto)) throw new Exception("sem espaco");
            Entidade t = p.Mundo.CriarInimigo(p.Catalogo.Inimigo("tecelao"), 3, perto);
            float vida = p.Jogador.Vida;
            bool envenenou = false;
            for (int i = 0; i < 60 * 15 && !envenenou; i++)
            {
                p.Passo(1f / 60f);
                envenenou = p.Jogador.TemEfeito("veneno");
            }
            Assert.True(p.Jogador.Vida < vida, "o tecelao nunca acertou");
            Assert.True(envenenou, "o ferrao nunca envenenou");
        }

        [Fact]
        public void RolamentoDeixaInvulneravel()
        {
            Partida p = Apoio.Partida("corredor");
            Apoio.PularCenas(p);
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            Entidade j = p.Jogador;
            p.Esquivar(j.Pos + new Vector2(0, 3));
            p.Passo(1f / 60f);
            Assert.Equal(EstadoAcao.Esquivando, j.Acao);
            Assert.True(j.Invulneravel > 0);
            float vida = j.Vida;
            p.Mundo.Atingir(null, j, p.Catalogo.Poder("mordida"), 1, j.Pos);
            Assert.Equal(vida, j.Vida);
        }

        [Fact]
        public void NoiteFechaOPortaoSoltaNoturnosEMexeOsMuros()
        {
            Partida p = Apoio.Partida("guardiao", 31337);
            Apoio.PularCenas(p);
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            int antes = p.Mundo.Entidades.Count(e => e.Tipo == "tecelao_noturno");
            var mudou = new List<string>();
            p.Registro.Registrar(new Escuta(mudou));
            p.Ciclo.Definir(1, 19.4f);
            // Tira o jogador do caminho do portao para ele poder fechar.
            p.Jogador.Pos = p.Mapa.Planta.CentroDaCelula(p.Mapa.Planta.Entrada.x, 1);
            ObjetoDeCena portao = p.Mundo.Objetos.First(o => o.Chave == "setor1:portao_patio");
            Assert.True(portao.Ligado);
            float segundosAteMeiaNoite = (4.6f) * p.Ciclo.SegundosDeNoite / 10f + 0.6f * p.Ciclo.SegundosDeDia / 14f;
            for (int i = 0; i < (int)(segundosAteMeiaNoite * 20); i++)
            {
                p.Jogador.Vida = p.Jogador.VidaMax;
                p.Passo(1f / 20f);
            }
            Assert.True(p.Ciclo.Noite);
            Assert.False(portao.Ligado, "portao do patio devia estar fechado de noite");
            Assert.True(p.Mundo.Entidades.Count(e => e.Tipo == "tecelao_noturno") > antes, "noite sem noturnos");
            Assert.Contains("muros", mudou);
        }

        private sealed class Escuta : IExecutor<NotCiclo>
        {
            private readonly List<string> _l;
            public Escuta(List<string> l) => _l = l;
            public void Executar(NotCiclo c) => _l.Add(c.Evento);
        }

        [Fact]
        public void MorrerLevaAEnfermariaECustaSucata()
        {
            Partida p = Apoio.Partida("corredor");
            Apoio.PularCenas(p);
            p.GanharSucata(100);
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            p.Mundo.Matar(p.Jogador, null);
            Assert.True(p.JogadorMorto);
            Assert.Equal(85, p.Heroi.Sucata);
            p.Renascer();
            Apoio.PularCenas(p);
            Assert.Equal("patio", p.Mapa.Id);
            Assert.False(p.JogadorMorto);
            Assert.Equal(p.Jogador.VidaMax, p.Jogador.Vida, 0);
        }

        [Fact]
        public void SalvarECarregarDevolveAMesmaPartida()
        {
            Partida p = Apoio.Partida("remendeiro", 2024);
            Apoio.PularCenas(p);
            Falar(p, "iara");
            p.GanharXp(500);
            p.GanharSucata(77);
            p.Heroi.Mochila.Adicionar(p.Loot.Criar(p.Catalogo.Item("machete"), 6, Raridade.Raro, new Random(1)), p.Catalogo);
            p.Guarda.Adicionar(new Item("soro", 3), p.Catalogo);
            p.EstadosDeObjetos["setor1:1:bau:0"] = true;
            p.Diario.DefinirStatus("teste_status");
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            string texto = p.Salvar();

            var q = new Partida(Apoio.Catalogo(), () => Apoio.Arquivos("Roteiros"));
            q.Carregar(texto);
            Apoio.PularCenas(q);
            Assert.Equal(texto, q.Salvar().Replace(q.TempoDeJogo.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), p.TempoDeJogo.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal("setor1", q.Mapa.Id);
            Assert.Equal(p.Heroi.Nivel, q.Heroi.Nivel);
            Assert.Equal(p.Heroi.Sucata, q.Heroi.Sucata);
            Assert.True(q.Diario.TemStatus("teste_status"));
            Assert.Equal(3, q.Guarda.Contar("soro"));
        }

        [Fact]
        public void LojaVendeComprarECobra()
        {
            Partida p = Apoio.Partida("corredor");
            Apoio.PularCenas(p);
            p.GanharSucata(200);
            List<Item> estoque = p.Estoque("enfermaria");
            int i = estoque.FindIndex(x => x.Base == "soro");
            int antes = p.Heroi.Sucata;
            Assert.True(p.Comprar("enfermaria", i));
            Assert.Equal(1, p.Heroi.Mochila.Contar("soro"));
            Assert.True(p.Heroi.Sucata < antes);
            int espaco = Array.FindIndex(p.Heroi.Mochila.Espacos, x => x?.Base == "soro");
            Assert.True(p.Vender("enfermaria", espaco));
            Assert.Equal(0, p.Heroi.Mochila.Contar("soro"));
        }

        [Fact]
        public void BancadaFabricaComMaterial()
        {
            Partida p = Apoio.Partida("corredor");
            Apoio.PularCenas(p);
            Receita r = p.ReceitasDa("enfermaria").First(x => x.Id == "r_bandagem");
            Assert.NotNull(p.MotivoParaNaoFabricar(r));
            p.Heroi.Mochila.Adicionar(new Item("trapo", 4), p.Catalogo);
            int antes = p.Heroi.Mochila.Contar("bandagem");
            Assert.True(p.Fabricar(r));
            Assert.Equal(antes + 2, p.Heroi.Mochila.Contar("bandagem"));
            Assert.Equal(2, p.Heroi.Mochila.Contar("trapo"));
        }

        [Fact]
        public void PoderesDeCadaClasseSaemSemErro()
        {
            foreach (string classe in new[] { "corredor", "guardiao", "remendeiro" })
            {
                Partida p = Apoio.Partida(classe, 99);
                Apoio.PularCenas(p);
                p.EntrarNoMapa("setor1", "patio");
                Apoio.PularCenas(p);
                p.Heroi.Nivel = 20;
                p.Heroi.PontosDePoder = 0;
                foreach (Poder poder in p.Catalogo.Poderes.Values.Where(x => x.Arvore == classe)) p.Heroi.Poderes[poder.Id] = 3;
                p.RecalcularFicha();
                foreach (Poder poder in p.Catalogo.Poderes.Values.Where(x => x.Arvore == classe))
                {
                    if (!poder.AceitaArma(p.Jogador.ClasseDaArma)) continue;
                    p.Jogador.Energia = p.Jogador.EnergiaMax;
                    p.Jogador.Recargas.Clear();
                    p.UsarPoder(poder.Id, p.Jogador.Pos + new Vector2(0, 4), 0);
                    Apoio.Rodar(p, 1.5f);
                    p.Jogador.Vida = p.Jogador.VidaMax;
                }
                Assert.True(p.Jogador.Vivo);
            }
        }

        [Fact]
        public void ChefeDoSetorUmMorreLibertaAPassagemEDaONucleo()
        {
            Partida p = Apoio.Partida("guardiao", 5150);
            Apoio.PularCenas(p);
            p.Diario.Iniciar("m02_hera");
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            Entidade chefe = p.Mundo.Entidades.First(e => e.Chefe);
            p.Mundo.Atingir(p.Jogador, chefe, new Poder { Id = "teste", Fonte = FonteDeDano.Nenhuma, DanoFixoMin = 99999, DanoFixoMax = 99999 }, 1, chefe.Pos);
            Apoio.PularCenas(p);
            Assert.False(chefe.Vivo);
            Assert.True(p.Diario.TemStatus("chefe_setor1"));
            Assert.Equal(1, p.Heroi.Mochila.Contar("nucleo_do_alfa"));
            Assert.True(p.Mundo.Loot.Count >= 3, "chefe sem loot");
        }
    }
}
