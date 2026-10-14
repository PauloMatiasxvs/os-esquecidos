using System.Numerics;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Simulacao
{
    // O que a simulacao conta para fora. A tela, o som, as missoes e os
    // testes escutam pelos mesmos executores; a simulacao nao sabe quem esta
    // ouvindo, como as notificacoes do Pal3.

    public sealed record NotEntidadeCriada(Entidade Entidade) : IComando;

    public sealed record NotEntidadeRemovida(Entidade Entidade) : IComando;

    public sealed record NotDano(Entidade Alvo, Entidade Fonte, float Valor, bool Critico, string Elemento, bool Esquivou, bool Bloqueou) : IComando;

    public sealed record NotCura(Entidade Alvo, float Valor) : IComando;

    public sealed record NotMorte(Entidade Morto, Entidade Matador) : IComando;

    /// <summary>Comeco da preparacao do golpe: a animacao comeca aqui.</summary>
    public sealed record NotAtaqueIniciado(Entidade Entidade, Poder Poder, Vector2 Ponto, float Duracao) : IComando;

    /// <summary>O instante em que o golpe acontece: som de corte, rastro, faisca.</summary>
    public sealed record NotImpacto(Entidade Entidade, Poder Poder, Vector2 Ponto) : IComando;

    public sealed record NotProjetilCriado(Projetil Projetil) : IComando;

    public sealed record NotProjetilFim(Projetil Projetil, Vector2 Ponto, bool Acertou) : IComando;

    public sealed record NotAreaCriada(AreaDeEfeito Area) : IComando;

    public sealed record NotAreaExplodiu(AreaDeEfeito Area) : IComando;

    public sealed record NotAreaFim(AreaDeEfeito Area) : IComando;

    public sealed record NotEfeito(Entidade Entidade, string Efeito, bool Aplicado) : IComando;

    public sealed record NotLootCriado(ItemNoChao Loot) : IComando;

    public sealed record NotLootRemovido(ItemNoChao Loot, bool Pego) : IComando;

    public sealed record NotObjetoMudou(ObjetoDeCena Objeto) : IComando;

    public sealed record NotInteracao(Entidade Quem, ObjetoDeCena Objeto) : IComando;

    public sealed record NotFalarComNpc(Entidade Quem, Entidade Npc) : IComando;

    public sealed record NotObjetoPisado(ObjetoDeCena Objeto, Entidade Quem) : IComando;

    public sealed record NotTexto(Vector2 Ponto, string Texto, string Cor) : IComando;

    public sealed record NotSom(string Som, Vector2 Ponto) : IComando;

    public sealed record NotTremor(float Forca) : IComando;

    public sealed record NotPoderNegado(Entidade Entidade, string Motivo) : IComando;
}
