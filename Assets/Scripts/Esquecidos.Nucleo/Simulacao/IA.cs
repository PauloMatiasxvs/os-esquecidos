using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Regras;

namespace Esquecidos.Nucleo.Simulacao
{
    /// <summary>
    /// Cerebro de uma entidade. Pensa e da ordens; quem anda, mira e bate e a
    /// simulacao, igual para o jogador e para os bichos.
    /// </summary>
    public abstract class IA
    {
        public abstract void Pensar(Mundo m, Entidade e, float dt);

        public virtual void AoSerAtingido(Mundo m, Entidade e, Entidade atacante)
        {
        }

        public static IA Para(InimigoDef def)
        {
            switch (def.Ia)
            {
                case "corpo": return new IAComum();
                case "enxame": return new IAComum { Enxame = true };
                case "distancia": return new IAComum { Distancia = true, DistanciaPreferida = 7.5f };
                case "drone": return new IAComum { Distancia = true, DistanciaPreferida = 8.5f, Alarme = true };
                case "chefe": return new IAChefe();
                case "chefe_distancia": return new IAChefe { Distancia = true, DistanciaPreferida = 9f };
                case "aliado": return new IAAliada();
                case "parado": return null;
            }
            throw new FormatException($"inimigo '{def.Id}': ia desconhecida '{def.Ia}'");
        }
    }

    public class IAComum : IA
    {
        public bool Distancia;
        public bool Enxame;
        public bool Alarme;
        public float DistanciaPreferida = 7f;

        protected int Alvo;
        private float _perceberEm;
        private float _vagarEm;
        private float _atacarEm;
        private float _semVer;
        private Vector2 _ultimaVista;
        private bool _voltando;

        public bool Acordado => Alvo != 0;

        protected virtual float Visao(Mundo m, Entidade e)
        {
            float v = e.Def?.Visao ?? 10f;
            return m.Noite ? v * 1.4f : v;
        }

        public override void Pensar(Mundo m, Entidade e, float dt)
        {
            _perceberEm -= dt;
            _atacarEm -= dt;
            if (_perceberEm <= 0)
            {
                _perceberEm = 0.2f + (float)m.Rng.NextDouble() * 0.15f;
                Perceber(m, e);
            }

            Entidade alvo = m.Buscar(Alvo);
            if (alvo == null || !alvo.Vivo || !e.HostilA(alvo))
            {
                Alvo = 0;
                Vagar(m, e, dt);
                return;
            }

            float distOrigem = Vector2.Distance(e.Pos, e.Origem);
            float dist = Vector2.Distance(e.Pos, alvo.Pos);
            float perseguicao = (e.Def?.Perseguicao ?? 20f) * (m.Noite ? 1.5f : 1f);
            bool ve = m.Grade.LinhaDeVisao(e.Pos, alvo.Pos);
            if (ve)
            {
                _semVer = 0;
                _ultimaVista = alvo.Pos;
            }
            else
            {
                _semVer += dt;
            }
            if ((distOrigem > perseguicao && dist > Visao(m, e)) || _semVer > 7f)
            {
                // Desiste e volta para casa, curando no caminho como no Flare.
                Alvo = 0;
                _voltando = true;
                e.LimparOrdem();
                return;
            }

            e.MovimentoDireto = Vector2.Zero;
            if (e.Ocupado) return;

            if (!ve)
            {
                DarOrdemDeMover(m, e, _ultimaVista);
                return;
            }

            if (Distancia && dist < DistanciaPreferida * 0.55f && !e.Chefe)
            {
                // Recua mantendo o alvo a vista: o atirador nao deixa encostar.
                Vector2 fuga = Vector2.Normalize(e.Pos - alvo.Pos);
                Vector2 lado = new Vector2(-fuga.Y, fuga.X) * ((e.Id % 2 == 0) ? 0.5f : -0.5f);
                Vector2 dir = Vector2.Normalize(fuga + lado);
                if (m.Grade.CaminhoLivre(e.Pos, e.Pos + dir * 1.5f, e.Raio))
                {
                    e.LimparOrdem();
                    e.MovimentoDireto = dir;
                    e.OlharPara(alvo.Pos);
                    return;
                }
            }

            if (_atacarEm > 0)
            {
                // Entre um golpe e outro o bicho nao fica parado olhando: rodeia.
                if (Enxame || Distancia) Rodear(m, e, alvo);
                else if (dist > m.AlcanceEfetivo(e, PoderPrincipal(m, e)) + alvo.Raio) DarOrdemDeMover(m, e, alvo.Pos);
                else e.OlharPara(alvo.Pos);
                return;
            }

            Poder poder = EscolherPoder(m, e, dist - alvo.Raio);
            if (poder == null)
            {
                Poder principal = PoderPrincipal(m, e);
                if (principal == null) return;
                if (Distancia)
                {
                    if (dist > principal.Alcance * 0.9f) DarOrdemDeMover(m, e, alvo.Pos);
                    else Rodear(m, e, alvo);
                }
                else if (Enxame && dist > 3.5f)
                {
                    DarOrdemDeMover(m, e, PontoDeCerco(e, alvo));
                }
                else if (e.Recarga(principal.Id) <= 0)
                {
                    DarOrdemDeAtacar(e, alvo, principal);
                }
                else
                {
                    DarOrdemDeMover(m, e, alvo.Pos);
                }
                return;
            }
            DarOrdemDeAtacar(e, alvo, poder);
            _atacarEm = (Distancia ? 0.5f : 0.35f) + (float)m.Rng.NextDouble() * 0.6f;
            if (m.Noite) _atacarEm *= 0.7f;
        }

        private void Perceber(Mundo m, Entidade e)
        {
            Entidade atual = m.Buscar(Alvo);
            if (atual != null && atual.Vivo) return;
            Entidade agressor = m.Buscar(e.UltimoAgressor);
            if (agressor != null && agressor.Vivo && e.HostilA(agressor) && e.TempoDesdeDano < 5)
            {
                Acordar(m, e, agressor);
                return;
            }
            Entidade visto = m.HostilMaisProximo(e, Visao(m, e), true);
            // Barulho: colado demais, sente mesmo sem ver.
            visto ??= m.HostilMaisProximo(e, 2.5f, false);
            if (visto != null) Acordar(m, e, visto);
        }

        public void Acordar(Mundo m, Entidade e, Entidade alvo)
        {
            bool novo = Alvo == 0;
            Alvo = alvo.Id;
            _voltando = false;
            _semVer = 0;
            _ultimaVista = alvo.Pos;
            if (!novo) return;
            m.Despachante.Despachar(new NotSom(e.Def?.Voz + "_alerta", e.Pos));
            // O bando acorda junto. A sentinela grita mais longe.
            float raio = Alarme ? 18f : 8f;
            foreach (Entidade outro in m.Entidades)
            {
                if (outro == e || !outro.Vivo || outro.Faccao != e.Faccao) continue;
                if (Vector2.Distance(outro.Pos, e.Pos) > raio) continue;
                if (outro.Cerebro is IAComum ia && ia.Alvo == 0) ia.AcordarSemEco(alvo);
            }
        }

        private void AcordarSemEco(Entidade alvo)
        {
            Alvo = alvo.Id;
            _voltando = false;
            _ultimaVista = alvo.Pos;
        }

        public override void AoSerAtingido(Mundo m, Entidade e, Entidade atacante)
        {
            if (atacante == null || !atacante.Vivo) return;
            Entidade atual = m.Buscar(Alvo);
            // Provocacao e golpe forte trocam o alvo; o resto mantem o foco.
            if (atual == null || !atual.Vivo || e.TemEfeito("provocado") || m.Rng.NextDouble() < 0.3)
                Acordar(m, e, atacante);
        }

        protected Poder PoderPrincipal(Mundo m, Entidade e)
        {
            if (e.Def == null || e.Def.Poderes.Count == 0) return null;
            return m.Catalogo.Poder(e.Def.Poderes[0].Poder);
        }

        protected Poder EscolherPoder(Mundo m, Entidade e, float dist)
        {
            if (e.Def == null) return null;
            int total = 0;
            var candidatos = new List<(Poder, int)>();
            foreach (UsoDePoderDoInimigo u in e.Def.Poderes)
            {
                Poder p = m.Catalogo.Poder(u.Poder);
                if (dist < u.AlcanceMin || dist > u.AlcanceMax + e.Raio) continue;
                if (m.MotivoParaNaoUsar(e, p) != null) continue;
                if (p.Tipo == TipoPoder.Invocacao && m.Entidades.Count(x => x.Vivo && x.Dono == e.Id) >= p.Quantidade) continue;
                candidatos.Add((p, u.Peso));
                total += u.Peso;
            }
            if (total == 0) return null;
            int sorteio = m.Rng.Next(total);
            foreach ((Poder p, int peso) in candidatos)
            {
                if (sorteio < peso) return p;
                sorteio -= peso;
            }
            return candidatos[0].Item1;
        }

        protected static void DarOrdemDeAtacar(Entidade e, Entidade alvo, Poder p)
        {
            if (e.Ordem.Tipo == TipoOrdem.Atacar && e.Ordem.AlvoId == alvo.Id && e.Ordem.Poder == p) return;
            e.Ordem.Tipo = TipoOrdem.Atacar;
            e.Ordem.AlvoId = alvo.Id;
            e.Ordem.Poder = p;
            e.Ordem.Repetir = false;
        }

        protected static void DarOrdemDeMover(Mundo m, Entidade e, Vector2 ponto)
        {
            if (e.Ordem.Tipo == TipoOrdem.Mover && Vector2.Distance(e.Ordem.Ponto, ponto) < 0.8f) return;
            e.Ordem.Tipo = TipoOrdem.Mover;
            e.Ordem.Ponto = ponto;
        }

        /// <summary>Cada bicho do bando pega um lado diferente do alvo, pelo id.</summary>
        private static Vector2 PontoDeCerco(Entidade e, Entidade alvo)
        {
            float ang = (e.Id * 2.399963f) % (MathF.PI * 2);
            return alvo.Pos + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 2.2f;
        }

        private void Rodear(Mundo m, Entidade e, Entidade alvo)
        {
            Vector2 d = e.Pos - alvo.Pos;
            if (d.LengthSquared() < 1e-4f) return;
            float ang = MathF.Atan2(d.Y, d.X) + ((e.Id % 2 == 0) ? 0.5f : -0.5f);
            float raio = Distancia ? DistanciaPreferida : 2.6f;
            Vector2 ponto = alvo.Pos + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * raio;
            if (m.Grade.FolgaEm(ponto) < e.Raio) return;
            DarOrdemDeMover(m, e, ponto);
            e.OlharPara(alvo.Pos);
        }

        private void Vagar(Mundo m, Entidade e, float dt)
        {
            if (_voltando)
            {
                e.Vida = MathF.Min(e.VidaMax, e.Vida + e.VidaMax * 0.25f * dt);
                if (Vector2.Distance(e.Pos, e.Origem) < 1.2f) _voltando = false;
                else
                {
                    DarOrdemDeMover(m, e, e.Origem);
                    return;
                }
            }
            _vagarEm -= dt;
            if (_vagarEm > 0) return;
            _vagarEm = 2.5f + (float)m.Rng.NextDouble() * 4f;
            if (m.Rng.NextDouble() < 0.35) return;
            float ang = (float)(m.Rng.NextDouble() * Math.PI * 2);
            float r = 1.5f + (float)m.Rng.NextDouble() * 3.5f;
            Vector2 p = e.Origem + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
            if (m.Grade.CaminhoLivre(e.Pos, p, e.Raio)) DarOrdemDeMover(m, e, p);
        }
    }

    /// <summary>
    /// Chefe: luta como o bicho comum, mas troca de fase em 66% e 33% da vida.
    /// Cada fase chama reforcos (o poder de invocacao dele) e acelera.
    /// </summary>
    public sealed class IAChefe : IAComum
    {
        public int Fase { get; private set; } = 1;

        public override void Pensar(Mundo m, Entidade e, float dt)
        {
            float pct = e.Vida / e.VidaMax;
            int fase = pct > 0.66f ? 1 : pct > 0.33f ? 2 : 3;
            if (fase > Fase)
            {
                Fase = fase;
                m.Despachante.Despachar(new NotTexto(e.Pos, Fase == 3 ? "FÚRIA" : "A fera se enfurece", "#ff5040"));
                m.Despachante.Despachar(new NotSom(e.Def?.Voz + "_rugido", e.Pos));
                m.Despachante.Despachar(new NotTremor(0.8f));
                m.AplicarEfeito(e, "rapidez", 9999, Fase == 3 ? 35 : 15, e.Id);
                m.AplicarEfeito(e, "furia", 9999, Fase == 3 ? 30 : 10, e.Id);
                foreach (UsoDePoderDoInimigo u in e.Def.Poderes)
                {
                    Poder p = m.Catalogo.Poder(u.Poder);
                    if (p.Tipo != TipoPoder.Invocacao) continue;
                    e.Recargas.Remove(p.Id);
                    if (!e.Ocupado) m.Iniciar(e, p, e.Pos + e.Frente, 0);
                }
            }
            base.Pensar(m, e, dt);
        }
    }

    /// <summary>Companheiro e invocacao do jogador: segue o dono e bate em quem chega perto dele.</summary>
    public sealed class IAAliada : IA
    {
        private float _pensarEm;

        public override void Pensar(Mundo m, Entidade e, float dt)
        {
            _pensarEm -= dt;
            if (_pensarEm > 0) return;
            _pensarEm = 0.25f;
            Entidade dono = m.Buscar(e.Dono) ?? m.Jogador;
            if (dono == null) return;
            if (e.Ocupado) return;
            Entidade inimigo = null;
            float melhor = 9f;
            foreach (Entidade x in m.Hostis(e, dono.Pos, 9f))
            {
                float d = Vector2.Distance(x.Pos, e.Pos);
                if (d < melhor && m.Grade.LinhaDeVisao(e.Pos, x.Pos))
                {
                    melhor = d;
                    inimigo = x;
                }
            }
            float distDono = Vector2.Distance(e.Pos, dono.Pos);
            if (inimigo != null && distDono < 12f && e.Def != null && e.Def.Poderes.Count > 0)
            {
                Poder p = m.Catalogo.Poder(e.Def.Poderes[0].Poder);
                foreach (UsoDePoderDoInimigo u in e.Def.Poderes)
                {
                    Poder c = m.Catalogo.Poder(u.Poder);
                    if (m.MotivoParaNaoUsar(e, c) == null && melhor <= u.AlcanceMax + e.Raio)
                    {
                        p = c;
                        break;
                    }
                }
                e.Ordem.Tipo = TipoOrdem.Atacar;
                e.Ordem.AlvoId = inimigo.Id;
                e.Ordem.Poder = p;
                return;
            }
            if (distDono > 3.2f)
            {
                Vector2 atras = dono.Pos - dono.Frente * 1.8f;
                e.Ordem.Tipo = TipoOrdem.Mover;
                e.Ordem.Ponto = atras;
            }
            else if (e.Ordem.Tipo == TipoOrdem.Mover)
            {
                e.LimparOrdem();
            }
        }
    }

    /// <summary>Gente do Patio: fica no posto, olha para quem chega perto, as vezes da uns passos.</summary>
    public sealed class IANpc : IA
    {
        private float _pensarEm;
        public float Raio = 1.5f;

        public override void Pensar(Mundo m, Entidade e, float dt)
        {
            _pensarEm -= dt;
            if (_pensarEm > 0) return;
            _pensarEm = 0.5f;
            Entidade j = m.Jogador;
            if (j != null && Vector2.Distance(j.Pos, e.Pos) < 4.5f)
            {
                e.LimparOrdem();
                e.OlharPara(j.Pos);
                return;
            }
            if (m.Rng.NextDouble() < 0.06)
            {
                float ang = (float)(m.Rng.NextDouble() * Math.PI * 2);
                Vector2 p = e.Origem + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * Raio * (float)m.Rng.NextDouble();
                if (m.Grade.CaminhoLivre(e.Pos, p, e.Raio))
                {
                    e.Ordem.Tipo = TipoOrdem.Mover;
                    e.Ordem.Ponto = p;
                }
            }
        }
    }
}
