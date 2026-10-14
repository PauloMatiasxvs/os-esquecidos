using System.Collections.Generic;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;

namespace Esquecidos.Jogo.Atores
{
    /// <summary>
    /// O corpo visivel de uma entidade da simulacao. Le o estado dela a cada
    /// quadro (posicao interpolada, direcao, acao, fase do golpe) e anima. A
    /// simulacao nao sabe que este objeto existe, como os atores do Pal3 so
    /// leem o que o jogo decidiu.
    /// </summary>
    public abstract class VisualDeEntidade : MonoBehaviour
    {
        public Entidade E { get; private set; }
        public float AlturaDaCabeca = 1.9f;
        public float Escala = 1f;

        protected Transform Raiz;
        protected float Tempo;
        protected float Flash;
        protected float Cambaleio;
        protected Vector3 Empurrao;
        private float _yaw;
        private bool _primeiro = true;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _mpb;
        private float _some;
        private Color _corFlash = Color.white;

        /// <summary>Alfa de interpolacao entre o passo anterior e o atual da simulacao.</summary>
        public static float Alfa = 1f;

        public void Ligar(Entidade e)
        {
            E = e;
            Raiz = new GameObject("raiz").transform;
            Raiz.SetParent(transform, false);
            Construir();
            GetComponentsInChildren(true, _renderers);
            _mpb = new MaterialPropertyBlock();
            _yaw = e.Direcao;
            Posicionar(1f);
        }

        protected abstract void Construir();

        protected abstract void Animar(float dt);

        public Vector3 PosicaoVisual { get; private set; }

        private void Posicionar(float alfa)
        {
            Vector3 p = Util.Mundo(System.Numerics.Vector2.Lerp(E.PosAnterior, E.Pos, alfa));
            if (E.Acao == EstadoAcao.Saltando && E.DuracaoAcao > 0)
            {
                float t = Mathf.Clamp01(E.TempoAcao / E.DuracaoAcao);
                p.y += 4f * 1.8f * t * (1 - t);
            }
            if (E.Voa) p.y += AlturaDeVoo();
            PosicaoVisual = p;
            transform.position = p;
        }

        protected virtual float AlturaDeVoo() => 1.6f + Mathf.Sin(Tempo * 2.1f + E.Id) * 0.15f;

        protected virtual void LateUpdate()
        {
            if (E == null) return;
            float dt = Time.deltaTime;
            Tempo += dt;
            Posicionar(Alfa);
            // Gira o corpo para onde a simulacao manda, sem saltar.
            float alvo = E.Direcao;
            if (_primeiro)
            {
                _yaw = alvo;
                _primeiro = false;
            }
            float delta = Mathf.DeltaAngle(_yaw * Mathf.Rad2Deg, alvo * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            _yaw += delta * (1 - Mathf.Exp(-18f * dt));
            Raiz.localRotation = Util.Rotacao(_yaw);
            Raiz.localScale = Vector3.one * Escala;

            Flash = Mathf.Max(0, Flash - dt * 5f);
            Cambaleio = Mathf.Max(0, Cambaleio - dt * 4f);
            Empurrao = Vector3.Lerp(Empurrao, Vector3.zero, 1 - Mathf.Exp(-10 * dt));
            Animar(dt);

            float some = 0;
            if (!E.Vivo && E.Faccao != Faccao.Jogador) some = Mathf.Clamp01((E.TempoMorto - 1.6f) / 2f);
            if (some != _some || Flash > 0 || _flashAnterior > 0)
            {
                _some = some;
                foreach (Renderer r in _renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetFloat("_Flash", Flash * 0.8f);
                    _mpb.SetColor("_CorFlash", _corFlash);
                    _mpb.SetFloat("_Some", some);
                    r.SetPropertyBlock(_mpb);
                }
            }
            _flashAnterior = Flash;
        }

        private float _flashAnterior;

        /// <summary>Levou golpe: clarao, tranco para tras.</summary>
        public virtual void Golpeado(float dano, bool critico, Vector3 deOnde, string elemento)
        {
            Flash = critico ? 1f : 0.75f;
            _corFlash = elemento == "veneno" ? new Color(0.6f, 1f, 0.4f) : elemento == "choque" ? new Color(0.6f, 0.8f, 1f) : elemento == "fogo" ? new Color(1f, 0.6f, 0.3f) : Color.white;
            Cambaleio = 1f;
            Vector3 d = transform.position - deOnde;
            d.y = 0;
            if (d.sqrMagnitude > 1e-4f) Empurrao = d.normalized * 0.12f;
        }

        public virtual void AoAtacar(Poder p)
        {
        }

        public virtual void AoImpacto(Poder p)
        {
        }

        /// <summary>Fase da acao atual: preparacao (0..1) ou recuperacao (0..1).</summary>
        protected bool Preparando(out float t)
        {
            t = 0;
            if (E.Acao != EstadoAcao.Conjurando || E.DuracaoAcao <= 0) return false;
            t = Mathf.Clamp01(E.TempoAcao / E.DuracaoAcao);
            return true;
        }

        protected bool Recuperando(out float t)
        {
            t = 0;
            if (E.Acao != EstadoAcao.Recuperando || E.DuracaoAcao <= 0) return false;
            t = Mathf.Clamp01(E.TempoAcao / E.DuracaoAcao);
            return true;
        }

        protected string AnimacaoAtual => E.PoderAtual?.Animacao ?? "golpe";

        public Vector3 PontoDaCabeca => transform.position + Vector3.up * AlturaDaCabeca * Escala;

        /// <summary>Contorno mais grosso para destacar o alvo sob o mouse.</summary>
        public void Destacar(bool sim)
        {
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_Contorno", sim ? 3.2f : (E.Chefe ? 2.4f : 1.6f));
                _mpb.SetColor("_CorContorno", sim ? (E.Faccao == Faccao.Inimigo ? new Color(0.6f, 0.08f, 0.05f) : new Color(0.7f, 0.6f, 0.2f)) : new Color(0.06f, 0.05f, 0.05f));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
