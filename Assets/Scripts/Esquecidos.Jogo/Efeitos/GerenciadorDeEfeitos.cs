using System.Collections.Generic;
using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Visual;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using static Esquecidos.Nucleo.Idioma;

namespace Esquecidos.Jogo.Efeitos
{
    /// <summary>
    /// Transforma o que a simulacao conta em coisa vista: faisca, gota, numero
    /// de dano, rastro de golpe, aviso de bote no chao, projetil voando,
    /// explosao, poca de acido, veneno borbulhando no corpo.
    /// </summary>
    public sealed class GerenciadorDeEfeitos :
        IExecutor<NotDano>,
        IExecutor<NotCura>,
        IExecutor<NotMorte>,
        IExecutor<NotAtaqueIniciado>,
        IExecutor<NotImpacto>,
        IExecutor<NotProjetilCriado>,
        IExecutor<NotProjetilFim>,
        IExecutor<NotAreaCriada>,
        IExecutor<NotAreaExplodiu>,
        IExecutor<NotAreaFim>,
        IExecutor<NotEfeito>,
        IExecutor<NotTexto>,
        IExecutor<NotTremor>,
        IExecutor<NotPoderNegado>
    {
        private readonly Principal _p;
        private readonly Dictionary<Projetil, Transform> _projeteis = new Dictionary<Projetil, Transform>();
        private readonly Dictionary<int, GameObject> _areas = new Dictionary<int, GameObject>();
        private readonly Dictionary<(int, string), GameObject> _estados = new Dictionary<(int, string), GameObject>();
        public Transform Raiz;

        public GerenciadorDeEfeitos(Principal p)
        {
            _p = p;
        }

        public void Limpar(Transform pai)
        {
            if (Raiz != null) Object.Destroy(Raiz.gameObject);
            _projeteis.Clear();
            _areas.Clear();
            _estados.Clear();
            Raiz = new GameObject("Efeitos").transform;
            Raiz.SetParent(pai, false);
        }

        private VisualDeEntidade V(Entidade e) => _p.Visuais.De(e);

        private static Color CorDoSangue(VisualDeEntidade v)
        {
            switch (v)
            {
                case Tecelao _: return new Color(0.45f, 0.55f, 0.32f, 0.95f);
                case Humanoide h when h.Dados.Tipo == "cinzento" || h.Dados.Tipo == "bruto": return new Color(0.25f, 0.27f, 0.25f, 0.95f);
                case Humanoide _: return new Color(0.55f, 0.06f, 0.05f, 0.95f);
                default: return new Color(0.6f, 0.55f, 0.45f, 0.8f);
            }
        }

        private static bool Metalico(VisualDeEntidade v) => v is Drone || v is Mech;

        public void Executar(NotDano n)
        {
            VisualDeEntidade v = V(n.Alvo);
            if (v == null) return;
            Vector3 pos = v.PosicaoVisual + Vector3.up * v.AlturaDaCabeca * v.Escala * 0.55f;
            bool jogador = n.Alvo == _p.Partida.Jogador;
            if (n.Esquivou)
            {
                _p.UI.Flutuante(pos + Vector3.up * 0.4f, n.Valor <= 0 && n.Fonte != null && !jogador ? T("errou") : T("esquivou"), new Color(0.75f, 0.78f, 0.8f), 0.8f);
                return;
            }
            Vector3 de = n.Fonte != null && V(n.Fonte) != null ? V(n.Fonte).PosicaoVisual : pos - Vector3.forward;
            Vector3 dir = pos - de;
            dir.y = 0;
            bool continuo = n.Elemento == "veneno" && n.Fonte == null || n.Elemento == "sangramento" || n.Elemento == "queimadura";
            if (!continuo)
            {
                v.Golpeado(n.Valor, n.Critico, de, n.Elemento);
                if (v is Boneco) Particulas.Poeira(pos, 6, 0.3f, new Color(0.8f, 0.7f, 0.45f, 0.8f));
                else if (Metalico(v))
                {
                    Particulas.Faiscas(pos, dir, n.Critico ? 22 : 12, new Color(1f, 0.7f, 0.3f));
                    if (n.Critico) Particulas.Estilhacos(pos, 4, new Color(0.35f, 0.35f, 0.38f));
                }
                else
                {
                    Particulas.Gotas(pos, dir, n.Critico ? 16 : 8, CorDoSangue(v));
                    Particulas.Faiscas(pos, dir, n.Critico ? 6 : 2, new Color(1f, 0.9f, 0.7f), 0.6f);
                }
                if (n.Bloqueou)
                {
                    Particulas.Faiscas(pos, -dir, 10, new Color(0.9f, 0.95f, 1f));
                    _p.UI.Flutuante(pos + Vector3.up * 0.5f, T("bloqueou"), new Color(0.7f, 0.85f, 1f), 0.8f);
                }
                switch (n.Elemento)
                {
                    case "choque": Particulas.Faiscas(pos, Vector3.up, 10, new Color(0.5f, 0.75f, 1f), 0.8f); break;
                    case "fogo": Particulas.Fogo(pos, 6, 0.3f, new Color(1f, 0.55f, 0.2f)); break;
                    case "veneno": Particulas.Brilhos(pos, 6, new Color(0.5f, 1f, 0.3f), 0.3f); break;
                }
            }
            Color cor;
            float tamanho = 1f;
            if (jogador) cor = new Color(1f, 0.35f, 0.3f);
            else if (n.Elemento == "veneno") cor = new Color(0.55f, 1f, 0.35f);
            else if (n.Elemento == "sangramento") cor = new Color(1f, 0.45f, 0.4f);
            else if (n.Elemento == "queimadura" || n.Elemento == "fogo") cor = new Color(1f, 0.65f, 0.25f);
            else if (n.Elemento == "choque") cor = new Color(0.55f, 0.8f, 1f);
            else cor = new Color(1f, 0.97f, 0.9f);
            if (n.Critico)
            {
                cor = new Color(1f, 0.82f, 0.25f);
                tamanho = 1.55f;
            }
            if (continuo) tamanho = 0.75f;
            _p.UI.Flutuante(pos, Mathf.RoundToInt(n.Valor).ToString(), cor, tamanho, n.Critico);
            if (jogador)
            {
                _p.Ambiente.PulsoDeDano(Mathf.Clamp01(n.Valor / Mathf.Max(1, n.Alvo.VidaMax) * 4));
                if (!continuo) _p.Camera.Tremer(0.12f + n.Valor / Mathf.Max(1, n.Alvo.VidaMax));
            }
            else if (n.Fonte == _p.Partida.Jogador && n.Critico)
            {
                _p.Congelar(0.055f);
                _p.Camera.Tremer(0.22f);
            }
        }

        public void Executar(NotCura n)
        {
            VisualDeEntidade v = V(n.Alvo);
            if (v == null) return;
            Vector3 pos = v.PosicaoVisual + Vector3.up * v.AlturaDaCabeca * v.Escala * 0.6f;
            _p.UI.Flutuante(pos, "+" + Mathf.RoundToInt(n.Valor), new Color(0.45f, 1f, 0.5f), 0.9f);
            Particulas.Brilhos(v.PosicaoVisual + Vector3.up, 10, new Color(0.4f, 1f, 0.5f), 0.5f, 1.4f);
        }

        public void Executar(NotMorte n)
        {
            VisualDeEntidade v = V(n.Morto);
            if (v == null) return;
            Vector3 pos = v.PosicaoVisual + Vector3.up * 0.6f * v.Escala;
            if (Metalico(v))
            {
                Particulas.Explosao(pos, 1.6f * v.Escala, new Color(1f, 0.55f, 0.2f));
                Particulas.Estilhacos(pos, 10, new Color(0.3f, 0.3f, 0.33f), 1.2f);
            }
            else if (v is Boneco)
            {
                Particulas.Poeira(pos, 16, 0.6f, new Color(0.8f, 0.7f, 0.45f, 0.8f));
            }
            else
            {
                Particulas.Gotas(pos, Vector3.up, 14, CorDoSangue(v));
                Particulas.Mancha(pos, 0.8f * v.Escala, CorDoSangue(v) * 0.7f, 14);
            }
            if (n.Morto.Chefe)
            {
                Particulas.Explosao(pos, 4, new Color(1f, 0.7f, 0.3f));
                _p.Camera.Tremer(1f);
                _p.CameraLenta(1.2f);
            }
        }

        public void Executar(NotAtaqueIniciado n)
        {
            VisualDeEntidade v = V(n.Entidade);
            if (v == null) return;
            v.AoAtacar(n.Poder);
            Poder p = n.Poder;
            // Aviso no chao: so para bicho, e so o que da tempo de rolar.
            if (n.Entidade.Faccao == Faccao.Inimigo && p.Antecipacao >= 0.4f)
            {
                switch (p.Tipo)
                {
                    case TipoPoder.Area:
                        break;
                    case TipoPoder.Salto:
                        Indicador.Circulo(Raiz, Util.Mundo(n.Ponto), Mathf.Max(1.5f, p.Raio), n.Duracao * 0.55f + 0.55f);
                        break;
                    case TipoPoder.Grito:
                        Indicador.Circulo(Raiz, v.PosicaoVisual, Mathf.Max(1.5f, p.Raio), p.Antecipacao);
                        break;
                    case TipoPoder.Investida:
                        Indicador.Linha(Raiz, v.PosicaoVisual, Util.Mundo(n.Ponto), p.Distancia > 0 ? p.Distancia : p.Alcance, 1.2f * v.Escala, p.Antecipacao);
                        break;
                    case TipoPoder.Corpo:
                        if (p.Angulo >= 360) Indicador.Circulo(Raiz, v.PosicaoVisual, p.Alcance + n.Entidade.Raio, p.Antecipacao);
                        else if (p.Antecipacao >= 0.55f) Indicador.Cone(Raiz, v.transform, p.Alcance + n.Entidade.Raio, p.Angulo, p.Antecipacao);
                        break;
                }
            }
            if (p.Tipo == TipoPoder.Esquiva) Particulas.Poeira(v.PosicaoVisual, 6, 0.4f);
            if (p.Tipo == TipoPoder.Cura || p.Tipo == TipoPoder.Reforco)
                Particulas.Brilhos(v.PosicaoVisual + Vector3.up, 12, Util.Cor(p.Cor, Color.white), 0.6f, 1.2f);
        }

        public void Executar(NotImpacto n)
        {
            VisualDeEntidade v = V(n.Entidade);
            if (v == null) return;
            v.AoImpacto(n.Poder);
            Poder p = n.Poder;
            Color cor = Util.Cor(p.Cor, Color.white);
            switch (p.Tipo)
            {
                case TipoPoder.Corpo:
                    Rastro.Criar(Raiz, v, _p.Partida.Mundo.AlcanceEfetivo(n.Entidade, p), p.Angulo, cor, p.Vfx);
                    if (p.Vfx == "pancada" || p.Vfx == "giro") Particulas.Poeira(v.PosicaoVisual + v.transform.forward * 1.2f, 5, 0.6f);
                    break;
                case TipoPoder.Salto:
                case TipoPoder.Investida:
                    if (p.Tipo == TipoPoder.Salto || p.Raio > 0)
                    {
                        Particulas.Anel(v.PosicaoVisual, Mathf.Max(2f, p.Raio), new Color(1f, 0.85f, 0.6f), 0.5f);
                        Particulas.Poeira(v.PosicaoVisual, 18, Mathf.Max(1f, p.Raio * 0.5f));
                        Particulas.Estilhacos(v.PosicaoVisual, 6, new Color(0.45f, 0.43f, 0.4f));
                    }
                    break;
                case TipoPoder.Grito:
                    Particulas.Anel(v.PosicaoVisual, p.Raio, p.Vfx == "terremoto" ? new Color(1f, 0.7f, 0.4f) : cor, 0.6f);
                    if (p.Vfx == "terremoto")
                    {
                        Particulas.Poeira(v.PosicaoVisual, 24, p.Raio * 0.6f);
                        Particulas.Estilhacos(v.PosicaoVisual, 10, new Color(0.45f, 0.43f, 0.4f), 1.3f);
                    }
                    break;
                case TipoPoder.Projetil:
                    Particulas.Brilhos(v.PosicaoVisual + Vector3.up * 1.2f + v.transform.forward * 0.6f, 4, cor, 0.15f, 0.5f);
                    break;
                case TipoPoder.Invocacao:
                    Particulas.Anel(v.PosicaoVisual, 2.5f, new Color(0.6f, 0.9f, 1f), 0.6f);
                    Particulas.Brilhos(v.PosicaoVisual + Vector3.up, 18, new Color(0.6f, 0.9f, 1f), 1f, 1.5f);
                    break;
            }
        }

        // ------------------------------------------------------------------ projeteis

        public void Executar(NotProjetilCriado n)
        {
            Transform t = Projeteis.Criar(Raiz, n.Projetil);
            _projeteis[n.Projetil] = t;
        }

        public void Executar(NotProjetilFim n)
        {
            if (_projeteis.TryGetValue(n.Projetil, out Transform t))
            {
                _projeteis.Remove(n.Projetil);
                if (t != null)
                {
                    TrailRenderer tr = t.GetComponentInChildren<TrailRenderer>();
                    if (tr != null)
                    {
                        tr.transform.SetParent(Raiz, true);
                        tr.emitting = false;
                        Object.Destroy(tr.gameObject, tr.time + 0.1f);
                    }
                    Object.Destroy(t.gameObject);
                }
            }
            Vector3 pos = Util.Mundo(n.Ponto, n.Projetil.Altura);
            Color cor = Util.Cor(n.Projetil.Poder.Cor, Color.white);
            switch (n.Projetil.Poder.Vfx)
            {
                case "acido":
                    Particulas.Gotas(pos, Vector3.up, 10, new Color(0.5f, 0.9f, 0.25f, 0.9f));
                    break;
                case "laser":
                case "choque":
                case "raio":
                    Particulas.Faiscas(pos, Vector3.up, 10, cor, 0.8f);
                    Particulas.Brilhos(pos, 5, cor, 0.2f);
                    break;
                default:
                    Particulas.Poeira(new Vector3(pos.x, 0, pos.z), 3, 0.2f);
                    Particulas.Faiscas(pos, Vector3.up, 4, new Color(1f, 0.85f, 0.6f), 0.5f);
                    break;
            }
        }

        public void Atualizar()
        {
            float alfa = VisualDeEntidade.Alfa;
            foreach (KeyValuePair<Projetil, Transform> kv in _projeteis)
            {
                if (kv.Value == null) continue;
                Projetil pr = kv.Key;
                Vector3 pos = Util.Mundo(System.Numerics.Vector2.Lerp(pr.PosAnterior, pr.Pos, alfa), pr.Altura);
                kv.Value.position = pos;
                Vector3 v = new Vector3(pr.Velocidade.X, 0, pr.Velocidade.Y);
                if (v.sqrMagnitude > 1e-4f) kv.Value.rotation = Quaternion.LookRotation(v);
            }
        }

        // ------------------------------------------------------------------ areas

        public void Executar(NotAreaCriada n)
        {
            AreaDeEfeito a = n.Area;
            Vector3 centro = Util.Mundo(a.Pos);
            var g = new GameObject("area");
            g.transform.SetParent(Raiz, false);
            g.transform.position = centro;
            bool inimigo = a.Faccao == Faccao.Inimigo;
            Poder p = a.Poder;
            if (a.Atraso > 0.05f && !a.Armadilha)
            {
                // Granada, missil, teia: aviso no chao ate estourar.
                Indicador.Circulo(g.transform, centro, a.Raio, a.Atraso, inimigo ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.8f, 0.4f));
                VisualDeEntidade dono = _p.Visuais.De(a.Dono);
                if (p.Vfx == "explosao" && dono != null && p.Animacao == "arremesso")
                    Lancado.Criar(g.transform, dono.PosicaoVisual + Vector3.up * 1.6f, centro, Mathf.Min(0.5f, a.Atraso));
                else if (p.Vfx == "explosao")
                    Lancado.Criar(g.transform, centro + Vector3.up * 25, centro, a.Atraso);
            }
            if (a.Armadilha)
            {
                var c = new ConstrutorDeMalha();
                c.Cor(new Color(0.4f, 0.4f, 0.42f)).Cilindro(Vector3.zero, 0.45f, 0.08f, 12);
                c.Cor(new Color(0.75f, 0.75f, 0.78f));
                for (int i = 0; i < 8; i++)
                {
                    float an = i / 8f * Mathf.PI * 2;
                    c.Tubo(new Vector3(Mathf.Cos(an) * 0.4f, 0.08f, Mathf.Sin(an) * 0.4f), new Vector3(Mathf.Cos(an) * 0.2f, 0.3f, Mathf.Sin(an) * 0.2f), 0.025f, 0.01f, 4);
                }
                c.Cor(new Color(1f, 0.6f, 0.2f), 0f).Esfera(new Vector3(0, 0.12f, 0), Vector3.one * 0.06f, 3, 4);
                Util.ComMalha(Util.Filho(g.transform, "armadilha"), c.Gerar("armadilha"), Paleta.Toon);
            }
            if (a.Atraso <= 0.05f && !a.Armadilha) Poca(g.transform, a);
            _areas[a.Id] = g;
        }

        /// <summary>O que fica no chao enquanto a area dura: acido borbulhando, fogo, teia.</summary>
        private void Poca(Transform pai, AreaDeEfeito a)
        {
            if (a.Duracao <= 0.05f) return;
            Poder p = a.Poder;
            switch (p.Vfx)
            {
                case "acido":
                    Particulas.Mancha(pai.position, a.Raio, new Color(0.35f, 0.75f, 0.15f, 0.85f), a.Duracao + 0.5f);
                    Particulas.Continuo(pai, new Vector3(0, 0.1f, 0), new Color(0.55f, 1f, 0.3f), 14 * a.Raio, a.Raio * 0.8f, 0.6f, 0.18f, true);
                    break;
                case "fogo":
                    Particulas.FogoContinuo(pai, new Vector3(0, 0.05f, 0), a.Raio * 0.7f, new Color(1f, 0.85f, 0.55f), 40 * a.Raio);
                    Particulas.Mancha(pai.position, a.Raio, new Color(0.05f, 0.04f, 0.03f, 0.8f), a.Duracao + 6);
                    var g = new GameObject("luz");
                    g.transform.SetParent(pai, false);
                    g.transform.localPosition = Vector3.up * 1.2f;
                    var l = g.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.75f, 0.45f);
                    l.intensity = 3;
                    l.range = a.Raio * 3;
                    break;
                case "teia":
                    Particulas.Mancha(pai.position, a.Raio, new Color(0.85f, 0.85f, 0.8f, 0.6f), a.Duracao + 0.5f);
                    break;
                case "explosao":
                    Particulas.Mancha(pai.position, a.Raio * 0.8f, new Color(0.08f, 0.06f, 0.05f, 0.8f), 10);
                    break;
            }
        }

        public void Executar(NotAreaExplodiu n)
        {
            AreaDeEfeito a = n.Area;
            Vector3 centro = Util.Mundo(a.Pos);
            switch (a.Poder.Vfx)
            {
                case "explosao":
                    Particulas.Explosao(centro, a.Raio, new Color(1f, 0.6f, 0.25f));
                    _p.Camera.Tremer(0.4f);
                    break;
                case "acido":
                    Particulas.Gotas(centro + Vector3.up * 0.3f, Vector3.up, 16, new Color(0.5f, 0.9f, 0.25f, 0.9f));
                    break;
                case "teia":
                    Particulas.Brilhos(centro, 12, new Color(0.9f, 0.9f, 0.85f), a.Raio * 0.6f, 0.2f);
                    break;
                case "armadilha":
                    Particulas.Faiscas(centro + Vector3.up * 0.2f, Vector3.up, 14, new Color(1f, 0.8f, 0.5f));
                    break;
                default:
                    Particulas.Anel(centro, a.Raio, Util.Cor(a.Poder.Cor, Color.white), 0.4f);
                    break;
            }
            if (_areas.TryGetValue(a.Id, out GameObject g) && g != null)
            {
                foreach (Indicador i in g.GetComponentsInChildren<Indicador>()) Object.Destroy(i.gameObject);
                Poca(g.transform, a);
                Transform armadilha = g.transform.Find("armadilha");
                if (armadilha != null) Object.Destroy(armadilha.gameObject);
            }
        }

        public void Executar(NotAreaFim n)
        {
            if (!_areas.TryGetValue(n.Area.Id, out GameObject g)) return;
            _areas.Remove(n.Area.Id);
            if (g == null) return;
            foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>()) ps.Stop();
            foreach (Light l in g.GetComponentsInChildren<Light>()) Object.Destroy(l);
            Object.Destroy(g, 1.5f);
        }

        // ------------------------------------------------------------------ estados

        public void Executar(NotEfeito n)
        {
            var chave = (n.Entidade.Id, n.Efeito);
            if (!n.Aplicado)
            {
                if (_estados.TryGetValue(chave, out GameObject g))
                {
                    _estados.Remove(chave);
                    if (g != null) Object.Destroy(g);
                }
                return;
            }
            VisualDeEntidade v = V(n.Entidade);
            if (v == null || _estados.ContainsKey(chave)) return;
            float altura = v.AlturaDaCabeca * v.Escala;
            GameObject ef = null;
            switch (n.Efeito)
            {
                case "veneno":
                    ef = Particulas.Continuo(v.transform, Vector3.up * altura * 0.5f, new Color(0.5f, 1f, 0.3f), 10, 0.4f, 0.8f, 0.12f, true).gameObject;
                    break;
                case "sangramento":
                    ef = Particulas.Continuo(v.transform, Vector3.up * altura * 0.55f, new Color(0.6f, 0.05f, 0.05f), 8, 0.3f, 0.1f, 0.1f, false, 1.2f).gameObject;
                    break;
                case "queimadura":
                    ef = Particulas.FogoContinuo(v.transform, Vector3.up * 0.2f, 0.35f, new Color(1f, 0.6f, 0.25f), 25).gameObject;
                    break;
                case "lento":
                    ef = Particulas.Continuo(v.transform, Vector3.up * 0.3f, new Color(0.5f, 0.75f, 1f), 6, 0.5f, 0.2f, 0.1f, true).gameObject;
                    break;
                case "furia":
                    ef = Particulas.Continuo(v.transform, Vector3.up * altura * 0.5f, new Color(1f, 0.3f, 0.15f), 12, 0.5f, 1.2f, 0.14f, true).gameObject;
                    break;
                case "pele":
                case "barreira":
                    ef = Particulas.Continuo(v.transform, Vector3.up * altura * 0.5f, new Color(0.75f, 0.85f, 1f), 10, 0.55f, 0.3f, 0.09f, true).gameObject;
                    break;
                case "regeneracao":
                    ef = Particulas.Continuo(v.transform, Vector3.up * altura * 0.4f, new Color(0.4f, 1f, 0.5f), 6, 0.4f, 1f, 0.12f, true).gameObject;
                    break;
                case "rapidez":
                    ef = Particulas.Continuo(v.transform, Vector3.up * altura * 0.3f, new Color(0.85f, 0.95f, 1f), 14, 0.4f, 0.4f, 0.07f, true).gameObject;
                    break;
                case "atordoado":
                    ef = Estrelas.Criar(v.transform, altura + 0.2f);
                    break;
                case "vulneravel":
                    ef = Particulas.Continuo(v.transform, Vector3.up * altura * 0.5f, new Color(1f, 0.85f, 0.2f), 5, 0.5f, 0.3f, 0.12f, true).gameObject;
                    break;
                case "enraizado":
                    ef = Particulas.Continuo(v.transform, Vector3.up * 0.1f, new Color(1f, 0.8f, 0.5f), 10, 0.5f, 0.05f, 0.08f, true).gameObject;
                    break;
            }
            if (ef != null) _estados[chave] = ef;
        }

        public void Executar(NotTexto n)
        {
            _p.UI.Flutuante(Util.Mundo(n.Ponto, 2.6f), n.Texto, Util.Cor(n.Cor, Color.white), 1.4f, true);
        }

        public void Executar(NotTremor n) => _p.Camera.Tremer(n.Forca);

        public void Executar(NotPoderNegado n)
        {
            VisualDeEntidade v = V(n.Entidade);
            if (v != null && n.Motivo != "atordoado") _p.UI.Flutuante(v.PosicaoVisual + Vector3.up * 2.2f, T(n.Motivo), new Color(0.6f, 0.8f, 1f), 0.75f);
        }
    }
}
