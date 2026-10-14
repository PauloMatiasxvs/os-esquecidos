using Esquecidos.Jogo.Atores;
using Esquecidos.Jogo.Cena;
using Esquecidos.Motor.Entrada;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using V2 = System.Numerics.Vector2;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Traduz mouse, teclado e controle em pedidos a Partida. Clique no chao
    /// anda (segurando, continua andando atras do cursor), clique no bicho
    /// ataca, clique na gente conversa, clique no item pega, como no Flare.
    /// WASD anda direto, como no Pal3.
    /// </summary>
    public sealed class Controle
    {
        private readonly Principal _p;
        public VisualDeEntidade Mirado { get; private set; }
        public VisualDeLoot LootMirado { get; private set; }
        public VisualDeObjeto ObjetoMirado { get; private set; }
        public Vector3 PontoNoChao { get; private set; }
        public bool MostrarRotulos { get; private set; }
        private bool _segurandoChao;
        private bool _segurandoAtaque;
        private float _reenviar;

        public Controle(Principal p)
        {
            _p = p;
        }

        private Partida Partida => _p.Partida;

        public void Atualizar(float dt, bool interfaceAberta)
        {
            CameraDoJogo cam = _p.Camera;
            if (Controles.Desceu(Key.Q)) cam.Girar(-1);
            if (Controles.Desceu(Key.E)) cam.Girar(1);
            float roda = Controles.Roda;
            bool sobreUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Mathf.Abs(roda) > 0.01f && !sobreUi) cam.Aproximar(Mathf.Sign(roda));
            Gamepad gp = Controles.Controle;
            if (gp != null)
            {
                if (gp.leftShoulder.wasPressedThisFrame) cam.Girar(-1);
                if (gp.rightShoulder.wasPressedThisFrame) cam.Girar(1);
            }
            MostrarRotulos = Controles.Alt;

            if (cam.PontoNoChao(Controles.PosicaoDoMouse, out Vector3 chao)) PontoNoChao = chao;
            Mirar(sobreUi || interfaceAberta);

            if (interfaceAberta || Partida.EmCena || Partida.JogadorMorto)
            {
                Partida.MoverDireto(V2.Zero);
                _segurandoChao = _segurandoAtaque = false;
                return;
            }

            // WASD relativo a camera.
            Vector2 m = Controles.Movimento();
            Vector3 dir3 = cam.Frente * m.y + cam.Direita * m.x;
            Partida.MoverDireto(new V2(dir3.x, dir3.z));

            V2 ponto = Util.Plano(PontoNoChao);
            int alvo = Mirado != null && Mirado.E.Faccao == Faccao.Inimigo && Mirado.E.Vivo ? Mirado.E.Id : 0;

            if (Controles.CliqueEsquerdoDesceu && !sobreUi)
            {
                if (Mirado != null && Mirado.E.Faccao == Faccao.Inimigo && Mirado.E.Vivo)
                {
                    Partida.Atacar(Mirado.E.Id, true);
                    _segurandoAtaque = true;
                }
                else if (Mirado != null && Mirado.E.NpcId != null && Mirado.E.Faccao == Faccao.Neutro)
                {
                    Partida.FalarCom(Mirado.E.Id);
                }
                else if (LootMirado != null)
                {
                    Partida.PedirPegar(LootMirado.L.Id);
                }
                else if (ObjetoMirado != null)
                {
                    Partida.InteragirCom(ObjetoMirado.O.Id);
                }
                else if (Controles.Shift)
                {
                    Partida.AtacarNoLugar(ponto);
                }
                else
                {
                    Partida.MoverPara(ponto);
                    _segurandoChao = true;
                    _reenviar = 0.12f;
                    Efeitos.Particulas.Anel(PontoNoChao, 0.45f, new Color(1f, 0.85f, 0.5f), 0.35f);
                }
            }
            if (Controles.CliqueEsquerdoSegurado)
            {
                if (Controles.Shift && !sobreUi) Partida.AtacarNoLugar(ponto);
                else if (_segurandoChao)
                {
                    _reenviar -= dt;
                    if (_reenviar <= 0)
                    {
                        _reenviar = 0.12f;
                        Partida.MoverPara(ponto);
                    }
                }
            }
            if (Controles.CliqueEsquerdoSubiu)
            {
                _segurandoChao = false;
                if (_segurandoAtaque) Partida.PararDeRepetir();
                _segurandoAtaque = false;
            }

            if ((Controles.CliqueDireitoDesceu || (Controles.CliqueDireitoSegurado && Partida.Jogador != null && !Partida.Jogador.Ocupado)) && !sobreUi)
                Partida.UsarBarra(1, ponto, alvo);
            Key[] teclas = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
            for (int i = 0; i < teclas.Length; i++)
                if (Controles.Desceu(teclas[i])) Partida.UsarBarra(2 + i, ponto, alvo);
            if (Controles.Desceu(Key.Space))
            {
                V2 rumo = m.sqrMagnitude > 0.01f ? Util.Plano(Partida.Jogador != null ? Util.Mundo(Partida.Jogador.Pos) + dir3 : PontoNoChao) : ponto;
                Partida.Esquivar(rumo);
            }
            if (Controles.Desceu(Key.F)) Partida.InteragirComOMaisProximo();

            if (gp != null) Controle_(gp, dir3);
        }

        /// <summary>Controle de videogame: A ataca o mais perto na direcao do analogico, B rola, X/Y/gatilhos usam a barra.</summary>
        private void Controle_(Gamepad gp, Vector3 dir3)
        {
            Entidade j = Partida.Jogador;
            if (j == null) return;
            Vector2 mira = Controles.MiraDoControle();
            Vector3 frente = mira.sqrMagnitude > 0 ? _p.Camera.Frente * mira.y + _p.Camera.Direita * mira.x : (dir3.sqrMagnitude > 0.01f ? dir3 : new Vector3(j.Frente.X, 0, j.Frente.Y));
            V2 ponto = Util.Plano(Util.Mundo(j.Pos) + frente.normalized * 5f);
            Entidade perto = Partida.Mundo.HostilMaisProximo(j, 9f, true);
            if (gp.buttonSouth.wasPressedThisFrame)
            {
                if (perto != null) Partida.Atacar(perto.Id, false);
                else Partida.AtacarNoLugar(ponto);
            }
            if (gp.buttonEast.wasPressedThisFrame) Partida.Esquivar(ponto);
            if (gp.buttonWest.wasPressedThisFrame) Partida.UsarBarra(1, perto != null ? perto.Pos : ponto, perto?.Id ?? 0);
            if (gp.buttonNorth.wasPressedThisFrame) Partida.InteragirComOMaisProximo();
            if (gp.rightTrigger.wasPressedThisFrame) Partida.UsarBarra(2, perto != null ? perto.Pos : ponto, perto?.Id ?? 0);
            if (gp.leftTrigger.wasPressedThisFrame) Partida.UsarBarra(3, perto != null ? perto.Pos : ponto, perto?.Id ?? 0);
            if (gp.dpad.up.wasPressedThisFrame) Partida.UsarBarra(6, ponto, 0);
            if (gp.dpad.down.wasPressedThisFrame) Partida.UsarBarra(7, ponto, 0);
        }

        /// <summary>
        /// O que esta sob o cursor: bicho, gente, item, objeto. Por distancia
        /// na tela, nao por raio de fisica: o jogo nao tem colisores.
        /// </summary>
        private void Mirar(bool bloqueado)
        {
            VisualDeEntidade antes = Mirado;
            Mirado = null;
            LootMirado = null;
            ObjetoMirado = null;
            if (!bloqueado)
            {
                Camera cam = _p.Camera.Cam;
                Vector2 mouse = Controles.PosicaoDoMouse;
                float melhor = 1e9f;
                foreach (VisualDeEntidade v in _p.Visuais.Entidades)
                {
                    if (v == null || !v.E.Vivo || v.E == Partida.Jogador || v.E.Invocado && v.E.Faccao == Faccao.Jogador) continue;
                    if (v.E.Faccao == Faccao.Jogador && v.E.NpcId != null) continue;
                    Vector3 centro = v.PosicaoVisual + Vector3.up * v.AlturaDaCabeca * v.Escala * 0.5f;
                    Vector3 tela = cam.WorldToScreenPoint(centro);
                    if (tela.z < 0) continue;
                    float raio = Mathf.Max(28f, 900f * v.E.Raio * v.Escala / tela.z);
                    float d = Vector2.Distance(mouse, tela) / raio;
                    if (d < 1 && d < melhor)
                    {
                        melhor = d;
                        Mirado = v;
                    }
                }
                if (Mirado == null)
                {
                    float melhorLoot = 1e9f;
                    foreach (VisualDeLoot l in _p.Visuais.Loot)
                    {
                        if (l == null) continue;
                        Vector3 tela = cam.WorldToScreenPoint(l.transform.position + Vector3.up * 0.2f);
                        float d = Vector2.Distance(mouse, tela);
                        if (d < 34 && d < melhorLoot)
                        {
                            melhorLoot = d;
                            LootMirado = l;
                        }
                    }
                    if (LootMirado == null && _p.Cena != null)
                    {
                        float melhorObj = 1e9f;
                        foreach (VisualDeObjeto o in _p.Cena.Objetos)
                        {
                            if (o == null || !o.O.Interativo || o.O.Tipo == TipoObjeto.Saida || o.O.Tipo == TipoObjeto.MuroMovel) continue;
                            if (o.O.Tipo == TipoObjeto.Bau && o.O.Usado) continue;
                            if (o.O.Tipo == TipoObjeto.Porta && o.O.Ligado) continue;
                            Vector3 tela = cam.WorldToScreenPoint(o.transform.position + Vector3.up * 0.8f);
                            if (tela.z < 0) continue;
                            float raio = o.O.Tipo == TipoObjeto.Porta ? 120 : 60;
                            float d = Vector2.Distance(mouse, tela);
                            if (d < raio && d < melhorObj)
                            {
                                melhorObj = d;
                                ObjetoMirado = o;
                            }
                        }
                    }
                }
            }
            if (antes != Mirado)
            {
                if (antes != null) antes.Destacar(false);
                if (Mirado != null) Mirado.Destacar(true);
            }
        }
    }
}
