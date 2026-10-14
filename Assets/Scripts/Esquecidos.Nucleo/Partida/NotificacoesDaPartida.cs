using System.Collections.Generic;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Simulacao;

namespace Esquecidos.Nucleo.Partidas
{
    // O que a partida conta para a interface. Como as da simulacao, quem
    // quiser ouvir registra um executor.

    public sealed record NotMensagem(string Texto, string Cor = "#e8e2d0") : IComando;

    /// <summary>Mapa novo montado: a tela derruba a cena antiga e desenha esta.</summary>
    public sealed record NotMapaCarregado(Mapa Mapa, Simulacao.Mundo Mundo) : IComando;

    public sealed record NotJogadorMorreu(int SucataPerdida) : IComando;

    public sealed record NotNivelSubiu(int Nivel) : IComando;

    public sealed record NotItemRecebido(Item Item) : IComando;

    public sealed record NotSucataMudou(int Total, int Delta) : IComando;

    public sealed record NotMostrarTopicos(Entidade Npc, List<DialogoDef> Topicos) : IComando;

    public sealed record NotMissao(MissaoDef Missao, string Texto, bool Nova, bool Concluida) : IComando;

    /// <summary>"amanheceu", "aviso", "fecharam", "anoiteceu", "muros".</summary>
    public sealed record NotCiclo(string Evento) : IComando;

    public sealed record NotFichaMudou() : IComando;

    public sealed record NotSalvo(string Caminho) : IComando;

    public sealed record NotTransicao(string Mapa, string Entrada) : IComando;

    public sealed record NotRegistroNovo(RegistroDef Registro) : IComando;

    public sealed record NotLojaAberta(string Loja) : IComando;
}
