using System.Collections;
using System.IO;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using Vec2 = System.Numerics.Vector2;

namespace Esquecidos.Jogo
{
    /// <summary>--captura: um passeio pelas telas e pelos estados do jogo.</summary>
    internal sealed partial class RoteiroDeTeste
    {
        private IEnumerator Passeio()
        {
            _p.AbrirMenu();
            yield return Esperar(4);
            yield return Capturar("01_menu");

            _p.UI.Menus.Criacao();
            yield return Esperar(2.5f);
            yield return Capturar("02_criacao");
            _p.UI.Menus.EsconderTudo();

            _p.ComecarNovoJogo("corredor", "Teste", null);
            yield return Esperar(5);
            yield return Capturar("03_abertura");
            yield return PularCenas(90);
            Anotar($"abertura terminou; mapa {Partida.Mapa?.Id}, hora {Partida.Ciclo.Hora:0.0}");
            yield return Esperar(2);
            yield return Capturar("04_patio");

            Partida.MoverPara(new Vec2(46, 44));
            yield return Esperar(5);
            yield return Capturar("05_patio_andando");

            Entidade iara = null;
            foreach (Entidade e in Partida.Mundo.Entidades)
                if (e.NpcId == "iara") iara = e;
            if (iara != null)
            {
                Partida.FalarCom(iara.Id);
                float limite = Time.unscaledTime + 12;
                while (!_p.UI.Dialogo.Aberto && Time.unscaledTime < limite) yield return null;
                yield return Esperar(2.5f);
                yield return Capturar("05b_conversa");
                yield return PularCenas(60);
                if (_p.UI.Dialogo.Aberto) _p.UI.Dialogo.Fechar(true);
                Anotar($"conversa com iara; missao m01 = {Partida.Diario.Etapa("m01_primeiro_dia")}");
            }
            else Problema("iara nao encontrada no Patio");

            _p.UI.Janelas.Abrir("inventario");
            yield return Esperar(1);
            yield return Capturar("06_inventario");
            foreach (string id in new[] { "camisa_rasgada", "faca_cozinha", "tenis_gasto" })
            {
                Texture2D icone = _p.UI.Estudio.Icone(_p.Catalogo.Item(id)).texture;
                File.WriteAllBytes(Path.Combine(_pasta, "icone_" + id + ".png"), icone.EncodeToPNG());
            }
            _p.UI.Janelas.FecharTodas();
            _p.UI.Janelas.Abrir("personagem");
            yield return Esperar(1);
            yield return Capturar("07_personagem");
            _p.UI.Janelas.FecharTodas();
            _p.UI.Janelas.Abrir("poderes");
            yield return Esperar(1);
            yield return Capturar("08_poderes");
            _p.UI.Janelas.FecharTodas();
            _p.UI.Janelas.Abrir("diario");
            yield return Esperar(1);
            yield return Capturar("08b_diario");
            _p.UI.Janelas.FecharTodas();

            Partida.Ciclo.Definir(Partida.Ciclo.Dia, 9.5f);
            Partida.IrPara("setor1", "patio");
            yield return EsperarMapa("setor1");
            yield return Capturar("09_setor1");

            yield return Combater(25);

            _p.UI.Janelas.Abrir("mapa");
            yield return Esperar(1);
            yield return Capturar("12_mapa");
            _p.UI.Janelas.FecharTodas();

            Partida.Ciclo.Definir(Partida.Ciclo.Dia, 21.5f);
            yield return Esperar(4);
            ManterVivo();
            yield return Capturar("13_setor1_noite");

            Partida.IrPara("patio", "inicio");
            yield return EsperarMapa("patio");
            yield return Esperar(2);
            yield return Capturar("14_patio_noite");

            Partida.Ciclo.Definir(Partida.Ciclo.Dia + 1, 11f);
            Partida.IrPara("laboratorio", "patio");
            yield return EsperarMapa("laboratorio");
            yield return Esperar(1);
            yield return Capturar("15_laboratorio");

            Entidade j = Partida.Jogador;
            Partida.Mundo.Matar(j, null);
            yield return Esperar(3.5f);
            yield return Capturar("15b_morte");
            Anotar($"morte: estado {_p.Estado}");
            _p.UI.Menus.EsconderTudo();
            _p.Renascer();
            yield return Esperar(3);
            yield return PularCenas(30);
            yield return Capturar("15c_renascer");
            Anotar($"renasceu em {Partida.Mapa?.Id}, vida {Partida.Jogador.Vida:0}");

            _p.UI.Menus.Pausar();
            yield return Esperar(1);
            yield return Capturar("16_pausa");
            _p.UI.Menus.Opcoes(_p.UI.Menus.Pausar);
            yield return Esperar(1);
            yield return Capturar("17_opcoes");
            _p.UI.Menus.Despausar();
        }

        /// <summary>Anda ate o bicho mais perto e bate; fotografa no meio da briga.</summary>
        private IEnumerator Combater(float segundos, string foto = "10_combate", bool chefe = false)
        {
            float inicio = Time.unscaledTime;
            bool fotografou = foto == null;
            int mortos = 0;
            int alvo = 0;
            float golpesInicio = Time.unscaledTime;
            while (Time.unscaledTime - inicio < segundos)
            {
                ManterVivo();
                // Contra chefe o teste so quer ver a luta: o jogador nao cai.
                if (chefe) Partida.Jogador.Vida = Partida.Jogador.VidaMax;
                Entidade j = Partida.Jogador;
                Entidade e = Partida.Mundo.Buscar(alvo);
                if (e == null || !e.Vivo)
                {
                    if (alvo != 0)
                    {
                        mortos++;
                        Anotar($"  matou em {Time.unscaledTime - golpesInicio:0.0}s");
                    }
                    e = MaisProximo(j, chefe);
                    alvo = e?.Id ?? 0;
                    golpesInicio = Time.unscaledTime;
                    if (e != null) Partida.Atacar(e.Id, true);
                }
                if (e == null) break;
                if (Time.unscaledTime - _ultimaNota > 3)
                {
                    _ultimaNota = Time.unscaledTime;
                    Anotar($"  alvo {e.Tipo}#{e.Id} nv {e.Nivel} vida {e.Vida:0}/{e.VidaMax:0} dist {Vec2.Distance(e.Pos, j.Pos):0.0}");
                }
                if (!fotografou && Vec2.Distance(e.Pos, j.Pos) < 4f)
                {
                    yield return Esperar(0.8f);
                    yield return Capturar(foto);
                    fotografou = true;
                }
                yield return Esperar(chefe ? 0.1f : 0.25f);
            }
            if (!fotografou) yield return Capturar(foto);
            Anotar($"combate: {mortos} derrotados; vida {Partida.Jogador.Vida:0}/{Partida.Jogador.VidaMax:0}; nivel {Partida.Heroi.Nivel}, xp {Partida.Heroi.Xp}");
        }
    }
}
