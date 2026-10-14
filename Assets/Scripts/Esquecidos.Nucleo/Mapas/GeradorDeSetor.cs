using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Simulacao;

namespace Esquecidos.Nucleo.Mapas
{
    /// <summary>
    /// Gera um setor do labirinto a partir da receita e da semente do dia. O
    /// mesmo setor no mesmo dia sai sempre igual; no dia seguinte e outro
    /// labirinto, porque os muros andaram durante a noite.
    ///
    /// Passos: labirinto perfeito por busca em profundidade, ciclos abertos
    /// nos becos (corredor sem saida demais cansa e mata), arena do chefe com
    /// uma entrada so, salas, portao trancado por alavancas e muros moveis
    /// escolhidos de um jeito que nenhuma combinacao deles isola uma celula.
    /// </summary>
    public static class GeradorDeSetor
    {
        public const float Espessura = 1.4f;
        public const float MargemLateral = 4f;
        public const float Bocal = 12f;
        public const int IdsDeObjeto = 1000000;

        public sealed class Grafo
        {
            public int C, L;
            public bool[,] Leste;
            public bool[,] Norte;

            public Grafo(int c, int l)
            {
                C = c;
                L = l;
                Leste = new bool[c, l];
                Norte = new bool[c, l];
                for (int x = 0; x < c; x++)
                for (int z = 0; z < l; z++)
                {
                    Leste[x, z] = true;
                    Norte[x, z] = true;
                }
            }

            public bool Dentro(int x, int z) => x >= 0 && z >= 0 && x < C && z < L;

            /// <summary>Tem parede entre (x,z) e o vizinho na direcao d (0=L,1=N,2=O,3=S)?</summary>
            public bool Parede(int x, int z, int d)
            {
                switch (d)
                {
                    case 0: return Leste[x, z];
                    case 1: return Norte[x, z];
                    case 2: return x == 0 || Leste[x - 1, z];
                    default: return z == 0 || Norte[x, z - 1];
                }
            }

            public void Definir(int x, int z, int d, bool parede)
            {
                switch (d)
                {
                    case 0: if (x < C) Leste[x, z] = parede; break;
                    case 1: if (z < L) Norte[x, z] = parede; break;
                    case 2: if (x > 0) Leste[x - 1, z] = parede; break;
                    default: if (z > 0) Norte[x, z - 1] = parede; break;
                }
            }

            public static readonly int[] Dx = { 1, 0, -1, 0 };
            public static readonly int[] Dz = { 0, 1, 0, -1 };

            public int Grau(int x, int z)
            {
                int g = 0;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + Dx[d], nz = z + Dz[d];
                    if (Dentro(nx, nz) && !Parede(x, z, d)) g++;
                }
                return g;
            }

            /// <summary>Distancia em celulas a partir de uma origem; -1 onde nao chega.</summary>
            public int[,] Distancias(int sx, int sz, Func<int, int, bool> bloqueada = null, HashSet<(int, int, int)> fechadasExtra = null)
            {
                var dist = new int[C, L];
                for (int x = 0; x < C; x++)
                for (int z = 0; z < L; z++)
                    dist[x, z] = -1;
                var fila = new Queue<(int, int)>();
                dist[sx, sz] = 0;
                fila.Enqueue((sx, sz));
                while (fila.Count > 0)
                {
                    (int x, int z) = fila.Dequeue();
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Dx[d], nz = z + Dz[d];
                        if (!Dentro(nx, nz) || Parede(x, z, d) || dist[nx, nz] >= 0) continue;
                        if (bloqueada != null && bloqueada(nx, nz)) continue;
                        if (fechadasExtra != null && fechadasExtra.Contains(Aresta(x, z, d))) continue;
                        dist[nx, nz] = dist[x, z] + 1;
                        fila.Enqueue((nx, nz));
                    }
                }
                return dist;
            }

            /// <summary>Forma canonica de uma aresta: sempre a partir da celula a oeste/sul.</summary>
            public static (int, int, int) Aresta(int x, int z, int d)
            {
                switch (d)
                {
                    case 2: return (x - 1, z, 0);
                    case 3: return (x, z - 1, 1);
                    default: return (x, z, d);
                }
            }
        }

        public static Mapa Gerar(SetorDef def, int semente, int dia)
        {
            var rng = new Random(semente);
            int C = def.Colunas, L = def.Linhas;
            float T = def.TamanhoCelula;
            float largura = MargemLateral * 2 + Espessura + C * T;
            float profundidade = Bocal * 2 + Espessura + L * T;
            Tema tema = LerTema(def.Tema);
            var mapa = new Mapa(def.Id, largura, profundidade)
            {
                Nome = def.Nome,
                Tema = tema,
                Nivel = def.Nivel,
                Musica = def.Musica,
                Semente = semente,
                AlturaDosMuros = AlturaDoTema(tema),
                ChaoPadrao = ChaoDoTema(tema),
            };
            var origem = new Vector2(MargemLateral + Espessura / 2, Bocal + Espessura / 2);

            var g = new Grafo(C, L);
            var entrada = (x: C / 2, z: 0);
            CavarPerfeito(g, entrada.x, entrada.z, rng);
            Entrelacar(g, rng, 0.55, 0.06);

            // Arena: 3x3 encostada na borda norte, longe da entrada.
            int ax = Math.Max(0, Math.Min(C - 3, rng.Next(1, Math.Max(2, C - 3))));
            int az = L - 3;
            bool NaArena(int x, int z) => x >= ax && x < ax + 3 && z >= az && z < az + 3;
            for (int x = ax; x < ax + 3; x++)
            for (int z = az; z < az + 3; z++)
            {
                if (x < ax + 2) g.Leste[x, z] = false;
                if (z < az + 2) g.Norte[x, z] = false;
            }
            for (int x = ax; x < ax + 3; x++)
            for (int z = az; z < az + 3; z++)
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                if (g.Dentro(nx, nz) && !NaArena(nx, nz)) g.Definir(x, z, d, true);
            }
            Reconectar(g, entrada, NaArena, rng);

            // Porta da arena: a aresta de fronteira cujo vizinho esta mais perto da entrada.
            int[,] distFora = g.Distancias(entrada.x, entrada.z, NaArena);
            (int x, int z, int d) porta = (-1, -1, -1);
            int melhor = int.MaxValue;
            for (int x = ax; x < ax + 3; x++)
            for (int z = az; z < az + 3; z++)
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                if (!g.Dentro(nx, nz) || NaArena(nx, nz) || distFora[nx, nz] < 0) continue;
                // Prefere a porta no meio de um lado: a arena fica simetrica.
                int peso = distFora[nx, nz] * 4 + (x == ax + 1 || z == az + 1 ? 0 : 3);
                if (peso < melhor)
                {
                    melhor = peso;
                    porta = (x, z, d);
                }
            }
            g.Definir(porta.x, porta.z, porta.d, false);

            // Salas 2x2 fora da arena e longe da entrada.
            var salas = new List<(int, int, int, int)>();
            for (int tentativa = 0; tentativa < 60 && salas.Count < def.Salas; tentativa++)
            {
                int sx = rng.Next(0, C - 1), sz = rng.Next(2, Math.Max(3, L - 4));
                bool ruim = false;
                for (int x = sx - 1; x <= sx + 2 && !ruim; x++)
                for (int z = sz - 1; z <= sz + 2 && !ruim; z++)
                    if (NaArena(x, z) || salas.Any(s => x >= s.Item1 && x < s.Item1 + s.Item3 && z >= s.Item2 && z < s.Item2 + s.Item4))
                        ruim = true;
                if (ruim || sz + 1 >= L) continue;
                g.Leste[sx, sz] = g.Leste[sx, sz + 1] = false;
                g.Norte[sx, sz] = g.Norte[sx + 1, sz] = false;
                salas.Add((sx, sz, 2, 2));
            }

            int[,] dist = g.Distancias(entrada.x, entrada.z);
            var celulas = new List<(int x, int z)>();
            for (int x = 0; x < C; x++)
            for (int z = 0; z < L; z++)
                if (!NaArena(x, z)) celulas.Add((x, z));

            // Muros moveis. Grupo A: aberto de dia, sobe a noite. Grupo B: o contrario.
            // Fechando todos ao mesmo tempo o setor ainda e conexo, entao qualquer
            // combinacao intermediaria tambem e.
            var fechadasNoPior = new HashSet<(int, int, int)>();
            var murosA = new List<(int x, int z, int d)>();
            var murosB = new List<(int x, int z, int d)>();
            bool Protegida(int x, int z) => NaArena(x, z) || (Math.Abs(x - entrada.x) <= 1 && z <= 1) || (x == porta.x && z == porta.z);
            var candidatas = new List<(int x, int z, int d)>();
            for (int x = 0; x < C; x++)
            for (int z = 0; z < L; z++)
            for (int d = 0; d < 2; d++)
            {
                int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                if (!g.Dentro(nx, nz) || Protegida(x, z) || Protegida(nx, nz)) continue;
                if (EmSala(salas, x, z) && EmSala(salas, nx, nz)) continue;
                candidatas.Add((x, z, d));
            }
            Embaralhar(candidatas, rng);
            int metade = def.MurosMoveis / 2;
            foreach ((int x, int z, int d) in candidatas)
            {
                if (murosA.Count >= def.MurosMoveis - metade && murosB.Count >= metade) break;
                bool aberta = !g.Parede(x, z, d);
                if (aberta && murosA.Count < def.MurosMoveis - metade)
                {
                    var aresta = Grafo.Aresta(x, z, d);
                    fechadasNoPior.Add(aresta);
                    int[,] teste = g.Distancias(entrada.x, entrada.z, NaArena, fechadasNoPior);
                    if (celulas.All(c => teste[c.x, c.z] >= 0)) murosA.Add((x, z, d));
                    else fechadasNoPior.Remove(aresta);
                }
                else if (!aberta && murosB.Count < metade && x < C - 1 + (d == 0 ? 0 : 1) && Math.Abs(dist[x, z] - dist[x + Grafo.Dx[d], z + Grafo.Dz[d]]) >= 6)
                {
                    // So vale a pena um atalho que encurta caminho de verdade.
                    murosB.Add((x, z, d));
                }
            }

            mapa.Planta = new PlantaDoLabirinto
            {
                Colunas = C,
                Linhas = L,
                TamanhoCelula = T,
                Origem = origem,
                ParedeLeste = (bool[,])g.Leste.Clone(),
                ParedeNorte = (bool[,])g.Norte.Clone(),
                Entrada = entrada,
                Arena = (ax, az),
                Salas = salas,
            };

            var moveis = new HashSet<(int, int, int)>(murosA.Concat(murosB).Select(m => Grafo.Aresta(m.x, m.z, m.d)));
            var arestaPorta = Grafo.Aresta(porta.x, porta.z, porta.d);
            bool temProximo = def.Proximo != null;
            ConstruirMuros(mapa, g, origem, T, entrada, (temProximo ? ax + 1 : -1, L - 1), moveis, arestaPorta);

            // Os objetos que ocupam arestas: muros moveis e a porta da arena.
            foreach ((int x, int z, int d) in murosA) CriarMuroMovel(mapa, origem, T, x, z, d, false);
            foreach ((int x, int z, int d) in murosB) CriarMuroMovel(mapa, origem, T, x, z, d, true);

            Retangulo retPorta = RetanguloDaAresta(origem, T, arestaPorta.Item1, arestaPorta.Item2, arestaPorta.Item3);
            var portaArena = new ObjetoDeCena
            {
                Id = 0,
                Tipo = TipoObjeto.Porta,
                Nome = "Grade da arena",
                Pos = retPorta.Centro,
                Direcao = arestaPorta.Item3 == 0 ? MathF.PI / 2 : 0,
                TemBloqueio = true,
                Bloqueio = retPorta,
                BloqueiaLigado = false,
                Ligado = def.Alavancas == 0,
                Interativo = def.Alavancas > 0,
                Mensagem = def.Alavancas > 1 ? $"Trancada. {def.Alavancas} alavancas mexem nessa grade." : "Trancada. Alguma alavanca abre isso.",
                Modelo = "grade",
                Altura = mapa.AlturaDosMuros * 0.55f,
                Largura = T - Espessura,
                Chave = $"{def.Id}:grade_arena",
            };
            mapa.Objetos.Add(portaArena);
            mapa.Muros.Add(new TrechoDeMuro { Area = retPorta, Altura = mapa.AlturaDosMuros, Estilo = "verga" });

            // Becos sem saida: onde ficam alavancas, baus e terminais.
            List<(int x, int z)> becos = celulas
                .Where(c => g.Grau(c.x, c.z) == 1 && dist[c.x, c.z] >= 3 && !EmSala(salas, c.x, c.z))
                .OrderByDescending(c => dist[c.x, c.z])
                .ToList();
            Embaralhar(becos, rng, 0.35);
            var ocupadas = new HashSet<(int, int)>();
            (int, int) Pegar(List<(int x, int z)> lista, Func<(int x, int z), bool> filtro = null)
            {
                foreach ((int x, int z) c in lista)
                {
                    if (ocupadas.Contains(c) || (filtro != null && !filtro(c))) continue;
                    ocupadas.Add(c);
                    return c;
                }
                // Sem beco sobrando: qualquer celula longe da entrada.
                foreach ((int x, int z) c in celulas.OrderBy(_ => rng.Next()))
                {
                    if (ocupadas.Contains(c) || dist[c.x, c.z] < 3 || (filtro != null && !filtro(c))) continue;
                    ocupadas.Add(c);
                    return c;
                }
                return celulas[0];
            }

            var alavancas = new List<(int x, int z)>();
            for (int i = 0; i < def.Alavancas; i++)
            {
                (int x, int z) c = Pegar(becos, cel => alavancas.All(a => Math.Abs(a.x - cel.x) + Math.Abs(a.z - cel.z) >= 4));
                alavancas.Add(c);
                Vector2 pos = PosNaParedeDoBeco(g, origem, T, c.x, c.z, out float dir);
                var alavanca = new ObjetoDeCena
                {
                    Tipo = TipoObjeto.Alavanca,
                    Nome = "Alavanca",
                    Pos = pos,
                    Direcao = dir,
                    RaioInteracao = 2.2f,
                    Modelo = "alavanca",
                    Chave = $"{def.Id}:{dia}:alavanca:{i}",
                };
                mapa.Objetos.Add(alavanca);
            }

            for (int i = 0; i < def.Baus; i++)
            {
                (int x, int z) c = Pegar(becos);
                Vector2 pos = PosNaParedeDoBeco(g, origem, T, c.x, c.z, out float dir);
                mapa.Objetos.Add(new ObjetoDeCena
                {
                    Tipo = TipoObjeto.Bau,
                    Nome = "Caixa de suprimentos",
                    Pos = pos,
                    Direcao = dir,
                    RaioInteracao = 2f,
                    Modelo = i == 0 && def.Nivel >= 3 ? "bau_raro" : "bau",
                    Nivel = def.Nivel + (i == 0 ? 1 : 0),
                    Loot = i == 0 ? "bau_raro" : "bau",
                    Chave = $"{def.Id}:{dia}:bau:{i}",
                    TemBloqueio = true,
                    Bloqueio = Retangulo.Centrado(pos.X, pos.Y, 1.2f, 1.2f),
                    BloqueiaLigado = false,
                });
            }

            for (int i = 0; i < def.Terminais && def.Terminal.Count > 0; i++)
            {
                (int x, int z) c = Pegar(becos);
                Vector2 pos = PosNaParedeDoBeco(g, origem, T, c.x, c.z, out float dir);
                mapa.Objetos.Add(new ObjetoDeCena
                {
                    Tipo = TipoObjeto.Terminal,
                    Nome = "Terminal da ARCA",
                    Pos = pos,
                    Direcao = dir,
                    RaioInteracao = 2f,
                    Modelo = "terminal",
                    Texto = def.Terminal[(i + dia) % def.Terminal.Count],
                    TemBloqueio = true,
                    Bloqueio = Retangulo.Centrado(pos.X, pos.Y, 1f, 1f),
                    BloqueiaLigado = false,
                });
            }


            Vector2 centroArena = origem + new Vector2((ax + 1.5f) * T, (az + 1.5f) * T);
            mapa.Objetos.Add(new ObjetoDeCena
            {
                Tipo = TipoObjeto.Marco,
                Nome = "Marco do " + def.Nome.Split('—')[0].Trim(),
                Pos = centroArena + new Vector2(0, T * 0.9f),
                RaioInteracao = 2.6f,
                Modelo = "marco",
                Chave = $"{def.Id}:marco",
                TemBloqueio = true,
                Bloqueio = Retangulo.Centrado(centroArena.X, centroArena.Y + T * 0.9f, 1.6f, 1.6f),
                BloqueiaLigado = false,
                Roteiro = $"marco_{def.Id}",
            });

            // Entrada (sul) e saida para o proximo setor (norte).
            float xEntrada = origem.X + (entrada.x + 0.5f) * T;
            mapa.Entradas["patio"] = new Entrada { Pos = new Vector2(xEntrada, origem.Y + T * 0.35f), Direcao = MathF.PI / 2 };
            mapa.Entradas["norte"] = new Entrada { Pos = new Vector2(origem.X + (ax + 1.5f) * T, origem.Y + (L - 0.4f) * T), Direcao = -MathF.PI / 2 };
            CriarBocal(mapa, origem, T, entrada.x, true, def);
            if (temProximo) CriarBocal(mapa, origem, T, ax + 1, false, def);

            // Bichos. Os grupos ficam longe da entrada e longe uns dos outros.
            var usadas = new List<(int x, int z)>();
            int grupo = 0;
            void Povoar(List<(string, int)> tipos, int quantos, bool noturno)
            {
                if (tipos.Count == 0) return;
                List<(int x, int z)> lugares = celulas.Where(c => dist[c.x, c.z] >= (noturno ? 2 : 3)).OrderBy(_ => rng.Next()).ToList();
                foreach ((int x, int z) c in lugares)
                {
                    if (quantos <= 0) break;
                    if (usadas.Any(u => Math.Abs(u.x - c.x) + Math.Abs(u.z - c.z) < 3)) continue;
                    usadas.Add(c);
                    quantos--;
                    grupo++;
                    int n = 1 + rng.Next(0, 2) + (def.Nivel >= 5 ? rng.Next(0, 2) : 0) + (noturno ? 1 : 0);
                    bool elite = rng.NextDouble() < (noturno ? 0.2 : 0.08);
                    Vector2 centro = origem + new Vector2((c.x + 0.5f) * T, (c.z + 0.5f) * T);
                    for (int k = 0; k < n; k++)
                    {
                        float ang = (float)(rng.NextDouble() * Math.PI * 2);
                        float r = (float)rng.NextDouble() * T * 0.25f;
                        mapa.Surgimentos.Add(new Surgimento
                        {
                            Inimigo = Sortear(tipos, rng),
                            Nivel = def.Nivel + rng.Next(0, 2),
                            Pos = centro + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r,
                            Elite = elite && k == 0,
                            Noturno = noturno,
                            Grupo = grupo,
                        });
                    }
                }
            }
            Povoar(def.Inimigos, def.Grupos, false);
            usadas.Clear();
            Povoar(def.InimigosNoturnos, def.GruposNoturnos, true);
            if (def.Chefe != null)
            {
                mapa.Surgimentos.Add(new Surgimento
                {
                    Inimigo = def.Chefe,
                    Nivel = def.Nivel + 2,
                    Pos = centroArena - new Vector2(0, T * 0.3f),
                    Chefe = true,
                });
            }

            Enfeitar(mapa, g, origem, T, rng, tema, ocupadas, NaArena, entrada);

            // Ids fixos dados aqui, para os objetos poderem apontar uns para os
            // outros antes de existir um mundo. Os da simulacao comecam em 1 e
            // nunca chegam perto.
            int proximo = IdsDeObjeto;
            foreach (ObjetoDeCena o in mapa.Objetos) o.Id = proximo++;
            List<ObjetoDeCena> listaAlavancas = mapa.Objetos.Where(o => o.Tipo == TipoObjeto.Alavanca).ToList();
            foreach (ObjetoDeCena a in listaAlavancas)
            {
                a.Alvos.Add(portaArena.Id);
                portaArena.Dependencias.Add(a.Id);
            }
            return mapa;
        }

        public static Retangulo RetanguloDaAresta(Vector2 origem, float T, int x, int z, int d)
        {
            float e = Espessura / 2;
            if (d == 0)
            {
                float lx = origem.X + (x + 1) * T;
                return new Retangulo(lx - e, origem.Y + z * T + e, lx + e, origem.Y + (z + 1) * T - e);
            }
            float lz = origem.Y + (z + 1) * T;
            return new Retangulo(origem.X + x * T + e, lz - e, origem.X + (x + 1) * T - e, lz + e);
        }

        private static void CriarMuroMovel(Mapa mapa, Vector2 origem, float T, int x, int z, int d, bool deDiaFechado)
        {
            var a = Grafo.Aresta(x, z, d);
            Retangulo r = RetanguloDaAresta(origem, T, a.Item1, a.Item2, a.Item3);
            var o = new ObjetoDeCena
            {
                Tipo = TipoObjeto.MuroMovel,
                Nome = "Muro",
                Pos = r.Centro,
                Direcao = a.Item3 == 0 ? MathF.PI / 2 : 0,
                Interativo = false,
                TemBloqueio = true,
                Bloqueio = r,
                BloqueiaLigado = true,
                Ligado = deDiaFechado,
                // Ciclo guarda o estado diurno; a noite inverte.
                Ciclo = deDiaFechado ? 1 : 0,
                Modelo = "muro_movel",
                Altura = mapa.AlturaDosMuros,
            };
            mapa.Objetos.Add(o);
        }

        private static void ConstruirMuros(Mapa mapa, Grafo g, Vector2 origem, float T, (int x, int z) entrada, (int x, int z) saidaNorte,
            HashSet<(int, int, int)> moveis, (int, int, int) porta)
        {
            int C = g.C, L = g.L;
            float h = mapa.AlturaDosMuros;
            float e = Espessura / 2;
            string estilo = mapa.Tema == Tema.Laboratorio ? "laboratorio" : mapa.Tema == Tema.Ferrugem || mapa.Tema == Tema.Engrenagens ? "metal" : "hera";
            var temParedeNoVertice = new bool[C + 1, L + 1];

            void Marcar(int vx, int vz)
            {
                if (vx >= 0 && vz >= 0 && vx <= C && vz <= L) temParedeNoVertice[vx, vz] = true;
            }

            // Bordas oeste e sul (as outras sao as arestas da ultima coluna/linha).
            for (int z = 0; z < L; z++)
            {
                float lx = origem.X;
                mapa.Muro(new Retangulo(lx - e, origem.Y + z * T + e, lx + e, origem.Y + (z + 1) * T - e), h, estilo);
                Marcar(0, z);
                Marcar(0, z + 1);
            }
            for (int x = 0; x < C; x++)
            {
                if (x == entrada.x) continue;
                float lz = origem.Y;
                mapa.Muro(new Retangulo(origem.X + x * T + e, lz - e, origem.X + (x + 1) * T - e, lz + e), h, estilo);
                Marcar(x, 0);
                Marcar(x + 1, 0);
            }
            for (int x = 0; x < C; x++)
            for (int z = 0; z < L; z++)
            {
                for (int d = 0; d < 2; d++)
                {
                    bool parede = d == 0 ? g.Leste[x, z] : g.Norte[x, z];
                    var aresta = (x, z, d);
                    bool borda = d == 0 ? x == C - 1 : z == L - 1;
                    if (borda && d == 1 && x == saidaNorte.x) parede = false;
                    if (borda && d == 0) parede = true;
                    if (borda && d == 1 && x != saidaNorte.x) parede = true;
                    bool ocupada = moveis.Contains(aresta) || aresta.Equals(porta);
                    if (!parede && !ocupada) continue;
                    if (d == 0) { Marcar(x + 1, z); Marcar(x + 1, z + 1); }
                    else { Marcar(x, z + 1); Marcar(x + 1, z + 1); }
                    if (ocupada) continue;
                    mapa.Muro(RetanguloDaAresta(origem, T, x, z, d), h, estilo);
                }
            }
            // Pilar em todo vertice com parede: fecha as quinas e da ritmo ao muro.
            for (int vx = 0; vx <= C; vx++)
            for (int vz = 0; vz <= L; vz++)
            {
                if (!temParedeNoVertice[vx, vz]) continue;
                float px = origem.X + vx * T, pz = origem.Y + vz * T;
                var m = mapa.Muro(new Retangulo(px - e - 0.15f, pz - e - 0.15f, px + e + 0.15f, pz + e + 0.15f), h + 0.8f, "pilar");
            }
        }

        /// <summary>
        /// O corredor curto que sai do setor: ao sul volta para o Patio por um
        /// portao que fecha a noite; ao norte segue para o proximo setor por um
        /// portao que so abre com o chefe morto.
        /// </summary>
        private static void CriarBocal(Mapa mapa, Vector2 origem, float T, int coluna, bool sul, SetorDef def)
        {
            float e = Espessura / 2;
            float x0 = origem.X + coluna * T, x1 = origem.X + (coluna + 1) * T;
            float borda = sul ? origem.Y : origem.Y + def.Linhas * T;
            float fim = sul ? borda - Bocal + 1 : borda + Bocal - 1;
            float h = mapa.AlturaDosMuros;
            mapa.Muro(new Retangulo(x0 - e, MathF.Min(borda, fim), x0 + e, MathF.Max(borda, fim)), h, "concreto");
            mapa.Muro(new Retangulo(x1 - e, MathF.Min(borda, fim), x1 + e, MathF.Max(borda, fim)), h, "concreto");
            mapa.Muro(new Retangulo(x0 - e, sul ? fim - 1.5f : fim, x1 + e, sul ? fim : fim + 1.5f), h, "concreto");
            float meio = (borda + fim) / 2;
            float xc = (x0 + x1) / 2;
            var portao = new ObjetoDeCena
            {
                Tipo = TipoObjeto.Porta,
                Nome = sul ? "Portão do Pátio" : "Passagem",
                Pos = new Vector2(xc, meio),
                TemBloqueio = true,
                Bloqueio = new Retangulo(x0 + e, meio - 0.6f, x1 - e, meio + 0.6f),
                BloqueiaLigado = false,
                Ligado = true,
                Interativo = false,
                Modelo = sul ? "portao" : "portao_passagem",
                Largura = T - Espessura,
                Altura = h,
                Chave = sul ? $"{def.Id}:portao_patio" : $"{def.Id}:portao_proximo",
                SoDeDia = sul && def.Volta == null,
                RequerStatus = sul ? null : def.StatusDoChefe,
            };
            mapa.Objetos.Add(portao);
            mapa.Muros.Add(new TrechoDeMuro { Area = new Retangulo(x0 - e, meio - 0.8f, x1 + e, meio + 0.8f), Altura = h, Estilo = "verga" });
            mapa.Objetos.Add(new ObjetoDeCena
            {
                Tipo = TipoObjeto.Saida,
                Nome = sul ? "Voltar ao Pátio" : "Seguir em frente",
                Pos = new Vector2(xc, sul ? fim + 2.5f : fim - 2.5f),
                RaioInteracao = 2.4f,
                Destino = sul ? def.Retorno().mapa : def.Proximo,
                Entrada = sul ? def.Retorno().entrada : "patio",
                Interativo = false,
            });
            mapa.Zona(new Retangulo(x0, MathF.Min(borda, fim), x1, MathF.Max(borda, fim)), TipoDeChao.Concreto);
        }

        /// <summary>
        /// Lugar para encostar um objeto (alavanca, bau, terminal) numa parede
        /// de verdade da celula, virado para dentro. Num beco e a parede do
        /// fundo; numa celula de passagem e qualquer parede, nunca uma abertura,
        /// senao o objeto tampa o corredor.
        /// </summary>
        private static Vector2 PosNaParedeDoBeco(Grafo g, Vector2 origem, float T, int x, int z, out float direcao)
        {
            Vector2 centro = origem + new Vector2((x + 0.5f) * T, (z + 0.5f) * T);
            int escolhida = -1;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                bool aberta = g.Dentro(nx, nz) && !g.Parede(x, z, d);
                if (!aberta) continue;
                int oposta = (d + 2) % 4;
                if (g.Parede(x, z, oposta))
                {
                    escolhida = oposta;
                    break;
                }
            }
            if (escolhida < 0)
                for (int d = 0; d < 4 && escolhida < 0; d++)
                    if (g.Parede(x, z, d)) escolhida = d;
            if (escolhida < 0)
            {
                direcao = 0;
                return centro;
            }
            var paraParede = new Vector2(Grafo.Dx[escolhida], Grafo.Dz[escolhida]);
            direcao = MathF.Atan2(-paraParede.Y, -paraParede.X);
            return centro + paraParede * (T * 0.5f - Espessura / 2 - 1.0f);
        }

        private static void CavarPerfeito(Grafo g, int sx, int sz, Random rng)
        {
            var visitada = new bool[g.C, g.L];
            var pilha = new Stack<(int, int)>();
            pilha.Push((sx, sz));
            visitada[sx, sz] = true;
            var dirs = new List<int> { 0, 1, 2, 3 };
            while (pilha.Count > 0)
            {
                (int x, int z) = pilha.Peek();
                Embaralhar(dirs, rng);
                bool andou = false;
                foreach (int d in dirs)
                {
                    int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                    if (!g.Dentro(nx, nz) || visitada[nx, nz]) continue;
                    g.Definir(x, z, d, false);
                    visitada[nx, nz] = true;
                    pilha.Push((nx, nz));
                    andou = true;
                    break;
                }
                if (!andou) pilha.Pop();
            }
        }

        private static void Entrelacar(Grafo g, Random rng, double chanceBeco, double chanceExtra)
        {
            for (int x = 0; x < g.C; x++)
            for (int z = 0; z < g.L; z++)
            {
                if (g.Grau(x, z) == 1 && rng.NextDouble() < chanceBeco)
                {
                    var opcoes = new List<int>();
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                        if (g.Dentro(nx, nz) && g.Parede(x, z, d)) opcoes.Add(d);
                    }
                    if (opcoes.Count > 0) g.Definir(x, z, opcoes[rng.Next(opcoes.Count)], false);
                }
                for (int d = 0; d < 2; d++)
                {
                    int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                    if (g.Dentro(nx, nz) && g.Parede(x, z, d) && rng.NextDouble() < chanceExtra) g.Definir(x, z, d, false);
                }
            }
        }

        /// <summary>Fechar a arena pode isolar celulas de fora; abre paredes ate todas voltarem.</summary>
        private static void Reconectar(Grafo g, (int x, int z) entrada, Func<int, int, bool> naArena, Random rng)
        {
            for (int rodada = 0; rodada < g.C * g.L; rodada++)
            {
                int[,] dist = g.Distancias(entrada.x, entrada.z, (x, z) => naArena(x, z));
                var soltas = new List<(int, int, int)>();
                for (int x = 0; x < g.C; x++)
                for (int z = 0; z < g.L; z++)
                {
                    if (naArena(x, z) || dist[x, z] >= 0) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Grafo.Dx[d], nz = z + Grafo.Dz[d];
                        if (g.Dentro(nx, nz) && !naArena(nx, nz) && dist[nx, nz] >= 0) soltas.Add((x, z, d));
                    }
                }
                if (soltas.Count == 0) return;
                (int sx, int sz, int sd) = soltas[rng.Next(soltas.Count)];
                g.Definir(sx, sz, sd, false);
            }
        }

        private static bool EmSala(List<(int x, int z, int w, int h)> salas, int x, int z)
        {
            return salas.Any(s => x >= s.x && x < s.x + s.w && z >= s.z && z < s.z + s.h);
        }

        private static void Enfeitar(Mapa mapa, Grafo g, Vector2 origem, float T, Random rng, Tema tema,
            HashSet<(int, int)> ocupadas, Func<int, int, bool> naArena, (int x, int z) entrada)
        {
            string[] leves, pesados;
            switch (tema)
            {
                case Tema.Ferrugem:
                    leves = new[] { "sucata", "barril", "corrente", "placa_metal", "cano" };
                    pesados = new[] { "caixote", "pilha_sucata", "tambor" };
                    break;
                case Tema.Fosso:
                    leves = new[] { "osso", "entulho", "cabo", "pedra" };
                    pesados = new[] { "buraco", "pedra_grande", "carcaca" };
                    break;
                case Tema.Engrenagens:
                    leves = new[] { "engrenagem_chao", "cano", "parafusos", "sucata" };
                    pesados = new[] { "pistao", "caixote", "tambor" };
                    break;
                case Tema.Nucleo:
                    leves = new[] { "cabo", "cristal", "painel_quebrado" };
                    pesados = new[] { "coluna_dados", "cristal_grande" };
                    break;
                case Tema.Laboratorio:
                    leves = new[] { "papeis", "cadeira", "monitor" };
                    pesados = new[] { "mesa_lab", "armario", "tanque" };
                    break;
                default:
                    leves = new[] { "folhas", "pedra", "osso", "galhos", "marca_corredor" };
                    pesados = new[] { "pedra_grande", "carcaca", "caixote", "tronco" };
                    break;
            }
            float e = Espessura / 2;
            for (int x = 0; x < g.C; x++)
            for (int z = 0; z < g.L; z++)
            {
                if (naArena(x, z) || (x == entrada.x && z == entrada.z)) continue;
                Vector2 centro = origem + new Vector2((x + 0.5f) * T, (z + 0.5f) * T);
                int nLeves = rng.Next(1, 4);
                for (int i = 0; i < nLeves; i++)
                {
                    Vector2 p = centro + new Vector2((float)(rng.NextDouble() - 0.5), (float)(rng.NextDouble() - 0.5)) * (T - Espessura - 1.2f);
                    mapa.Enfeite(leves[rng.Next(leves.Length)], p, (float)(rng.NextDouble() * Math.PI * 2), 0.8f + (float)rng.NextDouble() * 0.5f, semente: rng.Next());
                }
                if (ocupadas.Contains((x, z)) || rng.NextDouble() > 0.45) continue;
                // Coisa grande so encostada numa parede, para nao fechar o corredor.
                var paredes = new List<int>();
                for (int d = 0; d < 4; d++)
                    if (g.Parede(x, z, d)) paredes.Add(d);
                if (paredes.Count == 0) continue;
                int dp = paredes[rng.Next(paredes.Count)];
                var normal = new Vector2(Grafo.Dx[dp], Grafo.Dz[dp]);
                var lado = new Vector2(-normal.Y, normal.X);
                Vector2 pos = centro + normal * (T / 2 - e - 1.0f) + lado * (float)(rng.NextDouble() - 0.5) * (T - 4f);
                string tipo = pesados[rng.Next(pesados.Length)];
                float raio = tipo == "buraco" ? 1.6f : tipo == "pedra_grande" || tipo == "carcaca" || tipo == "cristal_grande" ? 0.9f : 0;
                float caixa = raio > 0 ? 0 : 1.2f;
                Vector2 posFinal = tipo == "buraco" ? centro + lado * (float)(rng.NextDouble() - 0.5) * 1.5f + normal * 0.8f : pos;
                mapa.Enfeite(tipo, posFinal, MathF.Atan2(normal.Y, normal.X), 1f, caixa, caixa, raio, semente: rng.Next());
                if (mapa.Grade.FolgaEm(centro) < 0.9f)
                {
                    // Fechou demais: desfaz.
                    Peca ultima = mapa.Pecas[mapa.Pecas.Count - 1];
                    if (raio > 0) mapa.Grade.BloquearCirculo(posFinal, raio, -1);
                    else mapa.Grade.Bloquear(Retangulo.Centrado(posFinal.X, posFinal.Y, caixa, caixa), -1);
                    mapa.Pecas.Remove(ultima);
                }
            }
        }

        private static string Sortear(List<(string id, int peso)> lista, Random rng)
        {
            int total = lista.Sum(i => i.peso);
            int s = rng.Next(total);
            foreach ((string id, int peso) in lista)
            {
                if (s < peso) return id;
                s -= peso;
            }
            return lista[0].id;
        }

        private static void Embaralhar<T>(List<T> lista, Random rng, double forca = 1)
        {
            for (int i = lista.Count - 1; i > 0; i--)
            {
                if (rng.NextDouble() > forca) continue;
                int j = rng.Next(i + 1);
                (lista[i], lista[j]) = (lista[j], lista[i]);
            }
        }

        public static Tema LerTema(string t)
        {
            switch (t)
            {
                case "patio": return Tema.Patio;
                case "hera": return Tema.Hera;
                case "ferrugem": return Tema.Ferrugem;
                case "fosso": return Tema.Fosso;
                case "engrenagens": return Tema.Engrenagens;
                case "nucleo": return Tema.Nucleo;
                case "laboratorio": return Tema.Laboratorio;
            }
            throw new FormatException($"tema desconhecido '{t}'");
        }

        private static float AlturaDoTema(Tema t)
        {
            switch (t)
            {
                case Tema.Laboratorio: return 5f;
                case Tema.Nucleo: return 16f;
                case Tema.Engrenagens: return 18f;
                default: return 15f;
            }
        }

        private static TipoDeChao ChaoDoTema(Tema t)
        {
            switch (t)
            {
                case Tema.Ferrugem: return TipoDeChao.Metal;
                case Tema.Engrenagens: return TipoDeChao.Metal;
                case Tema.Nucleo: return TipoDeChao.Concreto;
                case Tema.Laboratorio: return TipoDeChao.Ladrilho;
                case Tema.Fosso: return TipoDeChao.Terra;
                default: return TipoDeChao.Pedra;
            }
        }
    }
}
