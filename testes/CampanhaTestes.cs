using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using Xunit;
using Xunit.Abstractions;

namespace Esquecidos.Testes
{
    /// <summary>
    /// A historia principal inteira, do caixote ao console da ARCA, pelos
    /// mesmos caminhos do jogador: conversa por topico, portao de verdade,
    /// alavanca, chefe, marco, entrega. So o andar e o bater sao atalhos.
    /// </summary>
    public class CampanhaTestes
    {
        private readonly ITestOutputHelper _saida;

        public CampanhaTestes(ITestOutputHelper saida)
        {
            _saida = saida;
        }

        private static readonly Poder Golpe = new Poder { Id = "teste", Fonte = FonteDeDano.Nenhuma, DanoFixoMin = 99999, DanoFixoMax = 99999 };

        private void Log(Partida p, string texto) => _saida.WriteLine($"[dia {p.Ciclo.Dia} {p.Ciclo.Hora:00.0}h {p.Mapa?.Id}] {texto}");

        /// <summary>Escolhe o topico pelo roteiro, como o clique no botao do dialogo.</summary>
        private static void Topico(Partida p, string npc, string roteiro, int escolha = 0)
        {
            Entidade e = p.Mundo.Entidades.FirstOrDefault(x => x.NpcId == npc);
            Assert.True(e != null, $"{npc} nao esta em {p.Mapa.Id}");
            List<DialogoDef> topicos = p.Diario.Topicos(npc);
            DialogoDef t = topicos.FirstOrDefault(x => x.Roteiro == roteiro);
            Assert.True(t != null, $"{npc} sem o topico {roteiro}; tem: {string.Join(", ", topicos.Select(x => x.Roteiro))}");
            p.Jogador.Pos = e.Pos + new Vector2(1.2f, 0);
            p.EscolherTopico(t);
            Apoio.PularCenas(p, escolha);
        }

        private static void Matar(Partida p, Entidade e, int escolha = 0)
        {
            p.Jogador.Pos = p.Mundo.Grade.LivreMaisProximo(e.Pos + new Vector2(1.2f, 0), p.Jogador.Raio, out Vector2 livre) ? livre : p.Jogador.Pos;
            p.Mundo.Atingir(p.Jogador, e, Golpe, 1, e.Pos);
            Apoio.Rodar(p, 0.3f);
            Apoio.PularCenas(p, escolha);
            Assert.False(e.Vivo, $"{e.Tipo} nao morreu");
        }

        private static void Interagir(Partida p, ObjetoDeCena o)
        {
            p.Jogador.Pos = p.Mundo.Grade.LivreMaisProximo(o.Pos + o.FrenteDoObjeto() * 1.0f, p.Jogador.Raio, out Vector2 livre) ? livre : o.Pos;
            p.InteragirCom(o.Id);
            Apoio.Rodar(p, 1.0f);
            Apoio.PularCenas(p);
        }

        /// <summary>Pisa na saida e espera o mapa trocar.</summary>
        private static void Sair(Partida p, Func<ObjetoDeCena, bool> qual, string esperado)
        {
            ObjetoDeCena s = p.Mundo.Objetos.FirstOrDefault(o => o.Tipo == TipoObjeto.Saida && qual(o));
            Assert.True(s != null, $"sem saida para {esperado} em {p.Mapa.Id}: {string.Join(", ", p.Mundo.Objetos.Where(o => o.Tipo == TipoObjeto.Saida).Select(o => o.Destino))}");
            p.Jogador.Pos = s.Pos;
            for (int i = 0; i < 240 && p.Mapa.Id != esperado; i++) p.Passo(1f / 60f);
            Assert.True(p.Mapa.Id == esperado, $"pisou na saida e continuou em {p.Mapa.Id} (esperava {esperado})");
            Apoio.PularCenas(p);
        }

        private static void PeloPortao(Partida p, string lado, string setor)
        {
            if (p.Mapa.Id != "patio") Sair(p, o => o.Destino == "patio", "patio");
            // Portao so abre de dia: espera a manha se precisar, como o jogador dormindo na rede.
            if (!p.Ciclo.PortoesAbertos) p.Ciclo.DormirAteAmanhecer();
            p.Ciclo.Definir(p.Ciclo.Dia, Math.Max(p.Ciclo.Hora, 8f));
            p.EntrarNoMapa("patio", lado);
            Apoio.PularCenas(p);
            ObjetoDeCena portao = p.Mundo.Objetos.First(o => o.Chave == "patio:portao_" + lado);
            Assert.True(portao.Ligado, $"portao {lado} fechado (status: {string.Join(", ", p.Diario.Status)})");
            Sair(p, o => o.Destino == "portao:" + lado, setor);
        }

        /// <summary>Puxa as alavancas que a grade da arena pede, derruba o chefe e pega a placa do marco.</summary>
        private void ChefeEMarco(Partida p, string setor)
        {
            Entidade chefe = p.Mundo.Entidades.First(e => e.Chefe && e.Vivo);
            ObjetoDeCena grade = p.Mundo.Objetos.FirstOrDefault(o => o.Tipo == TipoObjeto.Porta && o.Dependencias.Count > 0);
            if (grade != null)
            {
                foreach (int id in grade.Dependencias)
                {
                    ObjetoDeCena alavanca = p.Mundo.Objetos.First(o => o.Id == id);
                    if (!alavanca.Ligado) Interagir(p, alavanca);
                    Assert.True(alavanca.Ligado, $"alavanca {alavanca.Id} nao ligou");
                }
                Apoio.Rodar(p, 0.5f);
                Assert.True(grade.Ligado, "grade da arena nao abriu com as alavancas");
            }
            Log(p, $"chefe {chefe.Tipo} com {chefe.VidaMax:0} de vida");
            Matar(p, chefe);
            Assert.True(p.Diario.TemStatus("chefe_" + setor), "status do chefe nao veio");
            ObjetoDeCena marco = p.Mundo.Objetos.FirstOrDefault(o => o.Tipo == TipoObjeto.Marco);
            if (marco != null) Interagir(p, marco);
        }

        [Theory]
        [InlineData("corredor", 0, 0)]
        [InlineData("guardiao", 1, 1)]
        [InlineData("remendeiro", 0, 1)]
        public void HistoriaPrincipalDoCaixoteAoFinal(string classe, int gravador, int final)
        {
            Partida p = Apoio.Partida(classe, 4242 + gravador * 10 + final);
            var erros = new List<string>();
            p.Roteiros.Erro += m => erros.Add(m);
            Apoio.PularCenas(p);

            // Primeiro dia.
            Topico(p, "iara", "iara_m01");
            Topico(p, "duda", "duda_m01");
            Topico(p, "marreta", "marreta_m01");
            foreach (Entidade b in p.Mundo.Entidades.Where(e => e.Tipo == "boneco").ToList()) Matar(p, b);
            Topico(p, "teo", "teo_pronto");
            Assert.True(p.Diario.Concluida("m01_primeiro_dia"));
            Log(p, "primeiro dia concluido");

            // Setor 1: cinco jovens, o Ferrao, a placa, o nucleo para o Caio.
            PeloPortao(p, "leste", "setor1");
            Assert.NotNull(p.Companheiro);
            foreach (Entidade j in p.Mundo.Entidades.Where(e => e.Tipo == "tecelao_jovem" && e.Vivo).Take(5).ToList()) Matar(p, j);
            Assert.True(p.Diario.Etapa("m02_hera") >= 2, $"m02 parou na etapa {p.Diario.Etapa("m02_hera")}");
            ChefeEMarco(p, "setor1");
            Assert.Equal(1, p.Heroi.Mochila.Contar("nucleo_do_alfa"));
            Assert.Equal(1, p.Heroi.Mochila.Contar("fragmento_hera"));
            Sair(p, o => o.Destino == "patio", "patio");
            Topico(p, "caio", "caio_m02");
            Apoio.PularCenas(p);
            Assert.True(p.Diario.Concluida("m02_hera"), $"m02 na etapa {p.Diario.Etapa("m02_hera")}");
            Assert.True(p.Diario.Ativa("m03_padrao"));
            Log(p, "Corredor");

            // Setor 2: Tecela-Mae; a placa destrava o oeste. Na volta, o gravador sobe.
            PeloPortao(p, "sul", "setor2");
            ChefeEMarco(p, "setor2");
            Assert.True(p.Diario.TemStatus("portao_oeste"));
            Sair(p, o => o.Destino == "patio", "patio");
            Apoio.PularCenas(p);
            Assert.True(p.Diario.TemStatus("recebeu_gravador"), "o gravador nao chegou na volta ao Patio");
            ObjetoDeCena montaCargas = p.Mundo.Objetos.First(o => o.Roteiro == "monta_cargas");
            Interagir(p, montaCargas);
            Assert.True(p.Diario.TemStatus("ouviu_gravador"));
            Topico(p, "iara", "iara_gravador", gravador);
            Assert.True(p.Diario.Concluida("m04_gravador"));
            Log(p, gravador == 0 ? "contou a verdade" : "escondeu a verdade");

            // Setor 3 e 4.
            PeloPortao(p, "oeste", "setor3");
            ChefeEMarco(p, "setor3");
            Assert.True(p.Diario.TemStatus("portao_norte"));
            PeloPortao(p, "norte", "setor4");
            ChefeEMarco(p, "setor4");
            Sair(p, o => o.Destino == "patio", "patio");
            Topico(p, "caio", "caio_m03");
            Apoio.PularCenas(p);
            Assert.True(p.Diario.TemStatus("padrao_decifrado"));
            Assert.True(p.Diario.Ativa("m05_saida"));
            Log(p, "padrao decifrado");

            // O Nucleo, por baixo do setor 4, e o laboratorio.
            PeloPortao(p, "norte", "setor4");
            Entidade ceifador = p.Mundo.Entidades.FirstOrDefault(e => e.Chefe && e.Vivo);
            Assert.True(ceifador == null, "o Ceifador voltou a viver num dia novo");
            Sair(p, o => o.Destino == "setor5", "setor5");
            ChefeEMarco(p, "setor5");
            Sair(p, o => o.Destino == "laboratorio", "laboratorio");
            if (gravador == 0) Log(p, $"companheiro no laboratorio: {p.CompanheiroNpc ?? "ninguem"}");
            Entidade executor = p.Mundo.Entidades.First(e => e.Chefe && e.Vivo);
            Matar(p, executor, final);
            Assert.True(p.Diario.TemStatus("final_escolhido"), "final nao foi escolhido");
            Assert.True(p.Diario.TemStatus(final == 0 ? "final_libertacao" : "final_arquiteto"));
            Assert.True(p.Diario.Concluida("m05_saida"), $"m05 na etapa {p.Diario.Etapa("m05_saida")}");
            Log(p, $"fim: nivel {p.Heroi.Nivel}, {p.Heroi.Sucata} de sucata, {p.TempoDeJogo / 60:0} min de jogo simulado");
            Assert.Empty(erros);
        }
    }
}
