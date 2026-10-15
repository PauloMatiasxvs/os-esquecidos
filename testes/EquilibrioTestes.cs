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
    /// O ritmo do comeco, como no Flare: bicho comum do primeiro setor cai em
    /// tres ou quatro golpes de quem acabou de sair da caixa.
    /// </summary>
    public class EquilibrioTestes
    {
        private readonly ITestOutputHelper _saida;

        public EquilibrioTestes(ITestOutputHelper saida)
        {
            _saida = saida;
        }

        private static float GolpesParaMatar(Partida p, string tipo, int nivel, int tentativas = 300)
        {
            InimigoDef def = p.Catalogo.Inimigos[tipo];
            Poder golpe = p.PoderBasico();
            int total = 0;
            for (int i = 0; i < tentativas; i++)
            {
                Entidade e = p.Mundo.CriarInimigo(def, nivel, p.Jogador.Pos + new Vector2(1.2f, 0));
                e.Cerebro = null;
                int golpes = 0;
                while (e.Vivo && golpes < 100)
                {
                    p.Mundo.Atingir(p.Jogador, e, golpe, 1, p.Jogador.Pos);
                    golpes++;
                }
                total += golpes;
                p.Mundo.Remover(e);
            }
            return total / (float)tentativas;
        }

        [Theory]
        [InlineData("corredor")]
        [InlineData("guardiao")]
        [InlineData("remendeiro")]
        public void BichoDoSetorUmCaiEmPoucosGolpesNoNivelUm(string classe)
        {
            Partida p = Apoio.Partida(classe, 99);
            Apoio.PularCenas(p);
            p.EntrarNoMapa("setor1", "patio");
            Apoio.PularCenas(p);
            int nivel = p.Catalogo.Setor("setor1").Nivel;
            foreach (string tipo in new[] { "tecelao_jovem", "tecelao_cuspidor", "tecelao" })
            {
                float golpes = GolpesParaMatar(p, tipo, nivel);
                _saida.WriteLine($"{classe}: {tipo} nv {nivel} cai em {golpes:0.0} golpes");
                Assert.InRange(golpes, 1.5f, tipo == "tecelao" ? 5.5f : 4.5f);
            }
        }
    }
}
