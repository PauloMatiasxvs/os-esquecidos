using Esquecidos.Nucleo.Comandos;

namespace Esquecidos.Nucleo.Roteiro
{
    // Tudo que um roteiro pode mandar fazer. Cada record e um comando; o nome
    // entre aspas e como ele se escreve no arquivo, e os parametros do record
    // sao os argumentos, na ordem. Quem executa os de tela e a camada do Unity;
    // os de regra, a Partida.

    // ---- conversa e tela ----

    /// <summary>Fala com retrato e nome. "quem" e o id do NPC, "jogador" ou um nome solto.</summary>
    [Roteiro("fala", "fala <quem> \"texto\"")]
    public sealed record Fala(string Quem, string Texto) : IComando;

    [Roteiro("narra", "narra \"texto\" — caixa sem retrato")]
    public sealed record Narrar(string Texto) : IComando;

    [Roteiro("legenda", "legenda \"texto\" [segundos] — letreiro no meio da tela")]
    public sealed record Legenda(string Texto, float Segundos = 3.5f) : IComando;

    [Roteiro("dica", "dica \"texto\" — caixa de tutorial")]
    public sealed record Dica(string Texto) : IComando;

    [Roteiro("escurece", "escurece [segundos]")]
    public sealed record Escurecer(float Segundos = 0.8f) : IComando;

    [Roteiro("clareia", "clareia [segundos]")]
    public sealed record Clarear(float Segundos = 0.8f) : IComando;

    [Roteiro("camera_foca", "camera_foca <ator> [segundos]")]
    public sealed record CameraFoca(string Ator, float Segundos = 1f) : IComando;

    [Roteiro("camera_volta", "camera_volta [segundos]")]
    public sealed record CameraVolta(float Segundos = 1f) : IComando;

    [Roteiro("tremor", "tremor <forca 0..1>")]
    public sealed record Tremer(float Forca) : IComando;

    [Roteiro("som", "som <nome>")]
    public sealed record TocarSom(string Som) : IComando;

    [Roteiro("musica", "musica <nome>")]
    public sealed record TrocarMusica(string Musica) : IComando;

    [Roteiro("abre_loja", "abre_loja <loja>")]
    public sealed record AbrirLoja(string Loja) : IComando;

    [Roteiro("abre_oficina", "abre_oficina <estacao>")]
    public sealed record AbrirOficina(string Estacao) : IComando;

    [Roteiro("abre_guarda", "abre o bau compartilhado")]
    public sealed record AbrirGuarda() : IComando;

    [Roteiro("abre_mapa", "abre a mesa dos mapas")]
    public sealed record AbrirMapa() : IComando;

    [Roteiro("abre_registro", "abre_registro <id> — mostra um registro da ARCA")]
    public sealed record AbrirRegistro(string Registro) : IComando;

    [Roteiro("creditos", "creditos <final>")]
    public sealed record Creditos(string Final) : IComando;

    // ---- regra e historia ----

    [Roteiro("status", "status <nome> — liga um fato")]
    public sealed record DefinirStatus(string Status) : IComando;

    [Roteiro("tira_status", "tira_status <nome>")]
    public sealed record RemoverStatus(string Status) : IComando;

    [Roteiro("missao_inicia", "missao_inicia <id>")]
    public sealed record IniciarMissao(string Missao) : IComando;

    [Roteiro("missao_etapa", "missao_etapa <id> <etapa>")]
    public sealed record EtapaDaMissao(string Missao, int Etapa) : IComando;

    [Roteiro("missao_conclui", "missao_conclui <id>")]
    public sealed record ConcluirMissao(string Missao) : IComando;

    [Roteiro("da_item", "da_item <id> [quantidade]")]
    public sealed record DarItem(string Item, int Quantidade = 1) : IComando;

    [Roteiro("tira_item", "tira_item <id> [quantidade]")]
    public sealed record TirarItem(string Item, int Quantidade = 1) : IComando;

    [Roteiro("da_xp", "da_xp <quantidade>")]
    public sealed record DarXp(int Quantidade) : IComando;

    [Roteiro("da_sucata", "da_sucata <quantidade>")]
    public sealed record DarSucata(int Quantidade) : IComando;

    [Roteiro("tira_sucata", "tira_sucata <quantidade>")]
    public sealed record TirarSucata(int Quantidade) : IComando;

    [Roteiro("ensina", "ensina <poder> — da o poder no rank 1")]
    public sealed record EnsinarPoder(string Poder) : IComando;

    [Roteiro("cura", "cura vida e folego")]
    public sealed record CurarTudo() : IComando;

    [Roteiro("vai_mapa", "vai_mapa <mapa> <entrada>")]
    public sealed record IrParaMapa(string Mapa, string Entrada) : IComando;

    [Roteiro("ator_anda", "ator_anda <ator> <x> <z> — espera chegar")]
    public sealed record AtorAnda(string Ator, float X, float Z) : IComando;

    [Roteiro("ator_olha", "ator_olha <ator> <outro ator>")]
    public sealed record AtorOlha(string Ator, string Alvo) : IComando;

    [Roteiro("ator_poe", "ator_poe <ator> <x> <z> [direcao em graus]")]
    public sealed record AtorPoe(string Ator, float X, float Z, float Graus = -999) : IComando;

    [Roteiro("npc_aparece", "npc_aparece <npc> <x> <z>")]
    public sealed record NpcAparece(string Npc, float X, float Z) : IComando;

    [Roteiro("npc_some", "npc_some <npc>")]
    public sealed record NpcSome(string Npc) : IComando;

    [Roteiro("inimigo", "inimigo <id> <x> <z> [nivel] [elite 0/1]")]
    public sealed record CriarInimigo(string Inimigo, float X, float Z, int Nivel = 0, bool Elite = false) : IComando;

    [Roteiro("objeto", "objeto <chave> <0/1> — liga/desliga um objeto do mapa")]
    public sealed record EstadoDoObjeto(string Chave, bool Ligado) : IComando;

    [Roteiro("descansa", "dorme ate o amanhecer e salva")]
    public sealed record Descansar() : IComando;

    [Roteiro("avanca_horas", "avanca_horas <horas>")]
    public sealed record AvancarHoras(float Horas) : IComando;

    [Roteiro("revela_mapa", "revela o setor atual no mapa")]
    public sealed record RevelarMapa() : IComando;

    [Roteiro("salva", "salva o jogo")]
    public sealed record SalvarJogo() : IComando;

    [Roteiro("companheiro", "companheiro <npc|nenhum> — quem vai junto para o labirinto")]
    public sealed record DefinirCompanheiro(string Npc) : IComando;

    [Roteiro("entrega_mapas", "entrega o que foi mapeado ao cartografo")]
    public sealed record EntregarMapas() : IComando;

    [Roteiro("esconde_item", "esconde_item <item> — poe o item num beco do setor atual")]
    public sealed record EsconderItem(string Item) : IComando;

    [Roteiro("afinidade", "afinidade <npc> <delta>")]
    public sealed record MudarAfinidade(string Npc, int Delta) : IComando;
}
