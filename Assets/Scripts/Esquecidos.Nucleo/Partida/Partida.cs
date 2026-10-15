using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Progresso;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Roteiro;
using Esquecidos.Nucleo.Simulacao;
using MundoSim = Esquecidos.Nucleo.Simulacao.Mundo;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Nucleo.Partidas
{
    /// <summary>
    /// Uma partida inteira: o heroi, o diario, o relogio, o mapa atual e as
    /// regras que ligam um ao outro. E o que o Pal3 chama de jogo rodando:
    /// recebe as notificacoes da simulacao e os comandos dos roteiros, e
    /// decide o que eles significam (XP, loot, porta trancada, troca de mapa).
    ///
    /// Nao conhece Unity. A camada de jogo chama Passo() a cada quadro, manda
    /// as acoes do jogador pelos metodos publicos e escuta as notificacoes.
    /// </summary>
    public sealed partial class Partida : IAvaliadorDeCondicoes
    {
        public const int TamanhoDaGuarda = 60;
        public const string MapaInicial = "patio";

        public Catalogo Catalogo { get; }
        public Despachante Despachante { get; }
        public RegistroDeExecutores Registro => Despachante.Registro;
        public GerenciadorDeRoteiros Roteiros { get; }
        public Diario Diario { get; }
        public CicloDoDia Ciclo { get; } = new CicloDoDia();
        public GeradorDeLoot Loot { get; }
        public Personagem Heroi { get; private set; }
        public Inventario Guarda { get; } = new Inventario(TamanhoDaGuarda);
        public Random Rng { get; private set; }
        public int SementeDoMundo { get; private set; }

        public Mapa Mapa { get; private set; }
        public MundoSim Mundo { get; private set; }
        public Entidade Jogador => Mundo?.Jogador;
        public Entidade Companheiro { get; private set; }
        public string CompanheiroNpc { get; set; }

        /// <summary>Estado de objetos que persiste: bau aberto, alavanca puxada, gatilho usado.</summary>
        public Dictionary<string, bool> EstadosDeObjetos { get; } = new Dictionary<string, bool>();
        /// <summary>Celulas visitadas, por "setor:dia". E o mapa que o corredor traz para o cartografo.</summary>
        public Dictionary<string, HashSet<int>> Explorado { get; } = new Dictionary<string, HashSet<int>>();
        public Dictionary<string, int> Entregue { get; } = new Dictionary<string, int>();
        public HashSet<string> RegistrosLidos { get; } = new HashSet<string>();
        public Dictionary<string, int> Afinidade { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> Contadores { get; } = new Dictionary<string, int>();
        public float TempoDeJogo { get; private set; }

        /// <summary>Roteiro rodando trava o jogador e a IA, como o Pal3 trava o estado Gameplay.</summary>
        public bool EmCena => Roteiros.Rodando > 0;
        public bool Pausada { get; set; }
        public bool JogadorMorto { get; private set; }

        private (string mapa, string entrada)? _transicao;
        private float _transicaoEm;
        private float _vidaGuardada = -1, _folegoGuardado = -1;
        private readonly Dictionary<(string, int), Poder> _poderesNoRank = new Dictionary<(string, int), Poder>();
        private int _lootPendente;
        private float _recargaDeConsumivel;
        private float _avisoSaidaEm;

        public Partida(Catalogo catalogo, Func<IEnumerable<(string nome, string conteudo)>> roteiros = null)
        {
            Catalogo = catalogo;
            Despachante = new Despachante(new RegistroDeExecutores());
            var fabrica = new FabricaDeComandos();
            fabrica.RegistrarDe(typeof(Partida).Assembly);
            Roteiros = new GerenciadorDeRoteiros(Despachante, fabrica) { Avaliador = this };
            if (roteiros != null)
                foreach ((string nome, string conteudo) in roteiros()) Roteiros.Carregar(conteudo, nome);
            Diario = new Diario(catalogo)
            {
                ContarItem = id => Heroi?.Mochila.Contar(id) ?? 0,
                NivelAtual = () => Heroi?.Nivel ?? 1,
            };
            Loot = new GeradorDeLoot(catalogo);
            Rng = new Random();
            Registro.Registrar(this);
            LigarEventos();
        }

        private void LigarEventos()
        {
            Diario.MissaoIniciada += m => Despachante.Despachar(new NotMissao(m, Diario.TextoDaEtapa(m.Id), true, false));
            Diario.EtapaAvancou += (m, _) => Despachante.Despachar(new NotMissao(m, Diario.TextoDaEtapa(m.Id), false, false));
            Diario.MissaoConcluida += AoConcluirMissao;
            Ciclo.Amanheceu += AoAmanhecer;
            Ciclo.AvisoDePortao += () =>
            {
                Despachante.Despachar(new NotCiclo("aviso"));
                Mensagem(T("A sirene do Pátio tocou: os portões fecham em breve."), "#ffb040");
            };
            Ciclo.PortoesFecharam += () =>
            {
                AtualizarPortoes();
                Despachante.Despachar(new NotCiclo("fecharam"));
            };
            Ciclo.Anoiteceu += AoAnoitecer;
            Ciclo.HoraDaNoite += AoMudarHoraDaNoite;
        }

        // ------------------------------------------------------------------
        // Comeco, carga e o laco
        // ------------------------------------------------------------------

        public void NovaPartida(string classe, string nome, Aparencia aparencia = null, int? semente = null)
        {
            SementeDoMundo = semente ?? new Random().Next(1, int.MaxValue);
            Rng = new Random(SementeDoMundo ^ 0x5eed);
            Heroi = Personagem.Novo(Catalogo, classe, string.IsNullOrWhiteSpace(nome) ? T("Novato") : nome.Trim());
            if (aparencia != null)
            {
                aparencia.Roupa = Heroi.Aparencia.Roupa;
                Heroi.Aparencia = aparencia;
            }
            Ciclo.Definir(1, 7.5f);
            EntrarNoMapa(MapaInicial, "inicio");
            if (Roteiros.Existe("abertura")) Roteiros.Executar("abertura");
        }

        public int SementeDoSetor(string setor, int dia)
        {
            unchecked
            {
                return (SementeDoMundo ^ Hash.Estavel(setor)) * 17 + dia * 7919;
            }
        }

        /// <summary>
        /// Quanto a troca de mapa espera depois de pedida. O jogo usa uns
        /// decimos de segundo para a tela escurecer antes: o tranco de montar
        /// o mapa novo acontece no escuro. Os testes usam zero.
        /// </summary>
        public float AtrasoDeTransicao { get; set; }

        public bool TransicaoPendente => _transicao.HasValue;

        /// <summary>Pede uma troca de mapa. Acontece no fim do passo, fora da simulacao; o primeiro pedido vale.</summary>
        public void IrPara(string mapa, string entrada)
        {
            if (_transicao.HasValue) return;
            _transicao = (mapa, entrada);
            _transicaoEm = AtrasoDeTransicao;
            Despachante.Despachar(new NotTransicao(mapa, entrada));
        }

        public void Passo(float dt)
        {
            if (Mundo == null || Pausada) return;
            TempoDeJogo += dt;
            _recargaDeConsumivel -= dt;
            _avisoSaidaEm -= dt;
            bool cena = EmCena;
            if (Jogador != null)
            {
                if (cena)
                {
                    Jogador.MovimentoDireto = Vector2.Zero;
                }
            }
            foreach (Entidade e in Mundo.Entidades)
                if (e.Cerebro is IAComum && cena) e.LimparOrdem();
            if (!cena) Mundo.Passo(dt);
            else PassoDeCena(dt);
            Roteiros.Atualizar(dt);
            if (!cena && !JogadorMorto) Ciclo.Avancar(dt);
            MarcarExplorado();
            AtualizarLootPendente();
            if (_transicao.HasValue)
            {
                _transicaoEm -= dt;
                if (_transicaoEm <= 0)
                {
                    (string mapa, string entrada) = _transicao.Value;
                    _transicao = null;
                    EntrarNoMapa(mapa, entrada);
                }
            }
        }

        /// <summary>Durante cena so andam os atores que o roteiro mandou andar; bicho nenhum ataca.</summary>
        private void PassoDeCena(float dt)
        {
            foreach (Entidade e in Mundo.Entidades)
            {
                if (!e.Vivo) continue;
                e.PosAnterior = e.Pos;
                if (e.Ordem.Tipo != TipoOrdem.Mover) continue;
                Vector2 falta = e.Ordem.Ponto - e.Pos;
                float d = falta.Length();
                if (d < 0.1f)
                {
                    e.LimparOrdem();
                    e.VelocidadeAtual = 0;
                    continue;
                }
                Vector2 dir = Mundo.SeguirCaminho(e, e.Ordem.Ponto, 0.1f);
                float passo = MathF.Min(d, e.VelocidadeBase * 0.8f * dt);
                e.Pos = Mundo.Grade.ResolverColisao(e.Pos + dir * passo, e.Raio);
                e.VelocidadeAtual = passo / MathF.Max(dt, 1e-4f);
                if (dir != Vector2.Zero) e.Direcao = MathF.Atan2(dir.Y, dir.X);
            }
        }

        // ------------------------------------------------------------------
        // Mapas
        // ------------------------------------------------------------------

        public Mapa ConstruirMapa(string id)
        {
            if (id == "patio") return ConstrutorDoPatio.Construir();
            SetorDef def = Catalogo.Setor(id);
            return GeradorDeSetor.Gerar(def, SementeDoSetor(id, Ciclo.Dia), Ciclo.Dia);
        }

        public void EntrarNoMapa(string id, string entrada)
        {
            _transicao = null;
            if (Jogador != null)
            {
                _vidaGuardada = Jogador.Vida;
                _folegoGuardado = Jogador.Energia;
            }
            Roteiros.PararTodos();
            Mapa = ConstruirMapa(id);
            Mundo = new MundoSim(Mapa.Grade, Catalogo, Despachante, SementeDoSetor(id, Ciclo.Dia) ^ 0x2f) { Noite = Ciclo.Noite };
            Mundo.SucataColetada += l => GanharSucata(l.Sucata, true);
            JogadorMorto = false;

            foreach (ObjetoDeCena o in Mapa.Objetos)
            {
                RestaurarObjeto(o);
                Mundo.AdicionarObjeto(o);
            }

            if (!Mapa.Entradas.TryGetValue(entrada ?? "", out Entrada e))
                e = Mapa.Entradas.Values.First();
            Entidade j = CriarCorpoDoJogador(e);

            foreach (PosicaoDeNpc n in Mapa.Npcs) CriarNpc(n.Npc, n.Pos, n.Direcao);
            foreach (Surgimento s in Mapa.Surgimentos)
            {
                if (s.Noturno && !Ciclo.Noite) continue;
                if (s.Chefe && ChefeDerrotado(Mapa.Id)) continue;
                Surgir(s);
            }
            Companheiro = null;
            if (CompanheiroNpc != null && !Mapa.Seguro) CriarCompanheiro(j);

            Diario.AoEntrarNoMapa(id);
            Despachante.Despachar(new NotMapaCarregado(Mapa, Mundo));
            string roteiroDeEntrada = "entrar_" + id;
            if (Roteiros.Existe(roteiroDeEntrada)) Roteiros.Executar(roteiroDeEntrada);
        }

        private Entidade CriarCorpoDoJogador(Entrada e)
        {
            var j = new Entidade
            {
                Tipo = "jogador",
                Nome = Heroi.Nome,
                Faccao = Faccao.Jogador,
                Pos = e.Pos,
                Direcao = e.Direcao,
                Raio = 0.42f,
                VelocidadeBase = 5.4f,
                Nivel = Heroi.Nivel,
            };
            Mundo.Adicionar(j);
            RecalcularFicha();
            j.Vida = _vidaGuardada > 0 ? MathF.Min(_vidaGuardada, j.VidaMax) : j.VidaMax;
            j.Energia = _folegoGuardado >= 0 ? MathF.Min(_folegoGuardado, j.EnergiaMax) : j.EnergiaMax;
            if (Mapa.Seguro) j.Vida = MathF.Max(j.Vida, j.VidaMax * 0.5f);
            return j;
        }

        public Entidade CriarNpc(string npcId, Vector2 pos, float direcao)
        {
            NpcDef def = Catalogo.Npc(npcId);
            var n = new Entidade
            {
                Tipo = "npc",
                NpcId = npcId,
                Nome = def.Nome,
                Faccao = Faccao.Neutro,
                Pos = pos,
                Origem = pos,
                Direcao = direcao,
                Raio = 0.42f,
                VelocidadeBase = 3.2f,
                Atacavel = false,
                Cerebro = new IANpc { Raio = def.Ocio == "anda" ? 4f : 1.2f },
            };
            n.Estat[Estat.Vida] = 100;
            n.Vida = 100;
            return Mundo.Adicionar(n);
        }

        private void CriarCompanheiro(Entidade dono)
        {
            string def = "aliado_" + CompanheiroNpc;
            if (!Catalogo.Inimigos.TryGetValue(def, out InimigoDef d)) return;
            Vector2 pos = dono.Pos - dono.Frente * 1.5f;
            if (Mundo.Grade.FolgaEm(pos) < d.Raio && Mundo.Grade.LivreMaisProximo(pos, d.Raio, out Vector2 livre)) pos = livre;
            Entidade c = Mundo.CriarInimigo(d, Math.Max(1, Heroi.Nivel), pos);
            c.Faccao = Faccao.Jogador;
            c.Dono = dono.Id;
            c.NpcId = CompanheiroNpc;
            c.Nome = Catalogo.Npc(CompanheiroNpc).Nome;
            c.Cerebro = new IAAliada();
            Companheiro = c;
        }

        private Entidade Surgir(Surgimento s)
        {
            InimigoDef def = Catalogo.Inimigo(s.Inimigo);
            Vector2 pos = s.Pos;
            if (Mundo.Grade.FolgaEm(pos) < def.Raio && Mundo.Grade.LivreMaisProximo(pos, def.Raio, out Vector2 livre)) pos = livre;
            return Mundo.CriarInimigo(def, s.Nivel, pos, s.Elite);
        }

        /// <summary>Devolve ao objeto o estado guardado e o que depende do relogio e da historia.</summary>
        private void RestaurarObjeto(ObjetoDeCena o)
        {
            if (o.Chave != null && EstadosDeObjetos.TryGetValue(o.Chave, out bool ligado))
            {
                o.Ligado = ligado;
                if (o.Tipo == TipoObjeto.Bau || o.Tipo == TipoObjeto.Coletavel || o.Tipo == TipoObjeto.Gatilho) o.Usado = ligado;
            }
            switch (o.Tipo)
            {
                case TipoObjeto.Porta when o.SoDeDia || o.RequerStatus != null:
                    o.Ligado = PortaDeveEstarAberta(o);
                    break;
                case TipoObjeto.MuroMovel:
                    o.Ligado = Ciclo.Noite ? o.Ciclo < 0.5f : o.Ciclo > 0.5f;
                    break;
            }
        }

        private bool PortaDeveEstarAberta(ObjetoDeCena o)
        {
            if (o.RequerStatus != null && !Diario.TemStatus(o.RequerStatus)) return false;
            if (o.SoDeDia && !Ciclo.PortoesAbertos) return false;
            return true;
        }

        public bool ChefeDerrotado(string setor)
        {
            return Catalogo.Setores.TryGetValue(setor, out SetorDef d) && d.StatusDoChefe != null && Diario.TemStatus(d.StatusDoChefe);
        }

        private void AtualizarPortoes()
        {
            if (Mundo == null) return;
            foreach (ObjetoDeCena o in Mundo.Objetos)
            {
                if (o.Tipo != TipoObjeto.Porta || (!o.SoDeDia && o.RequerStatus == null)) continue;
                bool aberta = PortaDeveEstarAberta(o);
                // Portao nao fecha em cima de ninguem: espera a pessoa sair do vao.
                if (!aberta && Mundo.AreaOcupada(o.Bloqueio)) continue;
                Mundo.DefinirLigado(o, aberta);
            }
        }

        // ------------------------------------------------------------------
        // Ficha, experiencia, sucata
        // ------------------------------------------------------------------

        public void RecalcularFicha()
        {
            Entidade j = Jogador;
            if (j == null) return;
            float pctVida = j.VidaMax > 1 ? j.Vida / j.VidaMax : 1;
            float pctFolego = j.EnergiaMax > 0 ? j.Energia / j.EnergiaMax : 1;
            j.Estat = Heroi.Calcular(Catalogo);
            j.Arma = Heroi.Arma(Catalogo);
            j.Nivel = Heroi.Nivel;
            j.Nome = Heroi.Nome;
            j.Vida = MathF.Max(1, j.VidaMax * pctVida);
            j.Energia = j.EnergiaMax * pctFolego;
            Despachante.Despachar(new NotFichaMudou());
        }

        public void GanharXp(int xp)
        {
            if (xp <= 0) return;
            float bonus = Jogador != null ? Jogador.Estat[Estat.BonusXp] : 0;
            int total = (int)MathF.Round(xp * (1 + bonus / 100f));
            int subiu = Heroi.GanharXp(total, Catalogo.Regras);
            if (subiu > 0)
            {
                RecalcularFicha();
                if (Jogador != null)
                {
                    Jogador.Vida = Jogador.VidaMax;
                    Jogador.Energia = Jogador.EnergiaMax;
                }
                Despachante.Despachar(new NotNivelSubiu(Heroi.Nivel));
                Mensagem(F("Nível {0}! +{1} pontos de atributo, +{2} de poder.", Heroi.Nivel, Catalogo.Regras.PontosAtributoPorNivel * subiu, Catalogo.Regras.PontosPoderPorNivel * subiu), "#ffd25a");
                Diario.Reavaliar();
            }
            Despachante.Despachar(new NotFichaMudou());
        }

        public void GanharSucata(int quantidade, bool comBonus = false)
        {
            if (quantidade == 0) return;
            if (comBonus && Jogador != null) quantidade = (int)MathF.Round(quantidade * (1 + Jogador.Estat[Estat.BonusSucata] / 100f));
            Heroi.Sucata = Math.Max(0, Heroi.Sucata + quantidade);
            Despachante.Despachar(new NotSucataMudou(Heroi.Sucata, quantidade));
        }

        public void Contar(string chave, int n = 1)
        {
            Contadores.TryGetValue(chave, out int v);
            Contadores[chave] = v + n;
        }

        public int Contador(string chave) => Contadores.TryGetValue(chave, out int v) ? v : 0;

        public void Mensagem(string texto, string cor = "#e8e2d0") => Despachante.Despachar(new NotMensagem(texto, cor));

        /// <summary>O poder com o rank que o heroi tem nele. Fica guardado; rank novo faz outra copia.</summary>
        public Poder PoderDoHeroi(string id)
        {
            int rank = Math.Max(1, Heroi.RankDe(id));
            if (_poderesNoRank.TryGetValue((id, rank), out Poder p)) return p;
            p = Catalogo.Poder(id).NoRank(rank);
            _poderesNoRank[(id, rank)] = p;
            return p;
        }

        /// <summary>O ataque basico muda com a arma: estilingue atira, cano esmaga, lanca alcanca longe.</summary>
        public Poder PoderBasico()
        {
            ItemBase arma = Heroi.Arma(Catalogo);
            string id;
            switch (arma?.Arma ?? ClasseArma.Nenhuma)
            {
                case ClasseArma.Distancia: id = "ataque_distancia"; break;
                case ClasseArma.Pesada: id = "ataque_pesado"; break;
                case ClasseArma.Haste: id = "ataque_haste"; break;
                case ClasseArma.Choque: id = "ataque_choque"; break;
                case ClasseArma.Leve: id = "ataque"; break;
                default: id = "soco"; break;
            }
            return PoderDoHeroi(id);
        }

        // ------------------------------------------------------------------
        // Relogio
        // ------------------------------------------------------------------

        private void AoAmanhecer()
        {
            if (Mundo == null) return;
            Mundo.Noite = false;
            foreach (ObjetoDeCena o in Mundo.Objetos.Where(x => x.Tipo == TipoObjeto.MuroMovel))
                if (!Mundo.AreaOcupada(o.Bloqueio)) Mundo.DefinirLigado(o, o.Ciclo > 0.5f);
            AtualizarPortoes();
            Despachante.Despachar(new NotCiclo("amanheceu"));
            Mensagem(F("Amanheceu. Dia {0} no labirinto.", Ciclo.Dia), "#ffe9b0");
            if (!Mapa.Seguro && Jogador != null && Jogador.Vivo)
            {
                Contar("noites_sobrevividas");
                Diario.DefinirStatus("sobreviveu_uma_noite");
                GanharXp(60 * Mapa.Nivel);
                Mensagem(T("Você sobreviveu a uma noite no labirinto."), "#ffd25a");
            }
        }

        private void AoAnoitecer()
        {
            if (Mundo == null) return;
            Mundo.Noite = true;
            AtualizarPortoes();
            Despachante.Despachar(new NotCiclo("anoiteceu"));
            if (Mapa.Seguro)
            {
                Mensagem(T("A noite caiu. Os portões estão fechados até o amanhecer."), "#9fb4ff");
                return;
            }
            Mensagem(T("A noite caiu e os portões se fecharam. Agora são eles que caçam."), "#ff6a5a");
            foreach (Surgimento s in Mapa.Surgimentos.Where(x => x.Noturno)) Surgir(s);
        }

        /// <summary>
        /// De madrugada os muros andam. Qualquer combinacao dos muros moveis
        /// mantem o setor conexo (o gerador garantiu), entao da para sortear.
        /// </summary>
        private void AoMudarHoraDaNoite(int hora)
        {
            if (Mundo == null || Mapa.Seguro) return;
            bool mexeu = false;
            foreach (ObjetoDeCena o in Mundo.Objetos.Where(x => x.Tipo == TipoObjeto.MuroMovel))
            {
                if (Rng.NextDouble() > 0.45) continue;
                bool novo = !o.Ligado;
                if (novo && Mundo.AreaOcupada(o.Bloqueio, 1f)) continue;
                Mundo.DefinirLigado(o, novo);
                mexeu = true;
            }
            if (!mexeu) return;
            Despachante.Despachar(new NotCiclo("muros"));
            Despachante.Despachar(new NotTremor(0.35f));
            Mensagem(T("Os muros estão se movendo."), "#c8b8a0");
        }
    }
}
