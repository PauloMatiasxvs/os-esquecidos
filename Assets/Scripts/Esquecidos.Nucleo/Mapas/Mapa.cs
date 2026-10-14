using System.Collections.Generic;
using System.Numerics;
using Esquecidos.Nucleo.Simulacao;

namespace Esquecidos.Nucleo.Mapas
{
    public enum Tema
    {
        Patio,
        Hera,
        Ferrugem,
        Fosso,
        Engrenagens,
        Nucleo,
        Laboratorio,
    }

    /// <summary>Enfeite de cenario: o que a tela desenha. Colisao, se tiver, ja foi para a grade.</summary>
    public sealed class Peca
    {
        public string Tipo;
        public Vector2 Pos;
        public float Direcao;
        public float Escala = 1f;
        public float Largura = 1f;
        public float Profundidade = 1f;
        public float Altura = 1f;
        public int Semente;
        public string Variante;
    }

    /// <summary>Um bloco de muro alinhado aos eixos, com altura e estilo de acabamento.</summary>
    public sealed class TrechoDeMuro
    {
        public Retangulo Area;
        public float Altura;
        public string Estilo = "concreto";
        /// <summary>Se o muro e de um objeto (muro movel, portao), o id dele; a tela anima pelo estado.</summary>
        public int Objeto;
    }

    public enum TipoDeChao
    {
        Grama,
        Terra,
        Pedra,
        Plantacao,
        Concreto,
        Metal,
        Ladrilho,
        Fosso,
    }

    public sealed class ZonaDeChao
    {
        public Retangulo Area;
        public TipoDeChao Tipo;
        public bool Circular;
    }

    public sealed class Surgimento
    {
        public string Inimigo;
        public int Nivel;
        public Vector2 Pos;
        public bool Elite;
        public bool Noturno;
        public bool Chefe;
        public int Grupo;
    }

    public sealed class PosicaoDeNpc
    {
        public string Npc;
        public Vector2 Pos;
        public float Direcao;
    }

    public sealed class Entrada
    {
        public Vector2 Pos;
        public float Direcao;
    }

    /// <summary>
    /// Como as celulas do labirinto se ligam: serve para o minimapa desenhar
    /// os corredores e para o "mapeamento" que o jogador entrega ao cartografo.
    /// </summary>
    public sealed class PlantaDoLabirinto
    {
        public int Colunas, Linhas;
        public float TamanhoCelula;
        public Vector2 Origem;
        /// <summary>Parede a leste da celula (x,z): [x,z]. A ultima coluna e a borda.</summary>
        public bool[,] ParedeLeste;
        /// <summary>Parede ao norte da celula (x,z).</summary>
        public bool[,] ParedeNorte;
        public (int x, int z) Entrada;
        public (int x, int z) Arena;
        public List<(int x, int z, int w, int h)> Salas = new List<(int, int, int, int)>();

        public Vector2 CentroDaCelula(int x, int z) => Origem + new Vector2((x + 0.5f) * TamanhoCelula, (z + 0.5f) * TamanhoCelula);

        public (int x, int z) CelulaEm(Vector2 p)
        {
            Vector2 d = (p - Origem) / TamanhoCelula;
            return ((int)System.MathF.Floor(d.X), (int)System.MathF.Floor(d.Y));
        }

        public bool Dentro(int x, int z) => x >= 0 && z >= 0 && x < Colunas && z < Linhas;
    }

    /// <summary>
    /// Um mapa pronto para jogar: a grade de colisao, os muros e enfeites para
    /// desenhar, os objetos com estado, onde nascem bichos e gente, e por onde
    /// se entra. E a "cena" do Pal3, so que gerada em codigo.
    /// </summary>
    public sealed class Mapa
    {
        public string Id;
        public string Nome;
        public Tema Tema;
        public int Nivel = 1;
        public bool Seguro;
        public float Largura;
        public float Profundidade;
        public Grade Grade;
        public string Musica = "patio";
        public int Semente;
        public float AlturaDosMuros = 14f;
        public List<TrechoDeMuro> Muros = new List<TrechoDeMuro>();
        public List<Peca> Pecas = new List<Peca>();
        public List<ZonaDeChao> Chao = new List<ZonaDeChao>();
        public List<ObjetoDeCena> Objetos = new List<ObjetoDeCena>();
        public List<Surgimento> Surgimentos = new List<Surgimento>();
        public List<PosicaoDeNpc> Npcs = new List<PosicaoDeNpc>();
        public Dictionary<string, Entrada> Entradas = new Dictionary<string, Entrada>();
        public PlantaDoLabirinto Planta;
        public TipoDeChao ChaoPadrao = TipoDeChao.Grama;

        public Mapa(string id, float largura, float profundidade)
        {
            Id = id;
            Largura = largura;
            Profundidade = profundidade;
            Grade = new Grade(largura, profundidade);
        }

        /// <summary>Muro solido: entra na grade e na lista de desenho de uma vez.</summary>
        public TrechoDeMuro Muro(Retangulo r, float altura, string estilo = "concreto")
        {
            Grade.Bloquear(r);
            var m = new TrechoDeMuro { Area = r, Altura = altura, Estilo = estilo };
            Muros.Add(m);
            return m;
        }

        /// <summary>Enfeite com colisao em caixa (cabana, caixote) ou em circulo (arvore, barril).</summary>
        public Peca Enfeite(string tipo, Vector2 pos, float direcao = 0, float escala = 1, float colisaoLargura = 0, float colisaoProfundidade = 0, float colisaoRaio = 0, string variante = null, int semente = 0)
        {
            if (colisaoLargura > 0 && colisaoProfundidade > 0)
            {
                bool deitado = System.MathF.Abs(System.MathF.Sin(direcao)) > 0.7f;
                float w = deitado ? colisaoProfundidade : colisaoLargura;
                float d = deitado ? colisaoLargura : colisaoProfundidade;
                Grade.Bloquear(Retangulo.Centrado(pos.X, pos.Y, w, d));
            }
            if (colisaoRaio > 0) Grade.BloquearCirculo(pos, colisaoRaio);
            var p = new Peca
            {
                Tipo = tipo,
                Pos = pos,
                Direcao = direcao,
                Escala = escala,
                Largura = colisaoLargura,
                Profundidade = colisaoProfundidade,
                Variante = variante,
                Semente = semente,
            };
            Pecas.Add(p);
            return p;
        }

        public void Zona(Retangulo r, TipoDeChao tipo, bool circular = false)
        {
            Chao.Add(new ZonaDeChao { Area = r, Tipo = tipo, Circular = circular });
        }
    }
}
