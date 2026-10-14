using System.Collections.Generic;
using System.Numerics;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Simulacao
{
    public sealed class Projetil
    {
        public int Id;
        public Vector2 Pos;
        public Vector2 PosAnterior;
        public Vector2 Velocidade;
        public float AlcanceRestante;
        public float Raio = 0.25f;
        public int Dono;
        public Faccao Faccao;
        public Poder Poder;
        public HashSet<int> Atingidos = new();
        public bool Vivo = true;
        public float Altura = 1.1f;
    }

    /// <summary>
    /// Area no chao: explosao com pavio (atraso), poca que queima (duracao),
    /// ou armadilha que espera alguem pisar.
    /// </summary>
    public sealed class AreaDeEfeito
    {
        public int Id;
        public Vector2 Pos;
        public float Raio;
        public float Atraso;
        public float Duracao;
        public float Tique;
        public bool Armadilha;
        public bool Explodiu;
        public int Dono;
        public Faccao Faccao;
        public Poder Poder;
        public bool Viva = true;
    }

    public sealed class ItemNoChao
    {
        public int Id;
        public Vector2 Pos;
        public Item Item;
        public int Sucata;
        public float Idade;
    }

    public enum TipoObjeto
    {
        Porta,
        Alavanca,
        Placa,
        MuroMovel,
        Bau,
        Coletavel,
        Saida,
        Gatilho,
        Descanso,
        Terminal,
        Marco,
        Painel,
        Estacao,
        Guarda,
        Elevador,
        Cartaz,
    }

    /// <summary>
    /// Tudo no cenario que tem estado ou reage ao jogador, como os
    /// SceneObjects do Pal3: porta, alavanca, placa de pressao, muro que
    /// sobe e desce, bau, saida de mapa. "Ligado" e o estado de cada um
    /// (porta aberta, alavanca puxada, bau aberto, muro erguido).
    /// </summary>
    public sealed class ObjetoDeCena
    {
        public int Id;
        public TipoObjeto Tipo;
        public string Nome = "";
        public Vector2 Pos;
        public float Direcao;
        public float RaioInteracao = 1.8f;
        public bool Interativo = true;
        public bool Ligado;
        public bool Usado;
        public bool TemBloqueio;
        public Retangulo Bloqueio;
        public bool BloqueiaLigado;
        public bool BloqueioAplicado;
        public List<int> Alvos = new();
        /// <summary>Porta que so abre com todas estas alavancas puxadas.</summary>
        public List<int> Dependencias = new();
        public string Destino;
        public string Entrada;
        public string Roteiro;
        public string RequerStatus;
        public string RequerItem;
        public string Mensagem;
        public string Chave;
        public string Modelo;
        public string Texto;
        public string Loot;
        public string Item;
        public int Nivel = 1;
        public float Largura = 1;
        public float Profundidade = 1;
        public float Altura = 1;
        public bool SoDeDia;
        public bool UmaVez;
        public float Ciclo;
        public bool PisandoAgora;

        public bool Bloqueando => TemBloqueio && (Ligado == BloqueiaLigado);
    }
}
