using System;
using System.Collections.Generic;
using System.Numerics;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Regras;
using E = Esquecidos.Nucleo.Regras.Estat;

namespace Esquecidos.Nucleo.Simulacao
{
    public enum Faccao
    {
        Jogador,
        Inimigo,
        Neutro,
    }

    public enum EstadoAcao
    {
        Livre,
        Conjurando,
        Recuperando,
        Esquivando,
        Investindo,
        Saltando,
        Atordoado,
        Morto,
    }

    public enum TipoOrdem
    {
        Nenhuma,
        Mover,
        Atacar,
        UsarPoder,
        Interagir,
    }

    /// <summary>
    /// O que a entidade quer fazer agora. Jogador e IA falam com a simulacao
    /// do mesmo jeito: dando uma ordem. Andar ate o alcance, mirar e atacar e
    /// trabalho da simulacao, nao de quem mandou.
    /// </summary>
    public sealed class Ordem
    {
        public TipoOrdem Tipo;
        public Vector2 Ponto;
        public int AlvoId;
        public Poder Poder;
        public bool Repetir;
        public int ObjetoId;
    }

    public sealed class EfeitoAtivo
    {
        public string Nome;
        public float Restante;
        public float Duracao;
        public float Valor;
        public int Fonte;
        public float Tique;
    }

    public sealed class Entidade
    {
        public int Id;
        public string Tipo;
        public string Nome;
        public Faccao Faccao;
        public InimigoDef Def;
        public string NpcId;
        public int Nivel = 1;

        public Vector2 Pos;
        public Vector2 PosAnterior;
        public Vector2 Empurrao;
        public Vector2 Origem;
        public float Raio = 0.4f;
        public float Direcao;
        public float VelocidadeBase = 5f;
        public float VelocidadeAtual;
        public bool Voa;

        public float Vida = 1;
        public float Energia;
        public Ficha Estat = new();
        public ItemBase Arma;

        public bool Chefe;
        public bool Elite;
        public bool Invocado;
        public bool Atacavel = true;
        public float TempoDeVida = -1;
        public int Dono;

        public EstadoAcao Acao;
        public float TempoAcao;
        public float DuracaoAcao;
        public Poder PoderAtual;
        public Vector2 PontoAcao;
        public Vector2 InicioAcao;
        public int AlvoAcao;
        public bool ImpactoFeito;
        public HashSet<int> AtingidosNaAcao = new();
        public float Invulneravel;
        public float Cambaleio;
        public float TempoMorto;

        public Ordem Ordem = new();
        public Vector2 MovimentoDireto;
        public List<Vector2> Caminho;
        public int IndiceCaminho;
        public Vector2 DestinoDoCaminho;
        public float RecalcularCaminhoEm;
        public int VersaoDaGradeDoCaminho;

        public Poder PoderEnfileirado;
        public Vector2 PontoEnfileirado;
        public int AlvoEnfileirado;
        public float ValidadeDaFila;

        public Dictionary<string, float> Recargas = new();
        public List<EfeitoAtivo> Efeitos = new();
        public IA Cerebro;

        /// <summary>Ultimo golpe recebido: a IA usa para revidar e a tela para o numero.</summary>
        public int UltimoAgressor;
        public float TempoDesdeDano = 99;

        public bool Vivo => Acao != EstadoAcao.Morto;
        public float VidaMax => MathF.Max(1, Estat[E.Vida]);
        public float EnergiaMax => MathF.Max(0, Estat[E.Energia]);
        public Vector2 Frente => new(MathF.Cos(Direcao), MathF.Sin(Direcao));
        public bool Ocupado => Acao != EstadoAcao.Livre;
        public ClasseArma ClasseDaArma => Arma?.Arma ?? ClasseArma.Nenhuma;

        public bool TemEfeito(string nome)
        {
            foreach (EfeitoAtivo e in Efeitos)
                if (e.Nome == nome) return true;
            return false;
        }

        public float ValorDoEfeito(string nome)
        {
            float v = 0;
            foreach (EfeitoAtivo e in Efeitos)
                if (e.Nome == nome) v = MathF.Max(v, e.Valor);
            return v;
        }

        public float Recarga(string poder) => Recargas.TryGetValue(poder, out float t) ? t : 0;

        public void OlharPara(Vector2 ponto)
        {
            Vector2 d = ponto - Pos;
            if (d.LengthSquared() > 1e-6f) Direcao = MathF.Atan2(d.Y, d.X);
        }

        /// <summary>Movimento com bonus e efeitos. Atordoado nao anda; enraizado tambem nao.</summary>
        public float Velocidade()
        {
            if (TemEfeito("atordoado") || TemEfeito("enraizado")) return 0;
            float v = VelocidadeBase * (1 + Estat[E.Velocidade] / 100f);
            v *= 1 - MathF.Min(80, ValorDoEfeito("lento")) / 100f;
            v *= 1 + ValorDoEfeito("rapidez") / 100f;
            return MathF.Max(0, v);
        }

        public bool HostilA(Entidade outra)
        {
            if (outra == null || outra == this) return false;
            if (Faccao == Faccao.Neutro || outra.Faccao == Faccao.Neutro) return false;
            return Faccao != outra.Faccao;
        }

        public void LimparOrdem()
        {
            Ordem.Tipo = TipoOrdem.Nenhuma;
            Ordem.Repetir = false;
            Caminho = null;
        }
    }
}
