using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using Vec2 = System.Numerics.Vector2;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// --campanha: a historia principal inteira com o jogo rodando, pelos
    /// mesmos caminhos do jogador (topico de conversa, portao, alavanca,
    /// chefe, marco, entrega). Andar ate as coisas e o golpe final nos
    /// chefes sao atalhos; o resto e o jogo de verdade, com fotos.
    /// </summary>
    internal sealed partial class RoteiroDeTeste
    {
        private static readonly Poder Golpe = new Poder { Id = "teste", Fonte = FonteDeDano.Nenhuma, DanoFixoMin = 99999, DanoFixoMax = 99999 };

        private bool _parou;

        private void Parar(string motivo)
        {
            Problema("CAMPANHA PAROU: " + motivo);
            _parou = true;
        }

        private void Teleportar(Vec2 perto)
        {
            Entidade j = Partida.Jogador;
            j.Pos = Partida.Mundo.Grade.LivreMaisProximo(perto, j.Raio, out Vec2 livre) ? livre : perto;
            j.PosAnterior = j.Pos;
            j.LimparOrdem();
        }

        private IEnumerator Topico(string npc, string roteiro, int escolha = 0)
        {
            if (_parou) yield break;
            Entidade e = Partida.Mundo.Entidades.FirstOrDefault(x => x.NpcId == npc);
            if (e == null)
            {
                Parar($"{npc} nao esta em {Partida.Mapa.Id}");
                yield break;
            }
            DialogoDef t = Partida.Diario.Topicos(npc).FirstOrDefault(x => x.Roteiro == roteiro);
            if (t == null)
            {
                Parar($"{npc} sem o topico {roteiro}");
                yield break;
            }
            Teleportar(e.Pos + new Vec2(1.3f, 0));
            yield return Esperar(0.3f);
            Partida.EscolherTopico(t);
            yield return PularCenas(60, escolha);
        }

        private IEnumerator Interagir(ObjetoDeCena o, string foto = null)
        {
            if (_parou) yield break;
            Teleportar(o.Pos + o.FrenteDoObjeto() * 1.1f);
            bool antes = o.Ligado;
            Partida.InteragirCom(o.Id);
            // Anda ate la de verdade se o teleporte caiu do lado errado de um muro.
            float fim = Time.unscaledTime + 10;
            while (Time.unscaledTime < fim && o.Ligado == antes && !Partida.EmCena && !_p.UI.Dialogo.Aberto)
            {
                ManterVivo();
                if (Partida.Jogador.Ordem.Tipo != TipoOrdem.Interagir) Partida.InteragirCom(o.Id);
                yield return Esperar(0.2f);
            }
            yield return Esperar(0.8f);
            if (foto != null) yield return Capturar(foto);
            yield return PularCenas(60);
        }

        private IEnumerator Abater(Entidade e, int escolha = 0)
        {
            if (_parou) yield break;
            Partida.Mundo.Atingir(Partida.Jogador, e, Golpe, 1, e.Pos);
            yield return Esperar(0.6f);
            yield return PularCenas(60, escolha);
            if (e.Vivo) Parar($"{e.Tipo} nao morreu");
        }

        /// <summary>Pisa na saida e espera o mapa novo.</summary>
        private IEnumerator Sair(Func<ObjetoDeCena, bool> qual, string destino)
        {
            if (_parou) yield break;
            ObjetoDeCena s = Partida.Mundo.Objetos.FirstOrDefault(o => o.Tipo == TipoObjeto.Saida && qual(o));
            if (s == null)
            {
                Parar($"sem saida para {destino} em {Partida.Mapa.Id}");
                yield break;
            }
            Teleportar(s.Pos);
            Partida.Jogador.Pos = s.Pos;
            yield return EsperarMapa(destino);
            if (Partida.Mapa.Id != destino) Parar($"a saida nao levou a {destino}");
        }

        private IEnumerator PeloPortao(string lado, string setor)
        {
            if (_parou) yield break;
            if (Partida.Mapa.Id != "patio") yield return Sair(o => o.Destino == "patio", "patio");
            if (_parou) yield break;
            if (!Partida.Ciclo.PortoesAbertos || Partida.Ciclo.Hora > 15) Partida.Ciclo.DormirAteAmanhecer();
            Partida.Ciclo.Definir(Partida.Ciclo.Dia, Mathf.Max(Partida.Ciclo.Hora, 8.5f));
            yield return Esperar(0.5f);
            ObjetoDeCena portao = Partida.Mundo.Objetos.FirstOrDefault(o => o.Chave == "patio:portao_" + lado);
            if (portao == null || !portao.Ligado)
            {
                Parar($"portao {lado} fechado");
                yield break;
            }
            yield return Sair(o => o.Destino == "portao:" + lado, setor);
        }

        /// <summary>Alavancas da grade, briga de verdade com o chefe por uns segundos, golpe final, placa do marco.</summary>
        private IEnumerator ChefeEMarco(string setor)
        {
            if (_parou) yield break;
            ObjetoDeCena grade = Partida.Mundo.Objetos.FirstOrDefault(o => o.Tipo == TipoObjeto.Porta && o.Dependencias.Count > 0);
            if (grade != null)
            {
                int n = 0;
                foreach (int id in grade.Dependencias)
                {
                    ObjetoDeCena alavanca = Partida.Mundo.Objetos.First(o => o.Id == id);
                    if (!alavanca.Ligado) yield return Interagir(alavanca, n++ == 0 ? $"c_{setor}_alavanca" : null);
                }
                yield return Esperar(1);
                if (!grade.Ligado) Problema($"grade da arena de {setor} nao abriu");
            }
            Entidade chefe = Partida.Mundo.Entidades.FirstOrDefault(e => e.Chefe && e.Vivo);
            if (chefe == null)
            {
                Parar($"sem chefe vivo em {setor}");
                yield break;
            }
            Teleportar(chefe.Pos + new Vec2(3.5f, 0));
            Anotar($"chefe {chefe.Tipo} nv {chefe.Nivel}, vida {chefe.VidaMax:0}");
            yield return Combater(7, $"c_{setor}_chefe", true);
            if (chefe.Vivo) yield return Abater(chefe);
            yield return Esperar(1.5f);
            yield return Capturar($"c_{setor}_chefe_caido");
            if (!Partida.Diario.TemStatus("chefe_" + setor)) Problema($"status chefe_{setor} nao veio");
            ObjetoDeCena marco = Partida.Mundo.Objetos.FirstOrDefault(o => o.Tipo == TipoObjeto.Marco);
            if (marco != null) yield return Interagir(marco, $"c_{setor}_marco");
        }

        private IEnumerator AbrirSetor(string setor)
        {
            yield return Esperar(1.5f);
            yield return Capturar($"c_{setor}");
            yield return Combater(8, $"c_{setor}_luta");
        }

        private IEnumerator Campanha()
        {
            _p.ComecarNovoJogo("guardiao", "Teste", null);
            yield return Esperar(3);
            yield return PularCenas(90);
            yield return Capturar("c00_patio");

            yield return Topico("iara", "iara_m01");
            yield return Topico("duda", "duda_m01");
            yield return Topico("marreta", "marreta_m01");
            Entidade boneco = Partida.Mundo.Entidades.FirstOrDefault(e => e.Tipo == "boneco");
            if (boneco != null)
            {
                Teleportar(boneco.Pos + new Vec2(1.5f, 0));
                Partida.Atacar(boneco.Id, true);
                yield return Esperar(2);
                yield return Capturar("c01_bonecos");
            }
            foreach (Entidade b in Partida.Mundo.Entidades.Where(e => e.Tipo == "boneco" && e.Vivo).ToList()) yield return Abater(b);
            yield return Topico("teo", "teo_pronto");
            if (!Partida.Diario.Concluida("m01_primeiro_dia")) Parar($"m01 na etapa {Partida.Diario.Etapa("m01_primeiro_dia")}");
            Anotar("primeiro dia concluido");

            yield return PeloPortao("leste", "setor1");
            yield return AbrirSetor("setor1");
            foreach (Entidade j in Partida.Mundo.Entidades.Where(e => e.Tipo == "tecelao_jovem" && e.Vivo).Take(5).ToList()) yield return Abater(j);
            yield return ChefeEMarco("setor1");
            yield return Sair(o => o.Destino == "patio", "patio");
            yield return Topico("caio", "caio_m02");
            if (!_parou && !Partida.Diario.Concluida("m02_hera")) Parar($"m02 na etapa {Partida.Diario.Etapa("m02_hera")}");

            yield return PeloPortao("sul", "setor2");
            yield return AbrirSetor("setor2");
            yield return ChefeEMarco("setor2");
            yield return Sair(o => o.Destino == "patio", "patio");
            yield return Esperar(1);
            if (!_parou && !Partida.Diario.TemStatus("recebeu_gravador"))
            {
                Parar("o gravador nao chegou na volta ao Patio");
            }
            ObjetoDeCena montaCargas = _parou ? null : Partida.Mundo.Objetos.FirstOrDefault(o => o.Roteiro == "monta_cargas");
            if (montaCargas != null)
            {
                Teleportar(montaCargas.Pos + montaCargas.FrenteDoObjeto() * 1.1f);
                Partida.InteragirCom(montaCargas.Id);
                yield return Esperar(4);
                yield return Capturar("c02_gravador");
                yield return PularCenas(60);
            }
            yield return Topico("iara", "iara_gravador", 0);

            yield return PeloPortao("oeste", "setor3");
            yield return AbrirSetor("setor3");
            yield return ChefeEMarco("setor3");

            yield return PeloPortao("norte", "setor4");
            yield return AbrirSetor("setor4");
            yield return ChefeEMarco("setor4");
            if (!_parou)
            {
                // A noite no labirinto: noturnos aparecem e os muros andam na virada da hora.
                Partida.Ciclo.Definir(Partida.Ciclo.Dia, 19.97f);
                yield return Esperar(3);
                Partida.Ciclo.Definir(Partida.Ciclo.Dia, 20.97f);
                int muros = Partida.Mundo.Objetos.Count(o => o.Tipo == TipoObjeto.MuroMovel && o.Ligado);
                yield return Esperar(2.5f);
                int depois = Partida.Mundo.Objetos.Count(o => o.Tipo == TipoObjeto.MuroMovel && o.Ligado);
                ManterVivo();
                yield return Capturar("c03_setor4_noite");
                Anotar($"noite: {Partida.Mundo.Entidades.Count(e => e.Vivo && e.Tipo == "tecelao_noturno")} noturnos, muros erguidos {muros} -> {depois}");
                Partida.Ciclo.Definir(Partida.Ciclo.Dia, 10);
                Partida.Mundo.Noite = false;
            }
            yield return Sair(o => o.Destino == "patio", "patio");
            yield return Topico("caio", "caio_m03");
            if (!_parou && !Partida.Diario.TemStatus("padrao_decifrado")) Parar("padrao nao decifrado");

            yield return PeloPortao("norte", "setor4");
            yield return Sair(o => o.Destino == "setor5", "setor5");
            yield return AbrirSetor("setor5");
            yield return ChefeEMarco("setor5");
            yield return Sair(o => o.Destino == "laboratorio", "laboratorio");
            yield return AbrirSetor("laboratorio");
            if (_parou) yield break;
            Anotar($"companheiro no laboratorio: {Partida.CompanheiroNpc ?? "ninguem"}");

            Entidade executor = Partida.Mundo.Entidades.FirstOrDefault(e => e.Chefe && e.Vivo);
            if (executor == null)
            {
                Parar("sem Executor no laboratorio");
                yield break;
            }
            Teleportar(executor.Pos + new Vec2(3.5f, 0));
            yield return Combater(7, "c_laboratorio_chefe", true);
            string salvo = Partida.Salvar();
            for (int final = 0; final < 2; final++)
            {
                if (final == 1)
                {
                    _p.CarregarTexto(salvo);
                    yield return EsperarMapa("laboratorio");
                    executor = Partida.Mundo.Entidades.FirstOrDefault(e => e.Chefe && e.Vivo);
                    if (executor == null)
                    {
                        Parar("o Executor nao voltou ao carregar");
                        yield break;
                    }
                }
                Partida.Mundo.Atingir(Partida.Jogador, executor, Golpe, 1, executor.Pos);
                yield return Esperar(1);
                yield return PularCenas(90, final);
                string nome = final == 0 ? "libertacao" : "arquiteto";
                if (!Partida.Diario.TemStatus("final_" + nome)) Problema($"final {nome} nao marcado");
                if (_p.Estado != EstadoDoJogo.Creditos) Problema($"creditos do final {nome} nao abriram (estado {_p.Estado})");
                yield return Esperar(2);
                yield return Capturar($"c_final_{nome}");
                yield return Esperar(8);
                yield return Capturar($"c_final_{nome}_creditos");
            }
            Anotar($"campanha completa: nivel {Partida.Heroi.Nivel}, {Partida.Diario.Missoes.Values.Count(m => m.Concluida)} missoes concluidas");
        }
    }
}
