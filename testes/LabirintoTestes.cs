using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Simulacao;
using Xunit;
using Xunit.Abstractions;

namespace Esquecidos.Testes
{
    /// <summary>
    /// O labirinto e gerado; o que importa e verificar, nao torcer: toda
    /// celula alcancavel pela grade de colisao de verdade (com pilar, enfeite
    /// e tudo), arena so pela grade, alavancas fora da arena, e nenhuma
    /// combinacao de muros moveis isolando alguem.
    /// </summary>
    public class LabirintoTestes
    {
        private readonly ITestOutputHelper _saida;

        public LabirintoTestes(ITestOutputHelper saida)
        {
            _saida = saida;
        }

        public static IEnumerable<object[]> Setores()
        {
            foreach (string s in new[] { "setor1", "setor2", "setor3", "setor4", "setor5", "laboratorio" })
            foreach (int dia in new[] { 1, 2, 3, 5, 7, 11, 13, 19 })
                yield return new object[] { s, dia };
        }

        private static Mapa Gerar(string setor, int dia, out Catalogo c)
        {
            c = Apoio.Catalogo();
            return GeradorDeSetor.Gerar(c.Setor(setor), 1000 + dia * 31 + Esquecidos.Nucleo.Hash.Estavel(setor) % 97, dia);
        }

        /// <summary>Aplica os bloqueios dos objetos como o Mundo faria ao recebe-los.</summary>
        private static void AplicarObjetos(Mapa m, Func<ObjetoDeCena, bool> fechado)
        {
            foreach (ObjetoDeCena o in m.Objetos.Where(o => o.TemBloqueio))
            {
                bool bloqueia = fechado(o);
                if (bloqueia) m.Grade.Bloquear(o.Bloqueio);
            }
        }

        [Theory]
        [MemberData(nameof(Setores))]
        public void TodaCelulaForaDaArenaEAlcancavelAndando(string setor, int dia)
        {
            Mapa m = Gerar(setor, dia, out _);
            PlantaDoLabirinto pl = m.Planta;
            // Pior caso: grade da arena fechada, todos os muros moveis erguidos ao mesmo tempo.
            AplicarObjetos(m, o => o.Tipo == TipoObjeto.MuroMovel || (o.Tipo == TipoObjeto.Porta && o.Dependencias.Count > 0) || o.Tipo == TipoObjeto.Bau || o.Tipo == TipoObjeto.Terminal || o.Tipo == TipoObjeto.Marco);
            Vector2 inicio = m.Entradas["patio"].Pos;
            Assert.True(m.Grade.FolgaEm(inicio) >= 0.42f, "a entrada esta encostada em parede");
            int falhas = 0;
            for (int x = 0; x < pl.Colunas; x++)
            for (int z = 0; z < pl.Linhas; z++)
            {
                bool arena = x >= pl.Arena.x && x < pl.Arena.x + 3 && z >= pl.Arena.z && z < pl.Arena.z + 3;
                Vector2 alvo = pl.CentroDaCelula(x, z);
                List<Vector2> c = Caminhos.Buscar(m.Grade, inicio, alvo, 0.42f, 200000);
                bool chegou = c != null && Vector2.Distance(c[c.Count - 1], alvo) < 2.5f;
                if (arena)
                {
                    Assert.False(chegou, $"arena ({x},{z}) alcancavel com a grade fechada");
                    continue;
                }
                if (!chegou)
                {
                    falhas++;
                    _saida.WriteLine($"{setor} dia {dia}: celula ({x},{z}) inalcancavel");
                }
            }
            Assert.Equal(0, falhas);
        }

        [Theory]
        [MemberData(nameof(Setores))]
        public void ArenaAbreComAGradeEOChefeNasceLaDentro(string setor, int dia)
        {
            Mapa m = Gerar(setor, dia, out Catalogo c);
            AplicarObjetos(m, o => o.Tipo == TipoObjeto.Bau || o.Tipo == TipoObjeto.Marco || o.Tipo == TipoObjeto.Terminal || (o.Tipo == TipoObjeto.MuroMovel && o.Ligado));
            Surgimento chefe = m.Surgimentos.Single(s => s.Chefe);
            Assert.Equal(c.Setor(setor).Chefe, chefe.Inimigo);
            List<Vector2> caminho = Caminhos.Buscar(m.Grade, m.Entradas["patio"].Pos, chefe.Pos, 0.42f, 200000);
            Assert.NotNull(caminho);
            Assert.True(Vector2.Distance(caminho[caminho.Count - 1], chefe.Pos) < 3f);
            float raioChefe = c.Inimigo(chefe.Inimigo).Raio;
            Assert.True(m.Grade.FolgaEm(chefe.Pos) >= raioChefe * 0.9f, $"chefe nasce espremido (folga {m.Grade.FolgaEm(chefe.Pos):0.00}, raio {raioChefe})");
        }

        [Theory]
        [MemberData(nameof(Setores))]
        public void AlavancasFicamForaDaArenaEApontamParaAGrade(string setor, int dia)
        {
            Mapa m = Gerar(setor, dia, out Catalogo c);
            ObjetoDeCena grade = m.Objetos.Single(o => o.Chave == setor + ":grade_arena");
            List<ObjetoDeCena> alavancas = m.Objetos.Where(o => o.Tipo == TipoObjeto.Alavanca).ToList();
            Assert.Equal(c.Setor(setor).Alavancas, alavancas.Count);
            Assert.Equal(alavancas.Select(a => a.Id).OrderBy(i => i), grade.Dependencias.OrderBy(i => i));
            AplicarObjetos(m, o => o.Tipo != TipoObjeto.Alavanca && o.Tipo != TipoObjeto.Saida && o.Tipo != TipoObjeto.Coletavel && (o.Tipo != TipoObjeto.Porta || o.Dependencias.Count > 0) && (o.Tipo != TipoObjeto.MuroMovel || true));
            foreach (ObjetoDeCena a in alavancas)
            {
                List<Vector2> cam = Caminhos.Buscar(m.Grade, m.Entradas["patio"].Pos, a.Pos, 0.42f, 200000);
                Assert.True(cam != null && Vector2.Distance(cam[cam.Count - 1], a.Pos) <= a.RaioInteracao, $"alavanca em {a.Pos} fora de alcance");
            }
        }

        [Fact]
        public void MesmoDiaMesmaPlantaDiaSeguinteOutra()
        {
            Catalogo c = Apoio.Catalogo();
            SetorDef d = c.Setor("setor1");
            Mapa a = GeradorDeSetor.Gerar(d, 99, 1), b = GeradorDeSetor.Gerar(d, 99, 1), outro = GeradorDeSetor.Gerar(d, 100, 2);
            Assert.Equal(a.Muros.Count, b.Muros.Count);
            Assert.True(a.Muros.Zip(b.Muros, (x, y) => x.Area.Centro == y.Area.Centro).All(v => v));
            bool igual = a.Planta.ParedeLeste.Cast<bool>().SequenceEqual(outro.Planta.ParedeLeste.Cast<bool>())
                         && a.Planta.ParedeNorte.Cast<bool>().SequenceEqual(outro.Planta.ParedeNorte.Cast<bool>());
            Assert.False(igual, "dia novo deveria ter planta nova");
        }

        [Fact]
        public void PatioTemTodasAsEntradasLivresEOsNpcsAlcancaveis()
        {
            Mapa m = ConstrutorDoPatio.Construir();
            foreach (ObjetoDeCena o in m.Objetos.Where(o => o.TemBloqueio && o.BloqueiaLigado == o.Ligado)) m.Grade.Bloquear(o.Bloqueio);
            Vector2 inicio = m.Entradas["inicio"].Pos;
            foreach (KeyValuePair<string, Entrada> e in m.Entradas)
            {
                Assert.True(m.Grade.FolgaEm(e.Value.Pos) >= 0.42f, $"entrada {e.Key} encostada em parede");
                Assert.NotNull(Caminhos.Buscar(m.Grade, inicio, e.Value.Pos, 0.42f, 200000));
            }
            foreach (PosicaoDeNpc n in m.Npcs)
            {
                List<Vector2> c = Caminhos.Buscar(m.Grade, inicio, n.Pos, 0.42f, 200000);
                Assert.True(c != null && Vector2.Distance(c[c.Count - 1], n.Pos) < 1.5f, $"npc {n.Npc} inalcancavel em {n.Pos}");
            }
            foreach (ObjetoDeCena o in m.Objetos.Where(o => o.Interativo && o.Tipo != TipoObjeto.Porta))
            {
                List<Vector2> c = Caminhos.Buscar(m.Grade, inicio, o.Pos, 0.42f, 200000);
                Assert.True(c != null && Vector2.Distance(c[c.Count - 1], o.Pos) <= o.RaioInteracao, $"{o.Nome} fora de alcance");
            }
        }
    }
}
