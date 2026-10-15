using System.Collections;
using System.IO;
using System.Linq;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using Vec2 = System.Numerics.Vector2;

namespace Esquecidos.Jogo
{
    /// <summary>--fotos: fotos para o README e a pagina do jogo, sem a interface por cima.</summary>
    internal sealed partial class RoteiroDeTeste
    {
        private IEnumerator Foto(string nome, float hora, float yaw, float distancia, float espera = 2.5f, bool interfaceVisivel = false)
        {
            Partida.Ciclo.Definir(Partida.Ciclo.Dia, hora);
            Partida.Ciclo.Parado = true;
            _p.Camera.DefinirYaw(yaw);
            _p.Camera.DefinirDistancia(distancia);
            _p.UI.MostrarCamadas(interfaceVisivel);
            yield return Esperar(espera);
            ManterVivo();
            yield return Capturar(nome);
            _p.UI.MostrarCamadas(true);
            Partida.Ciclo.Parado = false;
        }

        private IEnumerator FotoDeSetor(string setor, string nome, float hora, float yaw)
        {
            Partida.Ciclo.Definir(Partida.Ciclo.Dia, hora);
            Partida.IrPara(setor, "patio");
            yield return EsperarMapa(setor);
            // Um pouco para dentro, onde os muros enquadram.
            Entidade j = Partida.Jogador;
            Partida.MoverPara(j.Pos + new Vec2(0, 7));
            yield return Esperar(2);
            Entidade bicho = MaisProximo(j);
            if (bicho != null && Vec2.Distance(bicho.Pos, j.Pos) < 18) Partida.Atacar(bicho.Id, true);
            yield return Esperar(1.5f);
            yield return Foto(nome, hora, yaw, 16, 0.8f);
        }

        /// <summary>Grava quadros numerados (para virar GIF): o tempo do jogo anda 1/15 s por quadro, sem pressa.</summary>
        private IEnumerator Gravar(string prefixo, float segundos, System.Action<int> porQuadro = null)
        {
            const int Qps = 15;
            Time.captureFramerate = Qps;
            int n = (int)(segundos * Qps);
            string pasta = Path.Combine(_pasta, "quadros");
            Directory.CreateDirectory(pasta);
            for (int i = 0; i < n; i++)
            {
                ManterVivo();
                porQuadro?.Invoke(i);
                yield return new WaitForEndOfFrame();
                Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(pasta, $"{prefixo}_{i:000}.png"), t.EncodeToPNG());
                Destroy(t);
            }
            Time.captureFramerate = 0;
            Anotar($"gravou {n} quadros de {prefixo}");
        }

        /// <summary>O bicho com mais vizinhos perto: onde a briga fica bonita.</summary>
        private Entidade CentroDoBando()
        {
            Entidade melhor = null;
            int mais = -1;
            foreach (Entidade e in Partida.Mundo.Entidades)
            {
                if (!e.Vivo || e.Faccao != Faccao.Inimigo || e.Chefe) continue;
                int n = Partida.Mundo.Entidades.Count(o => o != e && o.Vivo && o.Faccao == Faccao.Inimigo && Vec2.Distance(o.Pos, e.Pos) < 7);
                if (n > mais)
                {
                    mais = n;
                    melhor = e;
                }
            }
            return melhor;
        }

        private IEnumerator Video()
        {
            _p.ComecarNovoJogo("corredor", "Teste", null);
            yield return Esperar(3);
            yield return PularCenas(90);
            Partida.Ciclo.Definir(Partida.Ciclo.Dia, 16.8f);
            Partida.Ciclo.Parado = true;
            Partida.IrPara("setor1", "patio");
            yield return EsperarMapa("setor1");
            _p.UI.MostrarCamadas(false);
            Entidade centro = CentroDoBando();
            if (centro != null) Teleportar(centro.Pos + new Vec2(6, 0));
            _p.Camera.DefinirDistancia(13);
            yield return Esperar(0.6f);
            int alvo = 0;
            yield return Gravar("briga", 9f, i =>
            {
                Entidade j = Partida.Jogador;
                Entidade e = Partida.Mundo.Buscar(alvo);
                if (e == null || !e.Vivo)
                {
                    e = MaisProximo(j);
                    alvo = e?.Id ?? 0;
                    if (e != null) Partida.Atacar(e.Id, true);
                }
                // O poder da classe de vez em quando: o corte duplo e o rolamento aparecem no video.
                if (e != null && i % 30 == 15) Partida.UsarBarra(1, e.Pos, e.Id);
                if (i % 45 == 40 && e != null) Partida.Esquivar(j.Pos + (j.Pos - e.Pos));
                if (i % 3 == 0) _p.Camera.GirarLivre(1.2f);
            });
            Partida.Ciclo.Parado = false;
            _p.UI.MostrarCamadas(true);
        }

        private IEnumerator Fotos()
        {
            _p.AbrirMenu();
            yield return Esperar(4);
            yield return Capturar("f01_menu");
            _p.UI.Menus.Criacao();
            yield return Esperar(2.5f);
            yield return Capturar("f02_criacao");
            _p.UI.Menus.EsconderTudo();

            _p.ComecarNovoJogo("corredor", "Teste", null);
            yield return Esperar(3);
            yield return PularCenas(90);
            Partida.MoverPara(new Vec2(47, 46));
            yield return Esperar(4);
            yield return Foto("f03_patio_tarde", 17.2f, 30, 17);
            yield return Foto("f04_patio_noite", 22f, 30, 15, 3);
            _p.UI.Hud.Mostrar(true);
            yield return Foto("f05_patio_hud", 10f, 0, 18, 2, true);

            yield return FotoDeSetor("setor1", "f06_setor1", 9.5f, 20);
            yield return Foto("f07_setor1_combate", 9.5f, 20, 13, 1.5f, true);
            yield return FotoDeSetor("setor2", "f08_setor2", 16.5f, -30);
            yield return FotoDeSetor("setor3", "f09_setor3", 12f, 45);
            yield return FotoDeSetor("setor4", "f10_setor4", 11f, 0);
            yield return FotoDeSetor("setor5", "f11_nucleo", 11f, 0);
            yield return FotoDeSetor("laboratorio", "f12_laboratorio", 11f, 30);

            Partida.IrPara("setor1", "patio");
            yield return EsperarMapa("setor1");
            Entidade chefe = Partida.Mundo.Entidades.FirstOrDefault(e => e.Chefe && e.Vivo);
            if (chefe != null)
            {
                Teleportar(chefe.Pos + new Vec2(4, 0));
                Partida.Atacar(chefe.Id, true);
                yield return Esperar(3);
                yield return Foto("f13_chefe", 10f, 20, 15, 0.5f, true);
            }

            _p.MostrarCreditos("libertacao");
            yield return Esperar(4);
            yield return Capturar("f14_creditos");
        }
    }
}
