using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Simulacao
{
    /// <summary>
    /// A simulacao de um mapa: entidades, projeteis, areas, itens no chao e
    /// objetos de cenario, tudo num plano visto de cima. Nao desenha nada e
    /// nao sabe que existe tela; conta o que acontece por notificacoes.
    /// </summary>
    public sealed partial class Mundo
    {
        public const float TempoDeCadaver = 4f;
        public const float RaioDeSucataAutomatica = 1.6f;

        public Grade Grade { get; }
        public Catalogo Catalogo { get; }
        public Despachante Despachante { get; }
        public Random Rng { get; }
        public float Tempo { get; private set; }
        public bool Noite { get; set; }
        public Entidade Jogador { get; private set; }

        public List<Entidade> Entidades { get; } = new();
        public List<Projetil> Projeteis { get; } = new();
        public List<AreaDeEfeito> Areas { get; } = new();
        public List<ItemNoChao> Loot { get; } = new();
        public List<ObjetoDeCena> Objetos { get; } = new();

        private readonly Dictionary<int, Entidade> _porId = new();
        private int _proximoId = 1;
        private int _buscasNesteQuadro;
        private const int BuscasPorQuadro = 6;

        public Mundo(Grade grade, Catalogo catalogo, Despachante despachante, int semente)
        {
            Grade = grade;
            Catalogo = catalogo;
            Despachante = despachante;
            Rng = new Random(semente);
        }

        public int NovoId() => _proximoId++;

        public Entidade Buscar(int id) => id != 0 && _porId.TryGetValue(id, out Entidade e) ? e : null;

        public Entidade Adicionar(Entidade e)
        {
            if (e.Id == 0) e.Id = NovoId();
            e.PosAnterior = e.Pos;
            if (e.Origem == default) e.Origem = e.Pos;
            Entidades.Add(e);
            _porId[e.Id] = e;
            if (e.Faccao == Faccao.Jogador && !e.Invocado && e.NpcId == null) Jogador = e;
            Despachante.Despachar(new NotEntidadeCriada(e));
            return e;
        }

        public void Remover(Entidade e)
        {
            if (!_porId.Remove(e.Id)) return;
            Entidades.Remove(e);
            if (Jogador == e) Jogador = null;
            Despachante.Despachar(new NotEntidadeRemovida(e));
        }

        /// <summary>Inimigo no nivel pedido: vida e dano crescem com a diferenca para o nivel base.</summary>
        public Entidade CriarInimigo(InimigoDef def, int nivel, Vector2 pos, bool elite = false)
        {
            int diferenca = nivel - def.Nivel;
            float multVida = MathF.Max(0.4f, 1 + def.VidaPorNivel * diferenca) * (elite ? 2.2f : 1f);
            float multDano = MathF.Max(0.4f, 1 + def.DanoPorNivel * diferenca) * (elite ? 1.35f : 1f);
            var ficha = def.Estat.Copia();
            ficha[Estat.Vida] = def.Vida * multVida;
            foreach (Estat d in new[] { Estat.DanoCorpoMin, Estat.DanoCorpoMax, Estat.DanoDistMin, Estat.DanoDistMax, Estat.DanoTecMin, Estat.DanoTecMax })
                ficha[d] *= multDano;
            if (ficha[Estat.Precisao] == 0) ficha[Estat.Precisao] = 82 + 2 * nivel;
            ficha[Estat.AbsorcaoMin] += nivel * 0.4f;
            ficha[Estat.AbsorcaoMax] += nivel * 0.6f;
            if (ficha[Estat.Tenacidade] == 0) ficha[Estat.Tenacidade] = def.Chefe ? 9999 : 4 + nivel * 1.5f;
            var e = new Entidade
            {
                Tipo = def.Id,
                Nome = elite ? def.Nome + " Alfa" : def.Nome,
                Faccao = Faccao.Inimigo,
                Def = def,
                Nivel = nivel,
                Pos = pos,
                Raio = def.Raio * (elite ? 1.15f : 1f),
                VelocidadeBase = def.Velocidade,
                Estat = ficha,
                Chefe = def.Chefe,
                Elite = elite,
                Voa = def.Voa,
                Direcao = (float)(Rng.NextDouble() * Math.PI * 2),
            };
            e.Vida = e.VidaMax;
            e.Energia = e.EnergiaMax;
            e.Cerebro = IA.Para(def);
            return Adicionar(e);
        }

        public void Passo(float dt)
        {
            Tempo += dt;
            _buscasNesteQuadro = 0;
            foreach (Entidade e in Entidades) e.PosAnterior = e.Pos;

            // Copia: a IA pode invocar ou matar durante o laco.
            Entidade[] vivas = Entidades.ToArray();
            foreach (Entidade e in vivas)
            {
                if (!e.Vivo) continue;
                if (e.Cerebro != null && !e.TemEfeito("atordoado")) e.Cerebro.Pensar(this, e, dt);
            }
            foreach (Entidade e in vivas) AtualizarEntidade(e, dt);
            Separar();
            AtualizarProjeteis(dt);
            AtualizarAreas(dt);
            AtualizarLoot(dt);
            AtualizarObjetos();

            for (int i = Entidades.Count - 1; i >= 0; i--)
            {
                Entidade e = Entidades[i];
                if (!e.Vivo && e.TempoMorto > TempoDeCadaver && e != Jogador) Remover(e);
                else if (e.Vivo && e.Invocado && e.TempoDeVida >= 0 && e.TempoDeVida <= 0) Matar(e, null);
            }
        }

        private void AtualizarEntidade(Entidade e, float dt)
        {
            if (!e.Vivo)
            {
                e.TempoMorto += dt;
                e.Empurrao *= MathF.Exp(-8 * dt);
                e.Pos = Grade.ResolverColisao(e.Pos + e.Empurrao * dt, e.Raio * 0.5f);
                return;
            }
            e.TempoDesdeDano += dt;
            if (e.Invocado && e.TempoDeVida > 0) e.TempoDeVida = MathF.Max(0, e.TempoDeVida - dt);
            if (e.Invulneravel > 0) e.Invulneravel -= dt;
            if (e.Cambaleio > 0) e.Cambaleio -= dt;
            if (e.ValidadeDaFila > 0) e.ValidadeDaFila -= dt;

            if (e.Recargas.Count > 0)
            {
                foreach (string k in e.Recargas.Keys.ToList())
                {
                    float t = e.Recargas[k] - dt;
                    if (t <= 0) e.Recargas.Remove(k);
                    else e.Recargas[k] = t;
                }
            }

            AtualizarEfeitos(e, dt);
            if (!e.Vivo) return;

            float regenVida = e.Estat[Estat.RegenVida] + e.ValorDoEfeito("regeneracao");
            if (e.Faccao == Faccao.Jogador && e.TempoDesdeDano > 6f) regenVida += e.VidaMax * 0.02f;
            e.Vida = MathF.Min(e.VidaMax, e.Vida + regenVida * dt);
            e.Energia = MathF.Min(e.EnergiaMax, e.Energia + e.Estat[Estat.RegenEnergia] * dt);

            if (e.TemEfeito("atordoado"))
            {
                if (e.Acao != EstadoAcao.Atordoado)
                {
                    e.Acao = EstadoAcao.Atordoado;
                    e.PoderAtual = null;
                }
            }
            else if (e.Acao == EstadoAcao.Atordoado)
            {
                e.Acao = EstadoAcao.Livre;
            }

            AtualizarAcao(e, dt);

            if (e.Acao == EstadoAcao.Livre && e.Cambaleio <= 0 && e.PoderEnfileirado != null && e.ValidadeDaFila > 0)
            {
                Poder p = e.PoderEnfileirado;
                e.PoderEnfileirado = null;
                Entidade alvoDaFila = Buscar(e.AlvoEnfileirado);
                bool corpo = p.Tipo == TipoPoder.Corpo || p.Tipo == TipoPoder.Investida;
                if (corpo && alvoDaFila != null && alvoDaFila.Vivo
                    && Vector2.Distance(e.Pos, alvoDaFila.Pos) - alvoDaFila.Raio > AlcanceEfetivo(e, p))
                {
                    // O alvo saiu do alcance enquanto esperava: vira ordem de ir ate ele.
                    e.Ordem.Tipo = TipoOrdem.Atacar;
                    e.Ordem.AlvoId = alvoDaFila.Id;
                    e.Ordem.Poder = p;
                    e.Ordem.Repetir = false;
                }
                else
                {
                    Iniciar(e, p, alvoDaFila != null && alvoDaFila.Vivo ? alvoDaFila.Pos : e.PontoEnfileirado, e.AlvoEnfileirado);
                }
            }

            Vector2 desejo = Vector2.Zero;
            if (e.Acao == EstadoAcao.Livre && e.Cambaleio <= 0) desejo = ExecutarOrdem(e);

            float vel = e.Velocidade();
            Vector2 passo = desejo * vel;
            e.VelocidadeAtual = passo.Length();
            if (e.VelocidadeAtual > 0.05f && e.Acao == EstadoAcao.Livre) VirarPara(e, MathF.Atan2(passo.Y, passo.X), dt);

            Vector2 movimento = (passo + e.Empurrao) * dt;
            e.Empurrao *= MathF.Exp(-9 * dt);
            if (movimento.LengthSquared() > 1e-10f) Mover(e, movimento);
        }

        /// <summary>Anda em subpassos menores que meia celula, para nada atravessar parede.</summary>
        private void Mover(Entidade e, Vector2 movimento)
        {
            float d = movimento.Length();
            int sub = Math.Max(1, (int)MathF.Ceiling(d / (Grade.Celula * 0.45f)));
            Vector2 parte = movimento / sub;
            for (int i = 0; i < sub; i++) e.Pos = Grade.ResolverColisao(e.Pos + parte, e.Raio);
        }

        private static void VirarPara(Entidade e, float angulo, float dt)
        {
            float delta = MathF.IEEERemainder(angulo - e.Direcao, MathF.PI * 2);
            float max = 14f * dt;
            e.Direcao += Math.Clamp(delta, -max, max);
        }

        private Vector2 ExecutarOrdem(Entidade e)
        {
            if (e.MovimentoDireto.LengthSquared() > 0.01f)
            {
                if (e.Ordem.Tipo == TipoOrdem.Mover || e.Ordem.Tipo == TipoOrdem.Interagir) e.LimparOrdem();
                if (e.Ordem.Tipo != TipoOrdem.Atacar) return Vector2.Normalize(e.MovimentoDireto);
            }
            Ordem o = e.Ordem;
            switch (o.Tipo)
            {
                case TipoOrdem.Mover:
                {
                    Vector2 d = SeguirCaminho(e, o.Ponto, 0.15f);
                    if (d == Vector2.Zero && Vector2.Distance(e.Pos, o.Ponto) < 0.6f) e.LimparOrdem();
                    return d;
                }
                case TipoOrdem.Atacar:
                {
                    Entidade alvo = Buscar(o.AlvoId);
                    if (alvo == null || !alvo.Vivo || !e.HostilA(alvo) || o.Poder == null)
                    {
                        e.LimparOrdem();
                        return Vector2.Zero;
                    }
                    float alcance = AlcanceEfetivo(e, o.Poder);
                    float dist = Vector2.Distance(e.Pos, alvo.Pos) - alvo.Raio;
                    bool precisaVisao = o.Poder.Tipo == TipoPoder.Projetil || o.Poder.Tipo == TipoPoder.Area;
                    bool ve = !precisaVisao || Grade.LinhaDeVisao(e.Pos, alvo.Pos);
                    if (dist <= alcance && ve)
                    {
                        if (e.MovimentoDireto.LengthSquared() > 0.01f && !o.Repetir) e.LimparOrdem();
                        e.OlharPara(alvo.Pos);
                        Iniciar(e, o.Poder, alvo.Pos, alvo.Id);
                        if (!o.Repetir) e.LimparOrdem();
                        return Vector2.Zero;
                    }
                    if (e.MovimentoDireto.LengthSquared() > 0.01f)
                    {
                        e.LimparOrdem();
                        return Vector2.Normalize(e.MovimentoDireto);
                    }
                    return SeguirCaminho(e, alvo.Pos, MathF.Max(0.2f, alcance * 0.85f));
                }
                case TipoOrdem.UsarPoder:
                {
                    Poder p = o.Poder;
                    e.LimparOrdem();
                    if (p != null) Iniciar(e, p, o.Ponto, 0);
                    return Vector2.Zero;
                }
                case TipoOrdem.Interagir:
                {
                    ObjetoDeCena obj = Objetos.FirstOrDefault(x => x.Id == o.ObjetoId);
                    Entidade npc = obj == null ? Buscar(o.AlvoId) : null;
                    if (obj == null && npc == null)
                    {
                        e.LimparOrdem();
                        return Vector2.Zero;
                    }
                    Vector2 ponto = obj?.Pos ?? npc.Pos;
                    float raio = obj?.RaioInteracao ?? npc.Raio + 1.4f;
                    if (Vector2.Distance(e.Pos, ponto) <= raio)
                    {
                        e.LimparOrdem();
                        e.OlharPara(ponto);
                        if (obj != null) Interagir(e, obj);
                        else Despachante.Despachar(new NotFalarComNpc(e, npc));
                        return Vector2.Zero;
                    }
                    return SeguirCaminho(e, ponto, raio * 0.8f);
                }
            }
            return Vector2.Zero;
        }

        /// <summary>
        /// Direcao para chegar ao destino. Linha reta quando cabe; A* quando nao,
        /// guardando o caminho e refazendo so quando o destino foge ou a grade muda.
        /// </summary>
        public Vector2 SeguirCaminho(Entidade e, Vector2 destino, float tolerancia)
        {
            Vector2 falta = destino - e.Pos;
            if (falta.Length() <= tolerancia)
            {
                e.Caminho = null;
                return Vector2.Zero;
            }
            float folga = MathF.Max(0.05f, e.Raio - 0.08f);
            if (Grade.CaminhoLivre(e.Pos, destino, folga))
            {
                e.Caminho = null;
                return Vector2.Normalize(falta);
            }
            bool refazer = e.Caminho == null
                           || Vector2.Distance(e.DestinoDoCaminho, destino) > 1.5f
                           || e.VersaoDaGradeDoCaminho != Grade.Versao
                           || Tempo >= e.RecalcularCaminhoEm;
            if (refazer && _buscasNesteQuadro < BuscasPorQuadro)
            {
                _buscasNesteQuadro++;
                e.Caminho = Caminhos.Buscar(Grade, e.Pos, destino, e.Raio);
                e.IndiceCaminho = 0;
                e.DestinoDoCaminho = destino;
                e.VersaoDaGradeDoCaminho = Grade.Versao;
                e.RecalcularCaminhoEm = Tempo + 0.6f + (float)Rng.NextDouble() * 0.4f;
            }
            if (e.Caminho == null || e.Caminho.Count == 0) return Vector2.Zero;
            while (e.IndiceCaminho < e.Caminho.Count && Vector2.Distance(e.Pos, e.Caminho[e.IndiceCaminho]) < 0.35f)
                e.IndiceCaminho++;
            // Pula o ponto atual se o seguinte ja esta a vista: o corpo nao faz
            // a curva no centro exato da celula.
            if (e.IndiceCaminho + 1 < e.Caminho.Count && Grade.CaminhoLivre(e.Pos, e.Caminho[e.IndiceCaminho + 1], folga))
                e.IndiceCaminho++;
            if (e.IndiceCaminho >= e.Caminho.Count)
            {
                e.Caminho = null;
                return Vector2.Normalize(falta);
            }
            Vector2 d = e.Caminho[e.IndiceCaminho] - e.Pos;
            return d.LengthSquared() < 1e-6f ? Vector2.Zero : Vector2.Normalize(d);
        }

        /// <summary>Quanto o poder alcanca, contando o corpo de quem usa.</summary>
        public float AlcanceEfetivo(Entidade e, Poder p)
        {
            float alcance = p.Alcance;
            if (p.UsaArma && e.Arma != null && p.Tipo == TipoPoder.Corpo) alcance = MathF.Max(alcance, e.Arma.Alcance);
            return alcance + e.Raio;
        }

        // ------------------------------------------------------------------
        // Poderes
        // ------------------------------------------------------------------

        /// <summary>
        /// Pede um poder. Se a entidade esta no meio de outro, o pedido entra
        /// numa fila curta: o clique dado no fim de um golpe nao se perde. A
        /// esquiva fura a fila e cancela a recuperacao do golpe anterior.
        /// </summary>
        public bool Pedir(Entidade e, Poder p, Vector2 ponto, int alvoId)
        {
            if (!e.Vivo || p == null) return false;
            bool podeCancelar = p.Tipo == TipoPoder.Esquiva && e.Acao == EstadoAcao.Recuperando;
            if ((e.Ocupado || e.Cambaleio > 0) && !podeCancelar)
            {
                e.PoderEnfileirado = p;
                e.PontoEnfileirado = ponto;
                e.AlvoEnfileirado = alvoId;
                e.ValidadeDaFila = 0.45f;
                return false;
            }
            if (podeCancelar) e.Acao = EstadoAcao.Livre;
            return Iniciar(e, p, ponto, alvoId);
        }

        public string MotivoParaNaoUsar(Entidade e, Poder p)
        {
            if (!e.Vivo) return "morto";
            if (e.TemEfeito("atordoado")) return "atordoado";
            if (e.Recarga(p.Id) > 0) return "recarregando";
            if (e.Energia + 0.001f < p.Custo) return "fôlego insuficiente";
            if (!p.AceitaArma(e.ClasseDaArma)) return "arma errada";
            return null;
        }

        public bool Iniciar(Entidade e, Poder p, Vector2 ponto, int alvoId)
        {
            string motivo = MotivoParaNaoUsar(e, p);
            if (motivo != null)
            {
                if (e == Jogador && motivo != "recarregando") Despachante.Despachar(new NotPoderNegado(e, motivo));
                return false;
            }
            if (e.Ocupado) return false;
            e.Energia -= p.Custo;
            if (p.Recarga > 0)
            {
                float reducao = Math.Clamp(e.Estat[Estat.ReducaoRecarga], 0, 50) / 100f;
                e.Recargas[p.Id] = p.Recarga * (1 - reducao);
            }
            if (p.Alvo != TipoDeAlvo.Proprio) e.OlharPara(ponto);
            e.PoderAtual = p;
            e.PontoAcao = ponto;
            e.InicioAcao = e.Pos;
            e.AlvoAcao = alvoId;
            e.TempoAcao = 0;
            e.ImpactoFeito = false;
            e.AtingidosNaAcao.Clear();
            float velocidade = VelocidadeDeAtaque(e, p);

            switch (p.Tipo)
            {
                case TipoPoder.Esquiva:
                {
                    e.Acao = EstadoAcao.Esquivando;
                    e.DuracaoAcao = 0.42f;
                    e.Invulneravel = 0.34f;
                    Vector2 dir = ponto - e.Pos;
                    if (e.MovimentoDireto.LengthSquared() > 0.01f) dir = e.MovimentoDireto;
                    if (dir.LengthSquared() < 1e-4f) dir = e.Frente;
                    e.Direcao = MathF.Atan2(dir.Y, dir.X);
                    e.LimparOrdem();
                    break;
                }
                case TipoPoder.Investida:
                case TipoPoder.Salto:
                    // Bicho avisa antes do bote: a preparacao e o tempo que o
                    // jogador tem para rolar. O do jogador sai quase na hora.
                    if (p.Antecipacao > 0.02f)
                    {
                        e.Acao = EstadoAcao.Conjurando;
                        e.DuracaoAcao = p.Antecipacao / velocidade;
                    }
                    else
                    {
                        ComecarDeslocamento(e, p, ponto);
                    }
                    break;
                default:
                    e.Acao = EstadoAcao.Conjurando;
                    e.DuracaoAcao = p.Antecipacao / velocidade;
                    break;
            }
            Despachante.Despachar(new NotAtaqueIniciado(e, p, ponto, e.DuracaoAcao + p.Recuperacao / velocidade));
            return true;
        }

        private void ComecarDeslocamento(Entidade e, Poder p, Vector2 ponto)
        {
            e.InicioAcao = e.Pos;
            e.TempoAcao = 0;
            if (p.Tipo == TipoPoder.Investida)
            {
                e.OlharPara(ponto);
                e.Acao = EstadoAcao.Investindo;
                float dist = MathF.Min(p.Distancia > 0 ? p.Distancia : p.Alcance, Vector2.Distance(e.Pos, ponto) + 0.5f);
                e.PontoAcao = e.Pos + e.Frente * dist;
                e.DuracaoAcao = MathF.Max(0.12f, dist / 16f);
                if (e.Faccao == Faccao.Jogador) e.Invulneravel = e.DuracaoAcao * 0.6f;
            }
            else
            {
                e.Acao = EstadoAcao.Saltando;
                Vector2 d = ponto - e.Pos;
                float max = p.Distancia > 0 ? p.Distancia : p.Alcance;
                if (d.Length() > max) d = Vector2.Normalize(d) * max;
                e.PontoAcao = e.Pos + d;
                e.DuracaoAcao = 0.55f;
            }
        }

        public float VelocidadeDeAtaque(Entidade e, Poder p)
        {
            float v = 1 + e.Estat[Estat.VelocidadeAtaque] / 100f;
            if (p.UsaArma && e.Arma != null) v *= e.Arma.VelocidadeAtaque;
            v *= 1 - MathF.Min(60, e.ValorDoEfeito("lento")) / 200f;
            return MathF.Max(0.25f, v);
        }

        private void AtualizarAcao(Entidade e, float dt)
        {
            Poder p = e.PoderAtual;
            switch (e.Acao)
            {
                case EstadoAcao.Conjurando:
                    e.TempoAcao += dt;
                    if (e.TempoAcao >= e.DuracaoAcao)
                    {
                        if (p.Tipo == TipoPoder.Investida || p.Tipo == TipoPoder.Salto)
                        {
                            // Fim da preparacao: mira onde o alvo esta agora e vai.
                            Entidade alvoDoBote = Buscar(e.AlvoAcao);
                            Vector2 destino = alvoDoBote != null && alvoDoBote.Vivo ? alvoDoBote.Pos : e.PontoAcao;
                            ComecarDeslocamento(e, p, destino);
                            break;
                        }
                        e.ImpactoFeito = true;
                        Impacto(e, p);
                        if (!e.Vivo) return;
                        e.Acao = EstadoAcao.Recuperando;
                        e.TempoAcao = 0;
                        e.DuracaoAcao = p.Recuperacao / VelocidadeDeAtaque(e, p);
                    }
                    break;
                case EstadoAcao.Recuperando:
                    e.TempoAcao += dt;
                    if (e.TempoAcao >= e.DuracaoAcao) e.Acao = EstadoAcao.Livre;
                    break;
                case EstadoAcao.Esquivando:
                {
                    e.TempoAcao += dt;
                    float dist = p.Distancia > 0 ? p.Distancia : 4.5f;
                    float t = e.TempoAcao / e.DuracaoAcao;
                    // Rapido no comeco e freando no fim, que e o que um rolamento parece.
                    float vel = dist / e.DuracaoAcao * 1.6f * MathF.Max(0.15f, 1 - t);
                    Mover(e, e.Frente * vel * dt);
                    if (e.TempoAcao >= e.DuracaoAcao) e.Acao = EstadoAcao.Livre;
                    break;
                }
                case EstadoAcao.Investindo:
                {
                    e.TempoAcao += dt;
                    Vector2 antes = e.Pos;
                    Vector2 falta = e.PontoAcao - e.Pos;
                    float passo = 16f * dt;
                    if (falta.Length() > passo) Mover(e, Vector2.Normalize(falta) * passo);
                    else Mover(e, falta);
                    foreach (Entidade alvo in Hostis(e, e.Pos, e.Raio + 0.6f))
                    {
                        if (!e.AtingidosNaAcao.Add(alvo.Id)) continue;
                        Atingir(e, alvo, p, 1f, antes);
                        AplicarEfeitoDoPoder(alvo, p, e.Id);
                    }
                    bool parou = Vector2.Distance(antes, e.Pos) < passo * 0.25f;
                    if (falta.Length() <= passo || parou || e.TempoAcao > e.DuracaoAcao * 1.6f)
                    {
                        Despachante.Despachar(new NotImpacto(e, p, e.Pos));
                        AplicarEfeitoProprio(e, p);
                        e.Acao = EstadoAcao.Recuperando;
                        e.TempoAcao = 0;
                        e.DuracaoAcao = p.Recuperacao;
                    }
                    break;
                }
                case EstadoAcao.Saltando:
                {
                    e.TempoAcao += dt;
                    float t = MathF.Min(1, e.TempoAcao / e.DuracaoAcao);
                    e.Pos = Vector2.Lerp(e.InicioAcao, e.PontoAcao, t);
                    if (t >= 1)
                    {
                        e.Pos = Grade.ResolverColisao(e.Pos, e.Raio);
                        if (Grade.FolgaEm(e.Pos) < e.Raio * 0.5f && Grade.LivreMaisProximo(e.Pos, e.Raio, out Vector2 livre)) e.Pos = livre;
                        Despachante.Despachar(new NotImpacto(e, p, e.Pos));
                        Despachante.Despachar(new NotTremor(0.5f));
                        foreach (Entidade alvo in Hostis(e, e.Pos, p.Raio).ToList())
                        {
                            Atingir(e, alvo, p, 1f, e.Pos);
                            AplicarEfeitoDoPoder(alvo, p, e.Id);
                        }
                        AplicarEfeitoProprio(e, p);
                        e.Acao = EstadoAcao.Recuperando;
                        e.TempoAcao = 0;
                        e.DuracaoAcao = p.Recuperacao;
                    }
                    break;
                }
            }
        }

        /// <summary>O momento em que o poder acontece de fato.</summary>
        private void Impacto(Entidade e, Poder p)
        {
            Vector2 alvoPonto = e.PontoAcao;
            Entidade alvoEnt = Buscar(e.AlvoAcao);
            if (alvoEnt != null && alvoEnt.Vivo && p.Tipo != TipoPoder.Corpo) alvoPonto = alvoEnt.Pos;
            Despachante.Despachar(new NotImpacto(e, p, alvoPonto));

            switch (p.Tipo)
            {
                case TipoPoder.Corpo:
                {
                    float alcance = AlcanceEfetivo(e, p);
                    float meioAngulo = p.Angulo * MathF.PI / 360f;
                    Vector2 frente = e.Frente;
                    bool acertou = false;
                    foreach (Entidade alvo in Hostis(e, e.Pos, alcance + 1.5f).ToList())
                    {
                        Vector2 d = alvo.Pos - e.Pos;
                        float dist = d.Length() - alvo.Raio;
                        if (dist > alcance) continue;
                        if (p.Angulo < 360 && d.LengthSquared() > 1e-4f)
                        {
                            float ang = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(d), frente), -1, 1));
                            // Quem esta colado conta mesmo de lado: o corpo encosta.
                            if (ang > meioAngulo && dist > 0.35f) continue;
                        }
                        if (!Grade.LinhaDeVisao(e.Pos, alvo.Pos)) continue;
                        Atingir(e, alvo, p, 1f, e.Pos);
                        AplicarEfeitoDoPoder(alvo, p, e.Id);
                        acertou = true;
                    }
                    if (acertou) Despachante.Despachar(new NotTremor(e == Jogador ? 0.18f : 0.3f));
                    break;
                }
                case TipoPoder.Projetil:
                {
                    Vector2 dir = alvoPonto - e.Pos;
                    if (dir.LengthSquared() < 1e-4f) dir = e.Frente;
                    dir = Vector2.Normalize(dir);
                    float baseAng = MathF.Atan2(dir.Y, dir.X);
                    int n = Math.Max(1, p.Quantidade);
                    float leque = p.Espalhamento * MathF.PI / 180f;
                    for (int i = 0; i < n; i++)
                    {
                        float ang = baseAng + (n == 1 ? 0 : leque * (i / (float)(n - 1) - 0.5f));
                        var v = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                        var proj = new Projetil
                        {
                            Id = NovoId(),
                            Pos = e.Pos + v * (e.Raio + 0.25f),
                            Velocidade = v * p.Velocidade,
                            AlcanceRestante = p.Alcance + 2f,
                            Raio = p.Raio > 0 ? 0.3f : 0.22f,
                            Dono = e.Id,
                            Faccao = e.Faccao,
                            Poder = p,
                            Altura = e.Voa ? 2.2f : 1.1f,
                        };
                        proj.PosAnterior = proj.Pos;
                        Projeteis.Add(proj);
                        Despachante.Despachar(new NotProjetilCriado(proj));
                    }
                    break;
                }
                case TipoPoder.Area:
                case TipoPoder.Armadilha:
                {
                    Vector2 centro = p.Alvo == TipoDeAlvo.Proprio ? e.Pos : alvoPonto;
                    Vector2 d = centro - e.Pos;
                    if (d.Length() > p.Alcance) centro = e.Pos + Vector2.Normalize(d) * p.Alcance;
                    if (Grade.PrimeiroSolido(e.Pos, centro, out Vector2 bate)) centro = bate;
                    CriarArea(e, p, centro);
                    break;
                }
                case TipoPoder.Cura:
                {
                    float valor = RolarDano(e, p);
                    Curar(e, valor);
                    if (p.Raio > 0)
                        foreach (Entidade aliado in Entidades.Where(a => a.Vivo && a != e && a.Faccao == e.Faccao && Vector2.Distance(a.Pos, e.Pos) <= p.Raio).ToList())
                            Curar(aliado, valor * 0.7f);
                    break;
                }
                case TipoPoder.Grito:
                {
                    foreach (Entidade alvo in Hostis(e, e.Pos, p.Raio).ToList())
                    {
                        if (p.Fonte != FonteDeDano.Nenhuma || p.DanoFixoMax > 0) Atingir(e, alvo, p, 1f, e.Pos);
                        AplicarEfeitoDoPoder(alvo, p, e.Id);
                        if (alvo.Cerebro != null) alvo.UltimoAgressor = e.Id;
                    }
                    Despachante.Despachar(new NotTremor(0.35f));
                    break;
                }
                case TipoPoder.Invocacao:
                    Invocar(e, p, alvoPonto);
                    break;
            }
            AplicarEfeitoProprio(e, p);
        }

        public void CriarArea(Entidade dono, Poder p, Vector2 centro)
        {
            var a = new AreaDeEfeito
            {
                Id = NovoId(),
                Pos = centro,
                Raio = MathF.Max(0.5f, p.Raio),
                Atraso = p.Atraso,
                Duracao = p.Tipo == TipoPoder.Armadilha ? MathF.Max(5, p.Duracao) : p.Duracao,
                Armadilha = p.Tipo == TipoPoder.Armadilha,
                Dono = dono?.Id ?? 0,
                Faccao = dono?.Faccao ?? Faccao.Inimigo,
                Poder = p,
            };
            Areas.Add(a);
            Despachante.Despachar(new NotAreaCriada(a));
        }

        private void Invocar(Entidade e, Poder p, Vector2 ponto)
        {
            if (p.Invoca == null || !Catalogo.Inimigos.TryGetValue(p.Invoca, out InimigoDef def)) return;
            int max = Math.Max(1, p.Quantidade);
            List<Entidade> minhas = Entidades.Where(x => x.Vivo && x.Invocado && x.Dono == e.Id && x.Tipo == def.Id).ToList();
            while (minhas.Count >= max)
            {
                Matar(minhas[0], null);
                minhas.RemoveAt(0);
            }
            Vector2 pos = e.Pos + e.Frente * (e.Raio + def.Raio + 0.4f);
            if (Grade.FolgaEm(pos) < def.Raio && Grade.LivreMaisProximo(pos, def.Raio, out Vector2 livre)) pos = livre;
            Entidade nova = CriarInimigo(def, e.Nivel, pos);
            nova.Faccao = e.Faccao;
            nova.Invocado = true;
            nova.Dono = e.Id;
            nova.TempoDeVida = p.Duracao > 0 ? p.Duracao : -1;
            if (e.Faccao == Faccao.Jogador) nova.Cerebro = new IAAliada();
        }

        // ------------------------------------------------------------------
        // Dano, cura, efeitos
        // ------------------------------------------------------------------

        public float RolarDano(Entidade atacante, Poder p)
        {
            float min = p.DanoFixoMin, max = p.DanoFixoMax;
            if (atacante != null && p.Fonte != FonteDeDano.Nenhuma)
            {
                Ficha f = atacante.Estat;
                float smin, smax;
                switch (p.Fonte)
                {
                    case FonteDeDano.Distancia: smin = f[Estat.DanoDistMin]; smax = f[Estat.DanoDistMax]; break;
                    case FonteDeDano.Tecnologia: smin = f[Estat.DanoTecMin]; smax = f[Estat.DanoTecMax]; break;
                    default: smin = f[Estat.DanoCorpoMin]; smax = f[Estat.DanoCorpoMax]; break;
                }
                if (p.UsaArma && atacante.Arma != null)
                {
                    smin += atacante.Arma.DanoMin;
                    smax += atacante.Arma.DanoMax;
                }
                min += smin * p.Multiplicador;
                max += smax * p.Multiplicador;
            }
            float dano = min + (float)Rng.NextDouble() * MathF.Max(0, max - min);
            if (atacante != null)
            {
                dano *= 1 + atacante.Estat[Estat.DanoPct] / 100f;
                dano *= 1 + atacante.ValorDoEfeito("furia") / 100f;
            }
            return MathF.Max(0, dano);
        }

        /// <summary>
        /// Um golpe que chegou: esquiva, acerto, critico, absorcao, bloqueio,
        /// resistencia, empurrao, cambaleio e morte, nessa ordem.
        /// </summary>
        public void Atingir(Entidade atacante, Entidade alvo, Poder p, float mult, Vector2 origem)
        {
            if (alvo == null || !alvo.Vivo || !alvo.Atacavel) return;
            Regras.Regras r = Catalogo.Regras;
            if (alvo.Invulneravel > 0)
            {
                Despachante.Despachar(new NotDano(alvo, atacante, 0, false, p.Elemento, true, false));
                return;
            }
            if (atacante != null && p.Fonte != FonteDeDano.Nenhuma)
            {
                float chance = Math.Clamp(atacante.Estat[Estat.Precisao] - alvo.Estat[Estat.Esquiva], r.AcertoMinimo, r.AcertoMaximo);
                if (Rng.NextDouble() * 100 >= chance)
                {
                    Despachante.Despachar(new NotDano(alvo, atacante, 0, false, p.Elemento, true, false));
                    Provocar(alvo, atacante);
                    return;
                }
            }

            float dano = RolarDano(atacante, p) * mult;
            bool critico = false;
            if (atacante != null)
            {
                float chanceCritico = atacante.Estat[Estat.Critico] + alvo.ValorDoEfeito("vulneravel") * 0.5f;
                if (Rng.NextDouble() * 100 < chanceCritico)
                {
                    critico = true;
                    dano *= r.MultiplicadorCritico + atacante.Estat[Estat.DanoCritico] / 100f;
                }
            }

            bool fisico = p.Elemento == "fisico";
            if (fisico)
            {
                float absMin = alvo.Estat[Estat.AbsorcaoMin] + alvo.ValorDoEfeito("pele");
                float absMax = alvo.Estat[Estat.AbsorcaoMax] + alvo.ValorDoEfeito("pele");
                float absorcao = absMin + (float)Rng.NextDouble() * MathF.Max(0, absMax - absMin);
                dano -= MathF.Min(absorcao, dano * r.AbsorcaoMaximaPct / 100f);
            }
            else
            {
                Estat res = p.Elemento == "veneno" ? Estat.ResVeneno : p.Elemento == "fogo" ? Estat.ResFogo : Estat.ResChoque;
                dano *= 1 - Math.Clamp(alvo.Estat[res], -100, r.ResistenciaMaxima) / 100f;
            }

            bool bloqueou = false;
            if (atacante != null && alvo.Estat[Estat.Bloqueio] > 0 && fisico && alvo.Acao != EstadoAcao.Conjurando)
            {
                Vector2 vindo = atacante.Pos - alvo.Pos;
                if (vindo.LengthSquared() > 1e-4f && Vector2.Dot(Vector2.Normalize(vindo), alvo.Frente) > 0.2f
                    && Rng.NextDouble() * 100 < MathF.Min(60, alvo.Estat[Estat.Bloqueio]))
                {
                    bloqueou = true;
                    dano *= 0.35f;
                }
            }

            dano *= 1 + alvo.ValorDoEfeito("vulneravel") / 100f;
            dano = AbsorverComBarreira(alvo, dano);
            dano = MathF.Max(dano > 0 ? 1 : 0, MathF.Round(dano));

            alvo.Vida -= dano;
            alvo.TempoDesdeDano = 0;
            Despachante.Despachar(new NotDano(alvo, atacante, dano, critico, p.Elemento, false, bloqueou));

            if (atacante != null && atacante.Vivo)
            {
                float roubo = atacante.Estat[Estat.RouboVida] + p.RouboDeVida;
                if (roubo > 0 && dano > 0) Curar(atacante, dano * roubo / 100f, false);
                if (p.Tipo == TipoPoder.Corpo && alvo.Estat[Estat.Espinhos] > 0 && atacante != alvo)
                {
                    float volta = alvo.Estat[Estat.Espinhos];
                    atacante.Vida -= volta;
                    Despachante.Despachar(new NotDano(atacante, alvo, volta, false, "fisico", false, false));
                    if (atacante.Vida <= 0) Matar(atacante, alvo);
                }
            }

            Vector2 dir = alvo.Pos - origem;
            if (dir.LengthSquared() > 1e-4f && p.Empurrao > 0)
            {
                float massa = alvo.Chefe ? 8f : MathF.Max(1, alvo.Raio * 2.2f);
                alvo.Empurrao += Vector2.Normalize(dir) * p.Empurrao * (critico ? 1.6f : 1f) * 3.2f / massa;
            }
            if (!alvo.Chefe && dano >= alvo.Estat[Estat.Tenacidade] && alvo.Faccao != Faccao.Jogador)
            {
                alvo.Cambaleio = 0.22f;
                if (alvo.Acao == EstadoAcao.Conjurando)
                {
                    alvo.Acao = EstadoAcao.Livre;
                    alvo.PoderAtual = null;
                }
            }
            else if (alvo.Faccao == Faccao.Jogador && dano >= alvo.VidaMax * 0.18f)
            {
                alvo.Cambaleio = 0.15f;
            }

            Provocar(alvo, atacante);
            if (alvo.Vida <= 0) Matar(alvo, atacante);
        }

        private void Provocar(Entidade alvo, Entidade atacante)
        {
            if (atacante == null || !alvo.HostilA(atacante)) return;
            alvo.UltimoAgressor = atacante.Id;
            alvo.Cerebro?.AoSerAtingido(this, alvo, atacante);
        }

        private float AbsorverComBarreira(Entidade alvo, float dano)
        {
            foreach (EfeitoAtivo ef in alvo.Efeitos)
            {
                if (ef.Nome != "barreira" || ef.Valor <= 0) continue;
                float absorvido = MathF.Min(ef.Valor, dano);
                ef.Valor -= absorvido;
                dano -= absorvido;
                if (ef.Valor <= 0) ef.Restante = 0;
                if (dano <= 0) break;
            }
            return dano;
        }

        public void Curar(Entidade alvo, float valor, bool mostrar = true)
        {
            if (!alvo.Vivo || valor <= 0) return;
            float antes = alvo.Vida;
            alvo.Vida = MathF.Min(alvo.VidaMax, alvo.Vida + valor);
            if (mostrar && alvo.Vida - antes >= 1) Despachante.Despachar(new NotCura(alvo, alvo.Vida - antes));
        }

        public void Matar(Entidade alvo, Entidade matador)
        {
            if (!alvo.Vivo) return;
            alvo.Vida = 0;
            alvo.Acao = EstadoAcao.Morto;
            alvo.TempoMorto = 0;
            alvo.PoderAtual = null;
            alvo.LimparOrdem();
            foreach (EfeitoAtivo ef in alvo.Efeitos) Despachante.Despachar(new NotEfeito(alvo, ef.Nome, false));
            alvo.Efeitos.Clear();
            Despachante.Despachar(new NotMorte(alvo, matador));
            if (alvo.Chefe) Despachante.Despachar(new NotTremor(1f));
        }

        private void AplicarEfeitoDoPoder(Entidade alvo, Poder p, int fonte)
        {
            if (p.Efeito == null || !alvo.Vivo) return;
            if (alvo.Chefe && (p.Efeito.Nome == "atordoado" || p.Efeito.Nome == "enraizado"))
            {
                // Chefe atordoado por um grito trivializa a luta: vira lentidao curta.
                AplicarEfeito(alvo, "lento", p.Efeito.Duracao * 0.5f, 30, fonte);
                return;
            }
            float valor = p.Efeito.Valor;
            bool continuo = p.Efeito.Nome == "sangramento" || p.Efeito.Nome == "veneno" || p.Efeito.Nome == "queimadura";
            Entidade quem = Buscar(fonte);
            // Veneno de bicho do fim do jogo nao pode doer igual ao do comeco.
            if (continuo && quem != null) valor *= 1 + 0.12f * (quem.Nivel - 1);
            AplicarEfeito(alvo, p.Efeito.Nome, p.Efeito.Duracao, valor, fonte);
        }

        private void AplicarEfeitoProprio(Entidade e, Poder p)
        {
            if (p.EfeitoProprio != null) AplicarEfeito(e, p.EfeitoProprio.Nome, p.EfeitoProprio.Duracao, p.EfeitoProprio.Valor, e.Id);
        }

        /// <summary>Efeito repetido renova a duracao e fica com o valor maior; nao empilha.</summary>
        public void AplicarEfeito(Entidade alvo, string nome, float duracao, float valor, int fonte)
        {
            if (!alvo.Vivo || duracao <= 0) return;
            if (nome == "provocado")
            {
                alvo.UltimoAgressor = fonte;
                alvo.Cerebro?.AoSerAtingido(this, alvo, Buscar(fonte));
            }
            EfeitoAtivo existente = alvo.Efeitos.FirstOrDefault(x => x.Nome == nome);
            if (existente != null)
            {
                existente.Restante = MathF.Max(existente.Restante, duracao);
                existente.Duracao = MathF.Max(existente.Duracao, duracao);
                existente.Valor = nome == "barreira" ? existente.Valor + valor : MathF.Max(existente.Valor, valor);
                existente.Fonte = fonte;
                return;
            }
            alvo.Efeitos.Add(new EfeitoAtivo { Nome = nome, Restante = duracao, Duracao = duracao, Valor = valor, Fonte = fonte, Tique = 0.5f });
            Despachante.Despachar(new NotEfeito(alvo, nome, true));
        }

        public void RemoverEfeito(Entidade alvo, string nome)
        {
            int n = alvo.Efeitos.RemoveAll(x => x.Nome == nome);
            if (n > 0) Despachante.Despachar(new NotEfeito(alvo, nome, false));
        }

        private void AtualizarEfeitos(Entidade e, float dt)
        {
            for (int i = e.Efeitos.Count - 1; i >= 0; i--)
            {
                EfeitoAtivo ef = e.Efeitos[i];
                ef.Restante -= dt;
                if (ef.Nome == "sangramento" || ef.Nome == "veneno" || ef.Nome == "queimadura")
                {
                    ef.Tique -= dt;
                    if (ef.Tique <= 0)
                    {
                        ef.Tique += 0.5f;
                        float dano = ef.Valor * 0.5f;
                        if (ef.Nome == "veneno") dano *= 1 - Math.Clamp(e.Estat[Estat.ResVeneno], -100, 75) / 100f;
                        if (ef.Nome == "queimadura") dano *= 1 - Math.Clamp(e.Estat[Estat.ResFogo], -100, 75) / 100f;
                        dano = MathF.Max(1, MathF.Round(dano));
                        e.Vida -= dano;
                        e.TempoDesdeDano = 0;
                        Entidade fonte = Buscar(ef.Fonte);
                        Despachante.Despachar(new NotDano(e, fonte, dano, false, ef.Nome, false, false));
                        if (e.Vida <= 0)
                        {
                            Matar(e, fonte);
                            return;
                        }
                    }
                }
                if (ef.Restante <= 0)
                {
                    e.Efeitos.RemoveAt(i);
                    Despachante.Despachar(new NotEfeito(e, ef.Nome, false));
                }
            }
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public IEnumerable<Entidade> Hostis(Entidade de, Vector2 centro, float raio)
        {
            foreach (Entidade e in Entidades)
            {
                if (!e.Vivo || !e.Atacavel || !de.HostilA(e)) continue;
                if (Vector2.Distance(e.Pos, centro) - e.Raio <= raio) yield return e;
            }
        }

        public IEnumerable<Entidade> HostisDaFaccao(Faccao faccao, Vector2 centro, float raio)
        {
            foreach (Entidade e in Entidades)
            {
                if (!e.Vivo || !e.Atacavel || e.Faccao == Faccao.Neutro || e.Faccao == faccao) continue;
                if (Vector2.Distance(e.Pos, centro) - e.Raio <= raio) yield return e;
            }
        }

        public Entidade HostilMaisProximo(Entidade de, float raio, bool exigeVisao)
        {
            Entidade melhor = null;
            float melhorD = float.MaxValue;
            foreach (Entidade e in Hostis(de, de.Pos, raio))
            {
                float d = Vector2.DistanceSquared(e.Pos, de.Pos);
                if (d >= melhorD) continue;
                if (exigeVisao && !Grade.LinhaDeVisao(de.Pos, e.Pos)) continue;
                melhor = e;
                melhorD = d;
            }
            return melhor;
        }

        /// <summary>Empurra para fora quem esta sobreposto. Chefe pesa mais e quase nao sai do lugar.</summary>
        private void Separar()
        {
            int n = Entidades.Count;
            for (int i = 0; i < n; i++)
            {
                Entidade a = Entidades[i];
                if (!a.Vivo || a.Acao == EstadoAcao.Saltando) continue;
                for (int j = i + 1; j < n; j++)
                {
                    Entidade b = Entidades[j];
                    if (!b.Vivo || b.Acao == EstadoAcao.Saltando) continue;
                    if (a.Voa != b.Voa) continue;
                    Vector2 d = b.Pos - a.Pos;
                    float min = a.Raio + b.Raio;
                    float d2 = d.LengthSquared();
                    if (d2 >= min * min) continue;
                    float dist = MathF.Sqrt(d2);
                    Vector2 normal = dist > 1e-4f ? d / dist : new Vector2(1, 0);
                    float sobra = min - dist;
                    float ma = a.Chefe ? 10 : a.Raio * a.Raio, mb = b.Chefe ? 10 : b.Raio * b.Raio;
                    if (a.Esquivando()) ma *= 0.1f;
                    if (b.Esquivando()) mb *= 0.1f;
                    float total = ma + mb;
                    a.Pos = Grade.ResolverColisao(a.Pos - normal * sobra * (mb / total), a.Raio);
                    b.Pos = Grade.ResolverColisao(b.Pos + normal * sobra * (ma / total), b.Raio);
                }
            }
        }

        private void AtualizarProjeteis(float dt)
        {
            for (int i = Projeteis.Count - 1; i >= 0; i--)
            {
                Projetil pr = Projeteis[i];
                pr.PosAnterior = pr.Pos;
                Vector2 novo = pr.Pos + pr.Velocidade * dt;
                float andou = Vector2.Distance(pr.Pos, novo);
                bool acabou = false;
                Vector2 pontoFim = novo;
                bool acertou = false;

                if (Grade.PrimeiroSolido(pr.Pos, novo, out Vector2 parede))
                {
                    novo = parede;
                    pontoFim = parede;
                    acabou = true;
                }
                Entidade dono = Buscar(pr.Dono);
                foreach (Entidade alvo in Entidades)
                {
                    if (!alvo.Vivo || !alvo.Atacavel || alvo.Faccao == pr.Faccao || alvo.Faccao == Faccao.Neutro) continue;
                    if (pr.Atingidos.Contains(alvo.Id)) continue;
                    if (DistanciaAoSegmento(alvo.Pos, pr.Pos, novo) > alvo.Raio + pr.Raio) continue;
                    pr.Atingidos.Add(alvo.Id);
                    if (pr.Poder.Raio <= 0)
                    {
                        Atingir(dono, alvo, pr.Poder, 1f, pr.Pos);
                        AplicarEfeitoDoPoder(alvo, pr.Poder, pr.Dono);
                    }
                    acertou = true;
                    if (!pr.Poder.Perfura)
                    {
                        pontoFim = alvo.Pos - Vector2.Normalize(pr.Velocidade) * alvo.Raio;
                        acabou = true;
                        break;
                    }
                }
                pr.Pos = novo;
                pr.AlcanceRestante -= andou;
                if (pr.AlcanceRestante <= 0) acabou = true;
                if (!acabou) continue;
                pr.Vivo = false;
                Projeteis.RemoveAt(i);
                if (pr.Poder.Raio > 0)
                {
                    var p = pr.Poder;
                    var area = new AreaDeEfeito
                    {
                        Id = NovoId(), Pos = pontoFim, Raio = p.Raio, Atraso = 0, Duracao = p.Duracao,
                        Dono = pr.Dono, Faccao = pr.Faccao, Poder = p,
                    };
                    Areas.Add(area);
                    Despachante.Despachar(new NotAreaCriada(area));
                }
                Despachante.Despachar(new NotProjetilFim(pr, pontoFim, acertou));
            }
        }

        public static float DistanciaAoSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.LengthSquared();
            if (l2 < 1e-8f) return Vector2.Distance(p, a);
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0, 1);
            return Vector2.Distance(p, a + ab * t);
        }

        private void AtualizarAreas(float dt)
        {
            for (int i = Areas.Count - 1; i >= 0; i--)
            {
                AreaDeEfeito a = Areas[i];
                Entidade dono = Buscar(a.Dono);
                if (a.Armadilha && !a.Explodiu)
                {
                    a.Duracao -= dt;
                    bool pisou = HostisDaFaccao(a.Faccao, a.Pos, a.Raio * 0.6f).Any();
                    if (pisou) Explodir(a, dono);
                    else if (a.Duracao <= 0) a.Viva = false;
                }
                else if (!a.Explodiu)
                {
                    a.Atraso -= dt;
                    if (a.Atraso <= 0)
                    {
                        Explodir(a, dono);
                        if (a.Armadilha) a.Duracao = 0;
                    }
                }
                else
                {
                    a.Duracao -= dt;
                    a.Tique -= dt;
                    if (a.Tique <= 0 && a.Duracao > 0)
                    {
                        a.Tique = 0.5f;
                        foreach (Entidade alvo in HostisDaFaccao(a.Faccao, a.Pos, a.Raio).ToList())
                        {
                            if (a.Poder.Fonte != FonteDeDano.Nenhuma || a.Poder.DanoFixoMax > 0)
                                Atingir(dono, alvo, a.Poder, 0.25f, a.Pos);
                            AplicarEfeitoDoPoder(alvo, a.Poder, a.Dono);
                        }
                    }
                    if (a.Duracao <= 0) a.Viva = false;
                }
                if (!a.Viva)
                {
                    Areas.RemoveAt(i);
                    Despachante.Despachar(new NotAreaFim(a));
                }
            }
        }

        private void Explodir(AreaDeEfeito a, Entidade dono)
        {
            a.Explodiu = true;
            a.Tique = 0.5f;
            Despachante.Despachar(new NotAreaExplodiu(a));
            if (a.Raio >= 2.5f) Despachante.Despachar(new NotTremor(0.45f));
            foreach (Entidade alvo in HostisDaFaccao(a.Faccao, a.Pos, a.Raio).ToList())
            {
                if (!Grade.LinhaDeVisao(a.Pos, alvo.Pos)) continue;
                if (a.Poder.Fonte != FonteDeDano.Nenhuma || a.Poder.DanoFixoMax > 0) Atingir(dono, alvo, a.Poder, 1f, a.Pos);
                AplicarEfeitoDoPoder(alvo, a.Poder, a.Dono);
            }
            if (a.Duracao <= 0) a.Viva = false;
        }

        // ------------------------------------------------------------------
        // Itens no chao
        // ------------------------------------------------------------------

        public ItemNoChao SoltarItem(Vector2 perto, Item item, int sucata = 0)
        {
            // Espalha num anel para os itens de um mesmo bicho nao caírem empilhados.
            Vector2 pos = perto;
            for (int tentativa = 0; tentativa < 8; tentativa++)
            {
                float ang = (float)(Rng.NextDouble() * Math.PI * 2);
                float r = 0.5f + (float)Rng.NextDouble() * 1.1f;
                Vector2 p = perto + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
                if (Grade.FolgaEm(p) >= 0.3f && Grade.LinhaDeVisao(perto, p))
                {
                    pos = p;
                    break;
                }
            }
            var l = new ItemNoChao { Id = NovoId(), Pos = pos, Item = item, Sucata = sucata };
            Loot.Add(l);
            Despachante.Despachar(new NotLootCriado(l));
            return l;
        }

        public void RemoverLoot(ItemNoChao l, bool pego)
        {
            if (Loot.Remove(l)) Despachante.Despachar(new NotLootRemovido(l, pego));
        }

        /// <summary>Sucata no chao vem sozinha quando o jogador passa perto, como o ouro no Flare.</summary>
        public event Action<ItemNoChao> SucataColetada;

        private void AtualizarLoot(float dt)
        {
            for (int i = Loot.Count - 1; i >= 0; i--)
            {
                ItemNoChao l = Loot[i];
                l.Idade += dt;
                if (l.Item == null && Jogador != null && Jogador.Vivo && l.Idade > 0.35f
                    && Vector2.Distance(l.Pos, Jogador.Pos) <= RaioDeSucataAutomatica)
                {
                    Loot.RemoveAt(i);
                    Despachante.Despachar(new NotLootRemovido(l, true));
                    SucataColetada?.Invoke(l);
                }
            }
        }
    }

    public static class ExtensoesDeEntidade
    {
        public static bool Esquivando(this Entidade e) => e.Acao == EstadoAcao.Esquivando;
    }
}
