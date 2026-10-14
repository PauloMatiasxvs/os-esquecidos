using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Regras;
using Esquecidos.Nucleo.Roteiro;
using Esquecidos.Nucleo.Simulacao;

namespace Esquecidos.Nucleo.Partidas
{
    /// <summary>O que cada coisa que acontece no mundo significa para a partida.</summary>
    public sealed partial class Partida :
        IExecutor<NotMorte>,
        IExecutor<NotInteracao>,
        IExecutor<NotFalarComNpc>,
        IExecutor<NotObjetoPisado>
    {
        public void Executar(NotMorte n)
        {
            Entidade morto = n.Morto;
            if (morto == Jogador)
            {
                AoMorrerJogador();
                return;
            }
            if (morto.Faccao != Faccao.Inimigo || morto.Def == null || morto.Invocado && morto.Def.Xp == 0) return;
            InimigoDef def = morto.Def;
            Contar("inimigos_mortos");
            Contar("matou:" + def.Id);
            Diario.AoMatar(def.Id);

            // Bicho muito abaixo do nivel vale pouco: o labirinto nao paga para pisar em formiga.
            int diferenca = Heroi.Nivel - morto.Nivel;
            float mult = Math.Clamp(1f - 0.12f * (diferenca - 2), 0.1f, 1.2f);
            if (morto.Elite) mult *= 2.5f;
            int xpBase = (int)MathF.Round(def.Xp * (1 + 0.3f * (morto.Nivel - def.Nivel)));
            GanharXp((int)MathF.Max(1, xpBase * mult));

            float achado = Jogador?.Estat[Estat.AchadoRaro] ?? 0;
            if (morto.Elite) achado += 60;
            if (def.Chefe) achado += 150;
            if (def.Loot != null && Catalogo.Loot.TryGetValue(def.Loot, out TabelaDeLoot tabela))
            {
                foreach (Item item in Loot.Rolar(tabela, morto.Nivel, Rng, achado, def.Chefe || morto.Elite))
                    Mundo.SoltarItem(morto.Pos, item);
            }
            if (def.SucataMax > 0 && Rng.NextDouble() < (def.Chefe ? 1 : 0.55))
            {
                int s = Rng.Next(def.SucataMin, def.SucataMax + 1) * Math.Max(1, morto.Nivel / 3 + 1);
                if (morto.Elite) s *= 3;
                Mundo.SoltarItem(morto.Pos, null, s);
            }

            if (def.Chefe)
            {
                SetorDef setor = Catalogo.Setores.Values.FirstOrDefault(x => x.Chefe == def.Id);
                if (setor?.StatusDoChefe != null)
                {
                    Diario.DefinirStatus(setor.StatusDoChefe);
                    AtualizarPortoes();
                    Mensagem($"{def.Nome} caiu. A passagem ao norte se abriu.", "#ffd25a");
                }
                string roteiro = "chefe_" + def.Id;
                if (Roteiros.Existe(roteiro)) Roteiros.Executar(roteiro);
            }
        }

        private void AoMorrerJogador()
        {
            JogadorMorto = true;
            Contar("mortes");
            int perda = (int)MathF.Round(Heroi.Sucata * Catalogo.Regras.PerdaSucataAoMorrer);
            Heroi.Sucata -= perda;
            Diario.DefinirStatus("morreu_uma_vez");
            Despachante.Despachar(new NotJogadorMorreu(perda));
        }

        /// <summary>Acorda na enfermaria do Patio, horas depois, com a vida toda.</summary>
        public void Renascer()
        {
            JogadorMorto = false;
            _vidaGuardada = 99999;
            _folegoGuardado = 99999;
            Ciclo.AvancarHoras(5);
            if (Jogador != null) Jogador.Vida = Jogador.VidaMax;
            EntrarNoMapa("patio", "enfermaria");
            if (Roteiros.Existe("acordou_na_enfermaria")) Roteiros.Executar("acordou_na_enfermaria");
        }

        public void Executar(NotInteracao n)
        {
            if (n.Quem != Jogador) return;
            ObjetoDeCena o = n.Objeto;
            switch (o.Tipo)
            {
                case TipoObjeto.Porta:
                    InteragirComPorta(o);
                    break;
                case TipoObjeto.Alavanca:
                    Mundo.DefinirLigado(o, !o.Ligado);
                    Guardar(o);
                    Despachante.Despachar(new NotSom("alavanca", o.Pos));
                    ObjetoDeCena grade = o.Alvos.Select(Mundo.Objeto).FirstOrDefault(x => x != null && x.Dependencias.Count > 0);
                    if (grade != null)
                    {
                        int puxadas = grade.Dependencias.Count(d => Mundo.Objeto(d)?.Ligado == true);
                        if (grade.Ligado) Mensagem("Um rangido longo, lá na arena: a grade subiu.", "#ffd25a");
                        else Mensagem($"Alavanca {(o.Ligado ? "puxada" : "solta")}. {puxadas} de {grade.Dependencias.Count}.", "#e8e2d0");
                        Despachante.Despachar(new NotTremor(0.15f));
                    }
                    break;
                case TipoObjeto.Bau:
                    AbrirBau(o);
                    break;
                case TipoObjeto.Coletavel:
                    if (o.Usado) break;
                    if (o.Item != null && DarItemAoHeroi(new Item(o.Item)))
                    {
                        o.Usado = true;
                        Mundo.DefinirLigado(o, true);
                        Guardar(o);
                    }
                    break;
                case TipoObjeto.Terminal:
                    LerRegistro(o.Texto);
                    break;
                case TipoObjeto.Painel:
                    Despachante.DespacharDinamico(new AbrirMapa());
                    break;
                case TipoObjeto.Estacao:
                    Despachante.DespacharDinamico(new AbrirOficina(o.Texto));
                    break;
                case TipoObjeto.Guarda:
                    Despachante.DespacharDinamico(new AbrirGuarda());
                    break;
                case TipoObjeto.Descanso:
                    if (Roteiros.Existe("rede")) Roteiros.Executar("rede");
                    else Descansar();
                    break;
                case TipoObjeto.Marco:
                case TipoObjeto.Elevador:
                case TipoObjeto.Cartaz:
                    if (o.Roteiro != null && Roteiros.Existe(o.Roteiro)) Roteiros.Executar(o.Roteiro);
                    else if (o.Tipo == TipoObjeto.Marco) Mensagem("Um obelisco de concreto com marcas que você não entende.");
                    break;
            }
        }

        private void InteragirComPorta(ObjetoDeCena o)
        {
            if (o.Ligado) return;
            if (o.Dependencias.Count > 0)
            {
                int puxadas = o.Dependencias.Count(d => Mundo.Objeto(d)?.Ligado == true);
                Mensagem($"{o.Mensagem} ({puxadas}/{o.Dependencias.Count})", "#d0c0a0");
                return;
            }
            if (o.SoDeDia && !Ciclo.PortoesAbertos)
            {
                Mensagem("Fechado até o amanhecer.", "#9fb4ff");
                return;
            }
            if (o.RequerStatus != null && !Diario.TemStatus(o.RequerStatus))
            {
                Mensagem(o.Mensagem ?? "Trancado.", "#d0c0a0");
                if (Roteiros.Existe("trancado_" + o.RequerStatus)) Roteiros.Executar("trancado_" + o.RequerStatus);
                return;
            }
            if (o.RequerItem != null)
            {
                if (!Heroi.Mochila.Remover(o.RequerItem, 1))
                {
                    Mensagem($"Precisa de: {Catalogo.Item(o.RequerItem).Nome}.", "#d0c0a0");
                    return;
                }
            }
            Mundo.DefinirLigado(o, true);
            Guardar(o);
        }

        private void AbrirBau(ObjetoDeCena o)
        {
            if (o.Usado) return;
            o.Usado = true;
            Mundo.DefinirLigado(o, true);
            Guardar(o);
            Contar("baus_abertos");
            Despachante.Despachar(new NotSom("bau", o.Pos));
            float achado = (Jogador?.Estat[Estat.AchadoRaro] ?? 0) + (o.Loot == "bau_raro" ? 80 : 20);
            if (o.Loot != null && Catalogo.Loot.TryGetValue(o.Loot, out TabelaDeLoot t))
                foreach (Item item in Loot.Rolar(t, o.Nivel, Rng, achado, true))
                    Mundo.SoltarItem(o.Pos + o.FrenteDoObjeto() * 0.9f, item);
            Mundo.SoltarItem(o.Pos + o.FrenteDoObjeto() * 0.9f, null, Rng.Next(4, 12) * o.Nivel);
        }

        public void LerRegistro(string id)
        {
            if (id == null || !Catalogo.Registros.TryGetValue(id, out RegistroDef r)) return;
            bool novo = RegistrosLidos.Add(id);
            Despachante.DespacharDinamico(new AbrirRegistro(id));
            if (!novo) return;
            Contar("registros");
            GanharXp(25 + 10 * Mapa.Nivel);
            Despachante.Despachar(new NotRegistroNovo(r));
            Diario.DefinirStatus("leu_" + id);
            if (Contador("registros") >= 6) Diario.DefinirStatus("leu_seis_registros");
        }

        private void Guardar(ObjetoDeCena o)
        {
            if (o.Chave != null) EstadosDeObjetos[o.Chave] = o.Ligado || o.Usado;
        }

        public void Executar(NotFalarComNpc n)
        {
            if (n.Quem != Jogador || n.Npc.NpcId == null) return;
            List<DialogoDef> topicos = Diario.Topicos(n.Npc.NpcId);
            n.Npc.OlharPara(Jogador.Pos);
            DialogoDef automatico = topicos.FirstOrDefault(t => t.Automatico);
            if (automatico != null)
            {
                Roteiros.Executar(automatico.Roteiro);
                return;
            }
            if (topicos.Count == 0)
            {
                Mensagem($"{n.Npc.Nome} não tem nada a dizer agora.");
                return;
            }
            if (topicos.Count == 1)
            {
                Roteiros.Executar(topicos[0].Roteiro);
                return;
            }
            Despachante.Despachar(new NotMostrarTopicos(n.Npc, topicos));
        }

        public void EscolherTopico(DialogoDef topico)
        {
            if (topico != null) Roteiros.Executar(topico.Roteiro);
        }

        public void Executar(NotObjetoPisado n)
        {
            ObjetoDeCena o = n.Objeto;
            switch (o.Tipo)
            {
                case TipoObjeto.Saida:
                    TentarSair(o);
                    break;
                case TipoObjeto.Gatilho:
                    if (o.UmaVez)
                    {
                        o.Usado = true;
                        Guardar(o);
                    }
                    if (o.Roteiro != null && Roteiros.Existe(o.Roteiro)) Roteiros.Executar(o.Roteiro);
                    break;
            }
        }

        private void TentarSair(ObjetoDeCena o)
        {
            string destino = o.Destino;
            string entrada = o.Entrada;
            if (destino != null && destino.StartsWith("portao:", StringComparison.Ordinal))
            {
                string lado = destino.Substring(7);
                SetorDef setor = Catalogo.Setores.Values.FirstOrDefault(s => s.Portao == lado);
                if (setor == null)
                {
                    AvisarSaida("Do outro lado só há parede. Este portão não leva a lugar nenhum.");
                    return;
                }
                destino = setor.Id;
            }
            if (destino == null) return;
            if (destino != "patio" && Catalogo.Setores.TryGetValue(destino, out SetorDef def) && def.RequerStatus != null && !Diario.TemStatus(def.RequerStatus))
            {
                AvisarSaida("Você não está pronto para ir por aí ainda.");
                return;
            }
            Despachante.Despachar(new NotTransicao(destino, entrada));
            IrPara(destino, entrada);
        }

        private void AvisarSaida(string texto)
        {
            if (_avisoSaidaEm > 0) return;
            _avisoSaidaEm = 3f;
            Mensagem(texto, "#d0c0a0");
        }

        /// <summary>Guarda na mochila; se nao couber, avisa e devolve false.</summary>
        public bool DarItemAoHeroi(Item item, bool avisar = true)
        {
            Item sobra = Heroi.Mochila.Adicionar(item, Catalogo);
            if (sobra != null)
            {
                if (sobra.Quantidade < item.Quantidade)
                {
                    var entrou = item.Clonar();
                    entrou.Quantidade -= sobra.Quantidade;
                    Despachante.Despachar(new NotItemRecebido(entrou));
                }
                if (Mundo != null && Jogador != null) Mundo.SoltarItem(Jogador.Pos, sobra);
                if (avisar) Mensagem("Mochila cheia. O resto ficou no chão.", "#ff9a6a");
                Diario.Reavaliar();
                return false;
            }
            Despachante.Despachar(new NotItemRecebido(item));
            Diario.Reavaliar();
            return true;
        }

        private void AtualizarLootPendente()
        {
            if (_lootPendente == 0 || Jogador == null) return;
            ItemNoChao l = Mundo.Loot.FirstOrDefault(x => x.Id == _lootPendente);
            if (l == null)
            {
                _lootPendente = 0;
                return;
            }
            if (Vector2.Distance(l.Pos, Jogador.Pos) > 1.7f) return;
            _lootPendente = 0;
            Jogador.LimparOrdem();
            Pegar(l);
        }

        private void Pegar(ItemNoChao l)
        {
            if (l.Item == null)
            {
                GanharSucata(l.Sucata, true);
                Mundo.RemoverLoot(l, true);
                return;
            }
            if (Heroi.Mochila.Cabe(l.Item, Catalogo) < l.Item.Quantidade)
            {
                Mensagem("Mochila cheia.", "#ff9a6a");
                return;
            }
            Mundo.RemoverLoot(l, true);
            DarItemAoHeroi(l.Item);
        }

        /// <summary>Clique num item do chao: anda ate ele e pega.</summary>
        public void PedirPegar(int lootId)
        {
            if (Jogador == null || EmCena) return;
            ItemNoChao l = Mundo.Loot.FirstOrDefault(x => x.Id == lootId);
            if (l == null) return;
            if (Vector2.Distance(l.Pos, Jogador.Pos) <= 1.7f)
            {
                Pegar(l);
                return;
            }
            _lootPendente = lootId;
            Jogador.Ordem.Tipo = TipoOrdem.Mover;
            Jogador.Ordem.Ponto = l.Pos;
        }
    }

    public static class ExtensoesDeObjeto
    {
        public static Vector2 FrenteDoObjeto(this ObjetoDeCena o) => new Vector2(MathF.Cos(o.Direcao), MathF.Sin(o.Direcao));
    }
}
