using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using Xunit;

namespace Esquecidos.Testes
{
    public class ComandosTestes
    {
        private sealed record Ping(int N) : IComando;

        private sealed class Ouvinte : IExecutor<Ping>
        {
            public int Soma;
            public void Executar(Ping c) => Soma += c.N;
        }

        [Fact]
        public void DespachaParaTodosOsExecutoresEPeloCaminhoDinamico()
        {
            var d = new Despachante(new RegistroDeExecutores());
            var a = new Ouvinte();
            var b = new Ouvinte();
            d.Registro.Registrar(a);
            d.Registro.Registrar(b);
            Assert.True(d.Despachar(new Ping(2)));
            Assert.True(d.DespacharDinamico(new Ping(3)));
            Assert.Equal(5, a.Soma);
            Assert.Equal(5, b.Soma);
            d.Registro.Remover(a);
            d.Despachar(new Ping(10));
            Assert.Equal(5, a.Soma);
        }
    }

    public class GradeTestes
    {
        [Fact]
        public void ColisaoEmpurraCirculoParaForaDaParede()
        {
            var g = new Grade(10, 10);
            g.Bloquear(new Retangulo(5, 0, 6, 10));
            Vector2 p = g.ResolverColisao(new Vector2(4.9f, 5), 0.4f);
            Assert.True(p.X <= 4.61f, $"x={p.X}");
        }

        [Fact]
        public void LinhaDeVisaoParaNaParede()
        {
            var g = new Grade(10, 10);
            g.Bloquear(new Retangulo(5, 2, 6, 8));
            Assert.False(g.LinhaDeVisao(new Vector2(2, 5), new Vector2(8, 5)));
            Assert.True(g.LinhaDeVisao(new Vector2(2, 1), new Vector2(8, 1)));
        }

        [Fact]
        public void CaminhoContornaParedeERespeitaFolga()
        {
            var g = new Grade(20, 20);
            g.Bloquear(new Retangulo(9, 0, 10, 16));
            List<Vector2> c = Caminhos.Buscar(g, new Vector2(4, 4), new Vector2(15, 4), 0.4f);
            Assert.NotNull(c);
            Assert.True(c.Any(p => p.Y > 16), "o caminho tem de passar por cima da parede");
            Vector2 anterior = new Vector2(4, 4);
            foreach (Vector2 p in c)
            {
                Assert.True(g.CaminhoLivre(anterior, p, 0.3f), $"trecho {anterior}->{p} raspa na parede");
                anterior = p;
            }
        }

        [Fact]
        public void FolgaMedeDistanciaAteParede()
        {
            var g = new Grade(20, 20);
            g.Bloquear(new Retangulo(10, 0, 11, 20));
            Assert.InRange(g.FolgaEm(new Vector2(8.25f, 10)), 1.5f, 2.0f);
            Assert.Equal(0, g.FolgaEm(new Vector2(10.5f, 10)));
        }
    }

    public class InventarioTestes
    {
        [Fact]
        public void EmpilhaAteOLimiteEDepoisOcupaEspacoNovo()
        {
            Catalogo c = Apoio.Catalogo();
            var inv = new Inventario(4);
            Assert.Null(inv.Adicionar(new Item("bandagem", 7), c));
            Assert.Null(inv.Adicionar(new Item("bandagem", 7), c));
            Assert.Equal(14, inv.Contar("bandagem"));
            Assert.Equal(2, inv.Espacos.Count(i => i != null));
            Assert.True(inv.Remover("bandagem", 12));
            Assert.Equal(2, inv.Contar("bandagem"));
        }

        [Fact]
        public void ArmaDeDuasMaosTiraOEscudo()
        {
            Catalogo c = Apoio.Catalogo();
            Personagem p = Personagem.Novo(c, "guardiao", "t");
            p.Desequipar(SlotEquip.Arma, c);
            p.Mochila.Adicionar(new Item("tampa_de_lixeira"), c);
            int escudo = System.Array.FindIndex(p.Mochila.Espacos, i => i?.Base == "tampa_de_lixeira");
            Assert.True(p.Equipar(escudo, c, out _));
            int cano = System.Array.FindIndex(p.Mochila.Espacos, i => i?.Base == "cano_ferro");
            Assert.True(p.Equipar(cano, c, out _));
            Assert.Null(p.Equipado[(int)SlotEquip.Secundaria]);
            Assert.Equal(1, p.Mochila.Contar("tampa_de_lixeira"));
        }

        [Fact]
        public void RequisitoDeAtributoBloqueiaEquipar()
        {
            Catalogo c = Apoio.Catalogo();
            Personagem p = Personagem.Novo(c, "remendeiro", "t");
            p.Mochila.Adicionar(new Item("marreta"), c);
            int i = System.Array.FindIndex(p.Mochila.Espacos, x => x?.Base == "marreta");
            Assert.False(p.Equipar(i, c, out string motivo));
            Assert.Contains("Físico", motivo);
        }
    }

    public class LootTestes
    {
        [Fact]
        public void RaridadeSegueAsFaixasEAfixosBatemComARaridade()
        {
            Catalogo c = Apoio.Catalogo();
            var g = new GeradorDeLoot(c);
            var rng = new System.Random(7);
            var contagem = new Dictionary<Raridade, int>();
            for (int i = 0; i < 4000; i++)
            {
                Item it = g.Equipamento(8, rng, 0);
                contagem.TryGetValue(it.Raridade, out int n);
                contagem[it.Raridade] = n + 1;
                switch (it.Raridade)
                {
                    case Raridade.Comum: Assert.Empty(it.Afixos); break;
                    case Raridade.Reforcado: Assert.Single(it.Afixos); break;
                    case Raridade.Raro: Assert.InRange(it.Afixos.Count, 2, 3); break;
                    case Raridade.Reliquia: Assert.InRange(it.Afixos.Count, 3, 4); break;
                }
                Assert.False(string.IsNullOrWhiteSpace(DescricaoDeItem.Nome(it, c)));
                Assert.True(it.Nivel >= c.Item(it.Base).Nivel);
            }
            Assert.InRange(contagem[Raridade.Comum], 2400, 3100);
            Assert.True(contagem.ContainsKey(Raridade.Reliquia));
        }

        [Fact]
        public void NomeConcordaComOGenero()
        {
            Catalogo c = Apoio.Catalogo();
            var faca = new Item("faca_cozinha") { Raridade = Raridade.Reforcado, Afixos = { new AfixoRolado { Afixo = "afiado", Valor = 2 } } };
            var cano = new Item("cano_ferro") { Raridade = Raridade.Reforcado, Afixos = { new AfixoRolado { Afixo = "afiado", Valor = 2 } } };
            Assert.Equal("Faca de Cozinha Afiada", DescricaoDeItem.Nome(faca, c));
            Assert.Equal("Cano de Ferro Afiado", DescricaoDeItem.Nome(cano, c));
        }
    }

    public class RegrasTestes
    {
        [Fact]
        public void NivelSobeComXpEDaPontos()
        {
            Catalogo c = Apoio.Catalogo();
            Personagem p = Personagem.Novo(c, "corredor", "t");
            int subiu = p.GanharXp(c.Regras.XpTotalPara(4), c.Regras);
            Assert.Equal(3, subiu);
            Assert.Equal(4, p.Nivel);
            Assert.Equal(9, p.PontosDeAtributo);
            Assert.Equal(3, p.PontosDePoder);
        }

        [Fact]
        public void FichaDoCorredorFazSentido()
        {
            Catalogo c = Apoio.Catalogo();
            Ficha f = Personagem.Novo(c, "corredor", "t").Calcular(c);
            Assert.InRange(f[Estat.Vida], 70, 100);
            Assert.InRange(f[Estat.Precisao], 85, 100);
            Assert.True(f[Estat.DanoCorpoMax] > f[Estat.DanoCorpoMin]);
        }
    }
}
