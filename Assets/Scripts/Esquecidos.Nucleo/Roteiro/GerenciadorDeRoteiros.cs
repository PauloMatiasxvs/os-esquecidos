using System;
using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Comandos;

namespace Esquecidos.Nucleo.Roteiro
{
    /// <summary>Algo que um roteiro precisa esperar acabar antes da proxima linha.</summary>
    public interface IEspera
    {
        bool Concluida(float dt);
    }

    public sealed class EsperaDeTempo : IEspera
    {
        private float _resta;

        public EsperaDeTempo(float segundos)
        {
            _resta = segundos;
        }

        public bool Concluida(float dt)
        {
            _resta -= dt;
            return _resta <= 0;
        }
    }

    public sealed class EsperaAte : IEspera
    {
        private readonly Func<bool> _condicao;
        private float _limite;

        /// <param name="limite">Segundos ate desistir; negativo espera para sempre.</param>
        public EsperaAte(Func<bool> condicao, float limite = -1)
        {
            _condicao = condicao;
            _limite = limite;
        }

        public bool Concluida(float dt)
        {
            if (_limite >= 0)
            {
                _limite -= dt;
                if (_limite <= 0) return true;
            }
            return _condicao();
        }
    }

    /// <summary>Quem avalia "se status x" e afins. O roteiro nao conhece o jogo; pergunta.</summary>
    public interface IAvaliadorDeCondicoes
    {
        bool Avaliar(string tipo, string[] args);
    }

    // Comandos que o proprio gerenciador entende ou emite.

    /// <summary>Executor pedindo para o roteiro que esta rodando esperar algo.</summary>
    public sealed record PedidoDeEspera(IEspera Espera) : IComando;

    [Roteiro("espera", "espera N segundos")]
    public sealed record Esperar(float Segundos) : IComando;

    /// <summary>O roteiro quer que o jogador escolha. A tela mostra e chama Escolher().</summary>
    public sealed record MostrarEscolha(string[] Opcoes) : IComando;

    public sealed record RoteiroComecou(string Nome) : IComando;

    public sealed record RoteiroTerminou(string Nome, bool Falhou, string Erro) : IComando;

    /// <summary>
    /// Uma execucao de roteiro: contador de programa, pilha para "chama" e as
    /// esperas pendentes. Avanca linha a linha ate algo pedir para esperar.
    /// </summary>
    public sealed class ExecucaoDeRoteiro
    {
        private struct Quadro
        {
            public Roteiro Roteiro;
            public int Pc;
        }

        private readonly Stack<Quadro> _pilha = new Stack<Quadro>();
        internal readonly List<IEspera> Esperas = new List<IEspera>();
        internal List<string> DestinosDaEscolha;
        internal int? EscolhaFeita;

        public string Nome { get; }
        public bool Terminada { get; internal set; }
        public bool AguardandoEscolha => DestinosDaEscolha != null;
        public Guid Id { get; } = Guid.NewGuid();

        public ExecucaoDeRoteiro(Roteiro r)
        {
            Nome = r.Nome;
            _pilha.Push(new Quadro { Roteiro = r, Pc = 0 });
        }

        internal bool Proxima(out LinhaDeRoteiro linha, out Roteiro roteiro)
        {
            while (_pilha.Count > 0)
            {
                Quadro q = _pilha.Pop();
                if (q.Pc < q.Roteiro.Linhas.Count)
                {
                    linha = q.Roteiro.Linhas[q.Pc];
                    roteiro = q.Roteiro;
                    q.Pc++;
                    _pilha.Push(q);
                    return true;
                }
            }
            linha = null;
            roteiro = null;
            return false;
        }

        internal void Saltar(string rotulo, LinhaDeRoteiro onde)
        {
            Quadro q = _pilha.Pop();
            if (!q.Roteiro.Rotulos.TryGetValue(rotulo, out int pc))
                throw new FormatException($"{onde.Arquivo}:{onde.Numero} rotulo '{rotulo}' nao existe em @{q.Roteiro.Nome}");
            q.Pc = pc;
            _pilha.Push(q);
        }

        internal void Chamar(Roteiro r)
        {
            if (_pilha.Count > 16) throw new InvalidOperationException("roteiro chamando roteiro fundo demais (laco?)");
            _pilha.Push(new Quadro { Roteiro = r, Pc = 0 });
        }

        internal void Encerrar()
        {
            _pilha.Clear();
        }

        internal void EncerrarQuadroAtual()
        {
            if (_pilha.Count > 0) _pilha.Pop();
        }
    }

    /// <summary>
    /// Guarda os roteiros lidos e roda os que estiverem em andamento, como o
    /// ScriptManager do Pal3. Linhas de fluxo (vai, se, escolha, chama, fim)
    /// sao resolvidas aqui; o resto vira comando e vai para o despachante.
    /// </summary>
    public sealed class GerenciadorDeRoteiros : IExecutor<PedidoDeEspera>, IExecutor<Esperar>
    {
        private readonly Dictionary<string, Roteiro> _roteiros = new Dictionary<string, Roteiro>();
        private readonly List<ExecucaoDeRoteiro> _rodando = new List<ExecucaoDeRoteiro>();
        private readonly Despachante _despachante;
        private readonly FabricaDeComandos _fabrica;
        private ExecucaoDeRoteiro _atual;

        public IAvaliadorDeCondicoes Avaliador { get; set; }
        public IReadOnlyDictionary<string, Roteiro> Roteiros => _roteiros;
        public FabricaDeComandos Fabrica => _fabrica;
        public int Rodando => _rodando.Count;
        public ExecucaoDeRoteiro Atual => _atual;

        /// <summary>Erro de roteiro nao derruba o jogo: termina aquele roteiro e avisa aqui.</summary>
        public event Action<string> Erro;

        public GerenciadorDeRoteiros(Despachante despachante, FabricaDeComandos fabrica)
        {
            _despachante = despachante;
            _fabrica = fabrica;
            despachante.Registro.Registrar(this);
        }

        public void Carregar(string conteudo, string arquivo)
        {
            foreach (Roteiro r in LeitorDeRoteiro.Ler(conteudo, arquivo))
            {
                if (_roteiros.ContainsKey(r.Nome)) throw new FormatException($"{arquivo}: roteiro @{r.Nome} repetido");
                _roteiros[r.Nome] = r;
            }
        }

        public bool Existe(string nome) => nome != null && _roteiros.ContainsKey(nome);

        /// <summary>
        /// Confere todo roteiro carregado sem rodar: comando conhecido, numero
        /// de argumentos, rotulo existente, roteiro chamado existente.
        /// </summary>
        public List<string> Validar()
        {
            var erros = new List<string>();
            foreach (Roteiro r in _roteiros.Values)
            {
                foreach (LinhaDeRoteiro l in r.Linhas)
                {
                    try
                    {
                        switch (l.Comando)
                        {
                            case "rotulo":
                            case "fim":
                            case "retorna":
                                break;
                            case "vai":
                                if (l.Args.Length != 1 || !r.Rotulos.ContainsKey(l.Args[0])) erros.Add($"{l}: rotulo inexistente");
                                break;
                            case "chama":
                                if (l.Args.Length != 1 || !_roteiros.ContainsKey(l.Args[0])) erros.Add($"{l}: roteiro inexistente");
                                break;
                            case "se":
                            {
                                (string destino, _) = PartirSe(l);
                                if (!r.Rotulos.ContainsKey(destino)) erros.Add($"{l}: rotulo '{destino}' inexistente");
                                break;
                            }
                            case "escolha":
                                foreach ((string _, string destino) in PartirEscolha(l))
                                    if (!r.Rotulos.ContainsKey(destino)) erros.Add($"{l}: rotulo '{destino}' inexistente");
                                break;
                            default:
                                _fabrica.Criar(l);
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        erros.Add(e.Message);
                    }
                }
            }
            return erros;
        }

        public ExecucaoDeRoteiro Executar(string nome)
        {
            if (!_roteiros.TryGetValue(nome, out Roteiro r))
            {
                Erro?.Invoke($"roteiro @{nome} nao existe");
                return null;
            }
            var ex = new ExecucaoDeRoteiro(r);
            _rodando.Add(ex);
            _despachante.Despachar(new RoteiroComecou(nome));
            // Roda o que der ja no mesmo quadro: dialogo abre sem atraso.
            Avancar(ex, 0);
            return ex;
        }

        public void Escolher(int indice)
        {
            ExecucaoDeRoteiro ex = _rodando.FirstOrDefault(r => r.AguardandoEscolha);
            if (ex != null) ex.EscolhaFeita = indice;
        }

        public void PararTodos()
        {
            foreach (ExecucaoDeRoteiro ex in _rodando)
            {
                ex.Terminada = true;
                _despachante.Despachar(new RoteiroTerminou(ex.Nome, false, null));
            }
            _rodando.Clear();
        }

        public void Atualizar(float dt)
        {
            foreach (ExecucaoDeRoteiro ex in _rodando.ToList()) Avancar(ex, dt);
        }

        private void Avancar(ExecucaoDeRoteiro ex, float dt)
        {
            if (ex.Terminada) return;
            // Um roteiro pode comecar outro no meio (missao concluida chama o
            // roteiro de fim). O de fora continua sendo o "atual" depois.
            ExecucaoDeRoteiro anterior = _atual;
            _atual = ex;
            try
            {
                for (int seguranca = 0; seguranca < 500; seguranca++)
                {
                    if (ex.AguardandoEscolha)
                    {
                        if (ex.EscolhaFeita == null) return;
                        int i = Math.Max(0, Math.Min(ex.EscolhaFeita.Value, ex.DestinosDaEscolha.Count - 1));
                        string destino = ex.DestinosDaEscolha[i];
                        ex.DestinosDaEscolha = null;
                        ex.EscolhaFeita = null;
                        ex.Saltar(destino, new LinhaDeRoteiro { Arquivo = ex.Nome, Comando = "escolha", Args = Array.Empty<string>() });
                    }
                    if (ex.Esperas.Count > 0)
                    {
                        ex.Esperas.RemoveAll(e => e.Concluida(dt));
                        dt = 0;
                        if (ex.Esperas.Count > 0) return;
                    }
                    if (!ex.Proxima(out LinhaDeRoteiro linha, out Roteiro roteiro))
                    {
                        Terminar(ex, null);
                        return;
                    }
                    Interpretar(ex, linha, roteiro);
                    if (ex.Terminada) return;
                }
                throw new InvalidOperationException($"roteiro @{ex.Nome} rodou 500 linhas sem esperar (laco infinito?)");
            }
            catch (Exception e)
            {
                Terminar(ex, e.Message);
            }
            finally
            {
                _atual = anterior;
            }
        }

        private void Interpretar(ExecucaoDeRoteiro ex, LinhaDeRoteiro l, Roteiro r)
        {
            switch (l.Comando)
            {
                case "rotulo":
                    return;
                case "fim":
                    ex.Encerrar();
                    return;
                case "retorna":
                    ex.EncerrarQuadroAtual();
                    return;
                case "vai":
                    ex.Saltar(l.Args[0], l);
                    return;
                case "chama":
                    if (!_roteiros.TryGetValue(l.Args[0], out Roteiro outro))
                        throw new FormatException($"{l.Arquivo}:{l.Numero} roteiro @{l.Args[0]} nao existe");
                    ex.Chamar(outro);
                    return;
                case "se":
                {
                    (string destino, string[] cond) = PartirSe(l);
                    bool negar = cond.Length > 0 && cond[0] == "nao";
                    if (negar) cond = cond.Skip(1).ToArray();
                    if (cond.Length == 0) throw new FormatException($"{l.Arquivo}:{l.Numero} 'se' sem condicao");
                    bool resultado = Avaliador != null && Avaliador.Avaliar(cond[0], cond.Skip(1).ToArray());
                    if (resultado != negar) ex.Saltar(destino, l);
                    return;
                }
                case "escolha":
                {
                    List<(string texto, string destino)> opcoes = PartirEscolha(l);
                    ex.DestinosDaEscolha = opcoes.Select(o => o.destino).ToList();
                    ex.EscolhaFeita = null;
                    _despachante.Despachar(new MostrarEscolha(opcoes.Select(o => o.texto).ToArray()));
                    return;
                }
            }
            IComando comando = _fabrica.Criar(l);
            _despachante.DespacharDinamico(comando);
        }

        /// <summary>"se [nao] cond args... -> rotulo"</summary>
        private static (string destino, string[] condicao) PartirSe(LinhaDeRoteiro l)
        {
            int seta = Array.IndexOf(l.Args, "->");
            if (seta < 0 || seta != l.Args.Length - 2) throw new FormatException($"{l.Arquivo}:{l.Numero} 'se' precisa terminar em '-> rotulo'");
            return (l.Args[seta + 1], l.Args.Take(seta).ToArray());
        }

        /// <summary>escolha "texto" -> rotulo "texto" -> rotulo ...</summary>
        private static List<(string, string)> PartirEscolha(LinhaDeRoteiro l)
        {
            var opcoes = new List<(string, string)>();
            for (int i = 0; i < l.Args.Length; i += 3)
            {
                if (i + 2 >= l.Args.Length || l.Args[i + 1] != "->")
                    throw new FormatException($"{l.Arquivo}:{l.Numero} 'escolha' e: \"texto\" -> rotulo, repetido");
                opcoes.Add((l.Args[i], l.Args[i + 2]));
            }
            if (opcoes.Count == 0) throw new FormatException($"{l.Arquivo}:{l.Numero} 'escolha' sem opcoes");
            return opcoes;
        }

        private void Terminar(ExecucaoDeRoteiro ex, string erro)
        {
            ex.Terminada = true;
            _rodando.Remove(ex);
            if (erro != null) Erro?.Invoke($"@{ex.Nome}: {erro}");
            _despachante.Despachar(new RoteiroTerminou(ex.Nome, erro != null, erro));
        }

        public void Executar(PedidoDeEspera pedido)
        {
            if (_atual != null) _atual.Esperas.Add(pedido.Espera);
        }

        public void Executar(Esperar comando)
        {
            if (_atual != null) _atual.Esperas.Add(new EsperaDeTempo(comando.Segundos));
        }
    }
}
