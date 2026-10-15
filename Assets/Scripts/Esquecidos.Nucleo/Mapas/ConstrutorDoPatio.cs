using System;
using System.Collections.Generic;
using System.Numerics;
using Esquecidos.Nucleo.Simulacao;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Nucleo.Mapas
{
    /// <summary>
    /// O Patio: o jardim murado no meio do labirinto, onde os Esquecidos
    /// vivem. Diferente dos setores, a planta e fixa e foi desenhada a mao
    /// (aqui, em coordenadas): quatro portoes, as cabanas, a arvore velha, a
    /// plantacao, o monta-cargas no centro.
    ///
    /// Eixos: x cresce para leste, z cresce para norte. O muro vai de 8 a 88
    /// nos dois eixos; os portoes ficam no meio de cada lado.
    /// </summary>
    public static class ConstrutorDoPatio
    {
        public const float Tamanho = 96f;
        public const float Borda0 = 8f, Borda1 = 88f;
        public const float Centro = 48f;
        public const float VaoDoPortao = 7f;
        public const float AlturaDoMuro = 24f;
        private const float E = 1.6f;

        public static readonly string[] Portoes = { "leste", "norte", "oeste", "sul" };

        public static Mapa Construir()
        {
            var m = new Mapa("patio", Tamanho, Tamanho)
            {
                Nome = T("O Pátio"),
                Tema = Tema.Patio,
                Seguro = true,
                Musica = "patio",
                AlturaDosMuros = AlturaDoMuro,
                ChaoPadrao = TipoDeChao.Grama,
                Nivel = 1,
            };
            var objetos = new List<ObjetoDeCena>();
            int id = GeradorDeSetor.IdsDeObjeto;
            ObjetoDeCena Obj(ObjetoDeCena o)
            {
                o.Id = id++;
                objetos.Add(o);
                return o;
            }

            Muralha(m);
            foreach (string p in Portoes) Portao(m, p, Obj);

            // Chao: caminhos de terra em cruz, praca de pedra no centro, plantacao.
            m.Zona(new Retangulo(Borda0, Centro - 2.6f, Borda1, Centro + 2.6f), TipoDeChao.Terra);
            m.Zona(new Retangulo(Centro - 2.6f, Borda0, Centro + 2.6f, Borda1), TipoDeChao.Terra);
            m.Zona(Retangulo.Centrado(Centro + 2, Centro, 19, 19), TipoDeChao.Pedra, circular: true);
            m.Zona(new Retangulo(12, 30, 31, 43.5f), TipoDeChao.Plantacao);
            m.Zona(Retangulo.Centrado(28, 64, 6, 4), TipoDeChao.Terra);
            m.Zona(Retangulo.Centrado(68, 64, 8, 5), TipoDeChao.Terra);

            // Monta-cargas: a jaula de metal por onde todo mundo chegou.
            var montaCargas = new Vector2(55, 44);
            m.Enfeite("monta_cargas", montaCargas, MathF.PI / 2);
            foreach (Vector2 canto in new[] { new Vector2(-2.6f, -2.6f), new Vector2(2.6f, -2.6f), new Vector2(-2.6f, 2.6f), new Vector2(2.6f, 2.6f) })
                m.Grade.BloquearCirculo(montaCargas + canto, 0.35f);
            m.Grade.Bloquear(Retangulo.Centrado(montaCargas.X, montaCargas.Y - 2.6f, 5.4f, 0.4f));
            Obj(new ObjetoDeCena
            {
                Tipo = TipoObjeto.Elevador,
                Nome = T("Monta-Cargas"),
                Pos = montaCargas + new Vector2(0, 2.8f),
                RaioInteracao = 2.4f,
                Modelo = "monta_cargas",
                Roteiro = "monta_cargas",
            });

            // Cabanas. A direcao e para onde a porta abre.
            Cabana(m, "enfermaria", new Vector2(28, 69), 11, 8, -MathF.PI / 2);
            Cabana(m, "oficina", new Vector2(68, 69), 12, 8, -MathF.PI / 2);
            Cabana(m, "sala_dos_mapas", new Vector2(68, 27), 10, 8, MathF.PI / 2);
            Cabana(m, "redario", new Vector2(27, 20), 14, 7, MathF.PI / 2);

            // Cozinha: so telhado sobre postes, com mesas.
            m.Enfeite("pavilhao", new Vector2(48, 70), 0);
            foreach (Vector2 poste in new[] { new Vector2(43.5f, 67), new Vector2(52.5f, 67), new Vector2(43.5f, 73), new Vector2(52.5f, 73) })
                m.Grade.BloquearCirculo(poste, 0.3f);
            m.Enfeite("mesa_comprida", new Vector2(46, 70), 0, 1, 4.2f, 1.1f);
            m.Enfeite("mesa_comprida", new Vector2(50.5f, 70.5f), 0, 1, 4.2f, 1.1f);
            m.Enfeite("fogao", new Vector2(52, 72.5f), 0, 1, 1.6f, 1f);

            // A arvore velha, a fogueira e o poco.
            m.Enfeite("arvore_velha", new Vector2(37, 53), 0.4f, 1f, colisaoRaio: 1.7f, semente: 7);
            m.Enfeite("fogueira", new Vector2(47, 57), 0, 1, colisaoRaio: 0.9f);
            m.Enfeite("banco", new Vector2(47, 59.6f), 0, 1, 2.6f, 0.6f);
            m.Enfeite("banco", new Vector2(47, 54.4f), MathF.PI, 1, 2.6f, 0.6f);
            m.Enfeite("banco", new Vector2(44.4f, 57), MathF.PI / 2, 1, 2.6f, 0.6f);
            m.Enfeite("poco", new Vector2(61, 55), 0, 1, colisaoRaio: 1.2f);

            // Plantacao entre o redario e o caminho do portao oeste, com cerca
            // nos lados que nao dao para o caminho do centro.
            for (int linha = 0; linha < 6; linha++)
                m.Enfeite("canteiro", new Vector2(21f, 31.6f + linha * 2.1f), 0, 1, variante: linha % 3 == 0 ? "milho" : linha % 3 == 1 ? "couve" : "abobora", semente: linha);
            Cerca(m, new Vector2(11.5f, 29.2f), new Vector2(31.5f, 29.2f));
            Cerca(m, new Vector2(11.5f, 44.2f), new Vector2(31.5f, 44.2f));
            Cerca(m, new Vector2(11.5f, 29.2f), new Vector2(11.5f, 44.2f));
            m.Enfeite("espantalho", new Vector2(27.5f, 37), 0.3f, 1, colisaoRaio: 0.3f);

            // Cemiterio no canto nordeste.
            for (int i = 0; i < 9; i++)
                m.Enfeite("cruz", new Vector2(76 + (i % 3) * 2.4f, 77 + (i / 3) * 2.6f), (i * 0.13f) - 0.3f, 1, colisaoRaio: 0.2f, semente: i);
            Cerca(m, new Vector2(73.5f, 75), new Vector2(84, 75));
            Cerca(m, new Vector2(73.5f, 75), new Vector2(73.5f, 85));

            // Matagal nos cantos noroeste e sudeste: o pedaco de mata que sobrou.
            var rng = new Random(1977);
            Matagal(m, new Retangulo(11, 74, 26, 86), 14, rng);
            Matagal(m, new Retangulo(73, 11, 86, 21), 12, rng);
            for (int i = 0; i < 6; i++)
                m.Enfeite("arvore", new Vector2(58 + i * 4.6f, 84.5f + (i % 2) * 1.2f), (float)rng.NextDouble() * 6, 0.9f + (float)rng.NextDouble() * 0.3f, colisaoRaio: 0.45f, semente: rng.Next());

            // Postes de luz pelos caminhos.
            foreach (Vector2 p in new[]
                     {
                         new Vector2(44.6f, 30), new Vector2(51.4f, 64), new Vector2(34, 44.6f), new Vector2(63, 51.4f),
                         new Vector2(78, 44.6f), new Vector2(18, 51.4f), new Vector2(44.6f, 79), new Vector2(51.4f, 17),
                     })
                m.Enfeite("poste", p, 0, 1, colisaoRaio: 0.22f);

            // Oficina: caixotes, barris, bonecos de treino.
            m.Enfeite("caixote", new Vector2(61.5f, 64), 0.1f, 1, 1.2f, 1.2f);
            m.Enfeite("caixote", new Vector2(62.2f, 65.4f), 0.5f, 0.8f, 1f, 1f);
            m.Enfeite("barril", new Vector2(75.5f, 64.5f), 0, 1, colisaoRaio: 0.45f);
            m.Enfeite("barril", new Vector2(76.4f, 63.4f), 0, 1, colisaoRaio: 0.45f);
            m.Enfeite("pilha_sucata", new Vector2(78.5f, 70), 0, 1, colisaoRaio: 1.2f);
            m.Enfeite("bancada", new Vector2(68, 63.6f), 0, 1, 3.2f, 1.1f);
            m.Enfeite("mesa_remedios", new Vector2(28, 63.6f), 0, 1, 2.4f, 1f);

            // Objetos com que se interage.
            Obj(new ObjetoDeCena { Tipo = TipoObjeto.Estacao, Nome = T("Bancada da Oficina"), Pos = new Vector2(68, 62.4f), RaioInteracao = 2f, Texto = "oficina", Modelo = "nenhum" });
            Obj(new ObjetoDeCena { Tipo = TipoObjeto.Estacao, Nome = T("Mesa da Enfermaria"), Pos = new Vector2(28, 62.4f), RaioInteracao = 2f, Texto = "enfermaria", Modelo = "nenhum" });
            Obj(new ObjetoDeCena { Tipo = TipoObjeto.Guarda, Nome = T("Baú do Redário"), Pos = new Vector2(33, 25.4f), RaioInteracao = 2f, Modelo = "bau_grande", TemBloqueio = true, Bloqueio = Retangulo.Centrado(33, 25.4f, 1.6f, 1f), BloqueiaLigado = false });
            Obj(new ObjetoDeCena { Tipo = TipoObjeto.Descanso, Nome = T("Rede"), Pos = new Vector2(23, 25.2f), RaioInteracao = 2.2f, Modelo = "rede" });
            Obj(new ObjetoDeCena { Tipo = TipoObjeto.Painel, Nome = T("Mesa dos Mapas"), Pos = new Vector2(63, 33.5f), RaioInteracao = 2.2f, Modelo = "mesa_mapas", TemBloqueio = true, Bloqueio = Retangulo.Centrado(63, 33.5f, 2.4f, 1.4f), BloqueiaLigado = false });
            Obj(new ObjetoDeCena { Tipo = TipoObjeto.Cartaz, Nome = T("Muro dos Nomes"), Pos = new Vector2(10.2f, 64), Direcao = 0, RaioInteracao = 2.6f, Modelo = "muro_dos_nomes", Roteiro = "muro_dos_nomes" });

            m.Surgimentos.Add(new Surgimento { Inimigo = "boneco", Nivel = 1, Pos = new Vector2(78, 60) });
            m.Surgimentos.Add(new Surgimento { Inimigo = "boneco", Nivel = 1, Pos = new Vector2(81, 61.5f) });
            m.Surgimentos.Add(new Surgimento { Inimigo = "boneco", Nivel = 1, Pos = new Vector2(80, 58) });

            Npc(m, "iara", 40.5f, 56, -0.6f);
            Npc(m, "teo", 82, 51.5f, MathF.PI);
            Npc(m, "duda", 31, 62, -MathF.PI / 2);
            Npc(m, "marreta", 64.5f, 62, -MathF.PI / 2);
            Npc(m, "caio", 66, 34, MathF.PI);
            Npc(m, "nico", 33.5f, 38, MathF.PI);
            Npc(m, "bia", 48, 67.2f, -MathF.PI / 2);
            Npc(m, "zeca", 51.5f, 82, -MathF.PI / 2);

            m.Entradas["inicio"] = new Entrada { Pos = montaCargas, Direcao = MathF.PI / 2 };
            m.Entradas["leste"] = new Entrada { Pos = new Vector2(Borda1 - 4, Centro), Direcao = MathF.PI };
            m.Entradas["oeste"] = new Entrada { Pos = new Vector2(Borda0 + 4, Centro), Direcao = 0 };
            m.Entradas["norte"] = new Entrada { Pos = new Vector2(Centro, Borda1 - 4), Direcao = -MathF.PI / 2 };
            m.Entradas["sul"] = new Entrada { Pos = new Vector2(Centro, Borda0 + 4), Direcao = MathF.PI / 2 };
            m.Entradas["redario"] = new Entrada { Pos = new Vector2(27, 26.5f), Direcao = MathF.PI / 2 };
            m.Entradas["enfermaria"] = new Entrada { Pos = new Vector2(28, 60.5f), Direcao = -MathF.PI / 2 };

            m.Objetos.AddRange(objetos);
            return m;
        }

        private static void Muralha(Mapa m)
        {
            float a = Centro - VaoDoPortao / 2, b = Centro + VaoDoPortao / 2;
            float h = AlturaDoMuro;
            // Sul e norte.
            m.Muro(new Retangulo(Borda0 - E, Borda0 - E, a, Borda0 + E), h, "patio");
            m.Muro(new Retangulo(b, Borda0 - E, Borda1 + E, Borda0 + E), h, "patio");
            m.Muro(new Retangulo(Borda0 - E, Borda1 - E, a, Borda1 + E), h, "patio");
            m.Muro(new Retangulo(b, Borda1 - E, Borda1 + E, Borda1 + E), h, "patio");
            // Oeste e leste.
            m.Muro(new Retangulo(Borda0 - E, Borda0 + E, Borda0 + E, a), h, "patio");
            m.Muro(new Retangulo(Borda0 - E, b, Borda0 + E, Borda1 - E), h, "patio");
            m.Muro(new Retangulo(Borda1 - E, Borda0 + E, Borda1 + E, a), h, "patio");
            m.Muro(new Retangulo(Borda1 - E, b, Borda1 + E, Borda1 - E), h, "patio");
        }

        /// <summary>Vao no muro, corredor curto para fora, portao que fecha a noite e a saida para o setor.</summary>
        private static void Portao(Mapa m, string lado, Func<ObjetoDeCena, ObjetoDeCena> obj)
        {
            float a = Centro - VaoDoPortao / 2, b = Centro + VaoDoPortao / 2;
            float h = AlturaDoMuro;
            Vector2 normal; // para fora
            switch (lado)
            {
                case "leste": normal = new Vector2(1, 0); break;
                case "oeste": normal = new Vector2(-1, 0); break;
                case "norte": normal = new Vector2(0, 1); break;
                default: normal = new Vector2(0, -1); break;
            }
            bool horizontal = normal.Y != 0;
            float bordaMuro = (normal.X + normal.Y) > 0 ? Borda1 : Borda0;
            float fim = (normal.X + normal.Y) > 0 ? Tamanho - 0.5f : 0.5f;
            float lo = MathF.Min(bordaMuro, fim), hi = MathF.Max(bordaMuro, fim);
            if (horizontal)
            {
                m.Muro(new Retangulo(a - 2, lo, a, hi), h * 0.8f, "concreto");
                m.Muro(new Retangulo(b, lo, b + 2, hi), h * 0.8f, "concreto");
            }
            else
            {
                m.Muro(new Retangulo(lo, a - 2, hi, a), h * 0.8f, "concreto");
                m.Muro(new Retangulo(lo, b, hi, b + 2), h * 0.8f, "concreto");
            }
            Vector2 noMuro = horizontal ? new Vector2(Centro, bordaMuro) : new Vector2(bordaMuro, Centro);
            Retangulo vao = horizontal
                ? new Retangulo(a, bordaMuro - 0.7f, b, bordaMuro + 0.7f)
                : new Retangulo(bordaMuro - 0.7f, a, bordaMuro + 0.7f, b);
            obj(new ObjetoDeCena
            {
                Tipo = TipoObjeto.Porta,
                Nome = F("Portão {0}", T(lado)),
                Pos = noMuro,
                Direcao = MathF.Atan2(normal.Y, normal.X),
                TemBloqueio = true,
                Bloqueio = vao,
                BloqueiaLigado = false,
                Ligado = false,
                Interativo = true,
                Modelo = "portao_patio",
                Largura = VaoDoPortao,
                Altura = h,
                SoDeDia = true,
                RequerStatus = "portao_" + lado,
                Mensagem = T("O portão está fechado."),
                Chave = "patio:portao_" + lado,
            });
            m.Muros.Add(new TrechoDeMuro
            {
                Area = horizontal ? new Retangulo(a, bordaMuro - E, b, bordaMuro + E) : new Retangulo(bordaMuro - E, a, bordaMuro + E, b),
                Altura = h,
                Estilo = "verga",
            });
            Vector2 saida = noMuro + normal * 5.5f;
            obj(new ObjetoDeCena
            {
                Tipo = TipoObjeto.Saida,
                Nome = T("Labirinto"),
                Pos = saida,
                RaioInteracao = 2.2f,
                Destino = "portao:" + lado,
                Entrada = "patio",
                Interativo = false,
            });
            m.Zona(horizontal ? new Retangulo(a, lo, b, hi) : new Retangulo(lo, a, hi, b), TipoDeChao.Pedra);
        }

        private static void Cabana(Mapa m, string tipo, Vector2 centro, float largura, float profundidade, float porta)
        {
            // A cabana inteira bloqueia; a "porta" e so desenho, ninguem entra.
            m.Grade.Bloquear(Retangulo.Centrado(centro.X, centro.Y, largura, profundidade));
            m.Pecas.Add(new Peca { Tipo = "cabana", Variante = tipo, Pos = centro, Direcao = porta, Largura = largura, Profundidade = profundidade, Altura = tipo == "sala_dos_mapas" ? 3.6f : 4.2f });
        }

        private static void Cerca(Mapa m, Vector2 de, Vector2 ate)
        {
            float x0 = MathF.Min(de.X, ate.X), x1 = MathF.Max(de.X, ate.X);
            float z0 = MathF.Min(de.Y, ate.Y), z1 = MathF.Max(de.Y, ate.Y);
            if (x1 - x0 < 0.3f) { x0 -= 0.12f; x1 += 0.12f; }
            if (z1 - z0 < 0.3f) { z0 -= 0.12f; z1 += 0.12f; }
            m.Grade.Bloquear(new Retangulo(x0, z0, x1, z1));
            m.Pecas.Add(new Peca { Tipo = "cerca", Pos = (de + ate) / 2, Direcao = MathF.Atan2(ate.Y - de.Y, ate.X - de.X), Largura = Vector2.Distance(de, ate) });
        }

        private static void Matagal(Mapa m, Retangulo area, int arvores, Random rng)
        {
            var postas = new List<Vector2>();
            for (int tentativa = 0; tentativa < arvores * 8 && postas.Count < arvores; tentativa++)
            {
                var p = new Vector2(area.X0 + (float)rng.NextDouble() * area.Largura, area.Z0 + (float)rng.NextDouble() * area.Profundidade);
                bool perto = false;
                foreach (Vector2 q in postas)
                    if (Vector2.Distance(p, q) < 2.6f) perto = true;
                if (perto) continue;
                postas.Add(p);
                m.Enfeite("arvore", p, (float)rng.NextDouble() * 6.28f, 0.8f + (float)rng.NextDouble() * 0.5f, colisaoRaio: 0.45f, semente: rng.Next());
                if (rng.NextDouble() < 0.5)
                    m.Enfeite("arbusto", p + new Vector2((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1) * 1.6f, 0, 0.7f + (float)rng.NextDouble() * 0.6f, semente: rng.Next());
            }
        }

        private static void Npc(Mapa m, string id, float x, float z, float direcao)
        {
            m.Npcs.Add(new PosicaoDeNpc { Npc = id, Pos = new Vector2(x, z), Direcao = direcao });
        }
    }
}
