using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Roteiro;
using Xunit;

namespace Esquecidos.Testes
{
    /// <summary>
    /// O conteudo e texto escrito a mao: id errado, roteiro que nao existe,
    /// rotulo trocado. Tudo isso so apareceria no meio da partida; aqui
    /// aparece com o nome do que falta.
    /// </summary>
    public class DadosTestes
    {
        [Fact]
        public void CatalogoCarregaEConfereTodaReferencia()
        {
            Catalogo c = Apoio.Catalogo();
            var p = new Partida(c, () => Apoio.Arquivos("Roteiros"));
            List<string> erros = c.Validar(p.Roteiros.Existe);
            Assert.True(erros.Count == 0, string.Join("\n", erros));
            Assert.Equal(3, c.Classes.Count);
            Assert.True(c.Itens.Count > 80, $"so {c.Itens.Count} itens");
            Assert.True(c.Setores.Count >= 6);
        }

        [Fact]
        public void TodoRoteiroUsaComandoConhecidoERotuloExistente()
        {
            var p = new Partida(Apoio.Catalogo(), () => Apoio.Arquivos("Roteiros"));
            List<string> erros = p.Roteiros.Validar();
            Assert.True(erros.Count == 0, string.Join("\n", erros));
            Assert.True(p.Roteiros.Roteiros.Count > 60, $"so {p.Roteiros.Roteiros.Count} roteiros");
        }

        [Fact]
        public void CadaSetorTemRoteiroDeMarcoEChefeTemRoteiroDeMorte()
        {
            Catalogo c = Apoio.Catalogo();
            var p = new Partida(c, () => Apoio.Arquivos("Roteiros"));
            foreach (SetorDef s in c.Setores.Values)
            {
                Assert.True(p.Roteiros.Existe("marco_" + s.Id), "falta @marco_" + s.Id);
                if (s.Chefe != null) Assert.True(p.Roteiros.Existe("chefe_" + s.Chefe), "falta @chefe_" + s.Chefe);
                Assert.NotNull(s.StatusDoChefe);
            }
        }

        [Fact]
        public void ToCondicoesDosRoteirosSaoConhecidas()
        {
            // Avalia cada "se" de todos os roteiros numa partida nova: condicao
            // desconhecida estoura aqui, nao na frente do jogador.
            Partida p = Apoio.Partida();
            foreach (Roteiro r in p.Roteiros.Roteiros.Values)
            foreach (LinhaDeRoteiro l in r.Linhas.Where(x => x.Comando == "se"))
            {
                string[] cond = l.Args.TakeWhile(a => a != "->").ToArray();
                if (cond[0] == "nao") cond = cond.Skip(1).ToArray();
                p.Avaliar(cond[0], cond.Skip(1).ToArray());
            }
        }

        [Fact]
        public void ArmasTemDanoEEquipaveisTemLugar()
        {
            Catalogo c = Apoio.Catalogo();
            foreach (ItemBase b in c.Itens.Values)
            {
                if (b.Tipo == TipoItem.Arma) Assert.True(b.DanoMax >= b.DanoMin && b.DanoMax > 0, b.Id);
                if (b.Equipavel) Assert.NotNull(Personagem.SlotPara(b));
                if (b.Tipo == TipoItem.Consumivel) Assert.True(b.Uso != null, b.Id + " sem uso");
            }
        }

        [Fact]
        public void CadaArvoreDeClasseTemOitoPoderesSemColisaoDePosicao()
        {
            Catalogo c = Apoio.Catalogo();
            foreach (string arvore in new[] { "corredor", "guardiao", "remendeiro" })
            {
                List<Poder> ps = c.Poderes.Values.Where(p => p.Arvore == arvore).ToList();
                Assert.Equal(8, ps.Count);
                Assert.Equal(ps.Count, ps.Select(p => (p.Coluna, p.Linha)).Distinct().Count());
                foreach (Poder p in ps.Where(x => x.RequerPoder != null))
                    Assert.Equal(arvore, c.Poder(p.RequerPoder).Arvore);
            }
        }

        [Fact]
        public void PoderNoRankCresceSemMexerNoOriginal()
        {
            Poder p = Apoio.Catalogo().Poder("corte_duplo");
            Poder r3 = p.NoRank(3);
            Assert.Equal(1.45f + 0.3f, r3.Multiplicador, 3);
            Assert.Equal(1.45f, p.Multiplicador, 3);
            Assert.Equal(p.Efeito.Valor + 2, r3.Efeito.Valor, 3);
        }

        [Fact]
        public void LeitorIniGuardaChavesRepetidasEmOrdem()
        {
            List<Secao> s = LeitorIni.Ler("# x\n[a]\nk=1\nk=2\n\n[b]\nnome=Olá, mundo\n", "t");
            Assert.Equal(2, s.Count);
            Assert.Equal(new[] { "1", "2" }, s[0].Todos("k").ToArray());
            Assert.Equal("Olá, mundo", s[1].Texto("nome"));
        }

        [Fact]
        public void TokenizadorRespeitaAspasEComentario()
        {
            List<string> t = LeitorDeRoteiro.Tokenizar("fala \"Dra. Vasco\" \"Ela disse \\\"oi\\\"\" # comentario");
            Assert.Equal(new[] { "fala", "Dra. Vasco", "Ela disse \"oi\"" }, t.ToArray());
        }

        [Fact]
        public void FabricaPreencheArgumentoOpcional()
        {
            var f = new FabricaDeComandos();
            f.RegistrarDe(typeof(Fala).Assembly);
            var c = (DarItem)f.Criar(new LinhaDeRoteiro { Comando = "da_item", Args = new[] { "bandagem" }, Arquivo = "t" });
            Assert.Equal(1, c.Quantidade);
            var l = (Legenda)f.Criar(new LinhaDeRoteiro { Comando = "legenda", Args = new[] { "oi", "2.5" }, Arquivo = "t" });
            Assert.Equal(2.5f, l.Segundos);
        }
    }
}
