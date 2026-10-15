using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Esquecidos.Motor;
using Esquecidos.Motor.Entrada;
using Esquecidos.Nucleo;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Opcoes de quem joga, num arquivo de texto ao lado do jogo salvo:
    /// som, idioma, tela, qualidade, tamanho do texto e teclas. "Ler" vem
    /// antes de tudo (o idioma decide como o catalogo e lido); "Aplicar"
    /// depois que o som, a camera e a interface existem.
    /// </summary>
    public static class Opcoes
    {
        public static float Musica = 0.8f;
        public static float Efeitos = 1f;
        public static float Ambiente = 1f;
        public static string Idioma;
        public static int Qualidade = 2;
        public static int Largura, Altura;
        public static bool TelaCheia;
        public static bool VSync = true;
        public static float EscalaDoTexto = 1f;
        public static string Teclas = "";

        private static bool _lido;

        public static void Ler()
        {
            Idioma = Application.systemLanguage == SystemLanguage.Portuguese ? Nucleo.Idioma.Portugues : Nucleo.Idioma.Ingles;
            _lido = true;
            try
            {
                if (!File.Exists(Principal.CaminhoDasOpcoes)) return;
                foreach (string linha in File.ReadAllLines(Principal.CaminhoDasOpcoes))
                {
                    int i = linha.IndexOf('=');
                    if (i <= 0) continue;
                    string k = linha.Substring(0, i).Trim(), v = linha.Substring(i + 1).Trim();
                    float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f);
                    switch (k)
                    {
                        case "musica": Musica = Mathf.Clamp01(f); break;
                        case "efeitos": Efeitos = Mathf.Clamp01(f); break;
                        case "ambiente": Ambiente = Mathf.Clamp01(f); break;
                        case "idioma": Idioma = v == Nucleo.Idioma.Ingles ? Nucleo.Idioma.Ingles : Nucleo.Idioma.Portugues; break;
                        case "qualidade": Qualidade = Mathf.Clamp((int)f, 0, 2); break;
                        case "largura": Largura = (int)f; break;
                        case "altura": Altura = (int)f; break;
                        case "tela_cheia": TelaCheia = f > 0.5f; break;
                        case "vsync": VSync = f > 0.5f; break;
                        case "texto": EscalaDoTexto = Mathf.Clamp(f, 0.8f, 1.5f); break;
                        case "teclas": Teclas = v; break;
                    }
                }
            }
            catch (Exception e)
            {
                Registro.Aviso("opcoes: " + e.Message);
            }
        }

        public static void Aplicar(Principal p)
        {
            if (!_lido) Ler();
            p.Sons.Musica.Volume = Musica;
            p.Sons.VolumeEfeitos = Efeitos;
            p.Sons.Musica.VolumeAmbiente = Ambiente;
            Controles.Ler(Teclas);
            QualidadeGrafica.Aplicar(Qualidade, p.Camera);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 144;
            p.UI.EscalaDoTexto(EscalaDoTexto);
            AplicarTela();
        }

        public static void AplicarTela()
        {
            FullScreenMode modo = TelaCheia ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Largura >= 640 && Altura >= 360) Screen.SetResolution(Largura, Altura, modo);
            else Screen.fullScreenMode = modo;
        }

        public static void Salvar(Principal p)
        {
            // O modo de teste zera o som; isso nao pode ir parar nas opcoes de quem joga.
            if (p.ModoTeste) return;
            Musica = p.Sons.Musica.Volume;
            Efeitos = p.Sons.VolumeEfeitos;
            Ambiente = p.Sons.Musica.VolumeAmbiente;
            Teclas = Controles.Serializar();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Principal.CaminhoDasOpcoes));
                var linhas = new List<string>
                {
                    "musica=" + Musica.ToString("0.###", CultureInfo.InvariantCulture),
                    "efeitos=" + Efeitos.ToString("0.###", CultureInfo.InvariantCulture),
                    "ambiente=" + Ambiente.ToString("0.###", CultureInfo.InvariantCulture),
                    "idioma=" + Idioma,
                    "qualidade=" + Qualidade,
                    "largura=" + Largura,
                    "altura=" + Altura,
                    "tela_cheia=" + (TelaCheia ? 1 : 0),
                    "vsync=" + (VSync ? 1 : 0),
                    "texto=" + EscalaDoTexto.ToString("0.##", CultureInfo.InvariantCulture),
                    "teclas=" + Teclas,
                };
                File.WriteAllLines(Principal.CaminhoDasOpcoes, linhas);
            }
            catch (Exception e)
            {
                Registro.Aviso("opcoes: " + e.Message);
            }
        }
    }

    /// <summary>Tres niveis de qualidade mexendo no que pesa: escala de render, sombra, oclusao e serrilhado.</summary>
    public static class QualidadeGrafica
    {
        public static readonly string[] Nomes = { "Baixa", "Média", "Alta" };

        public static void Aplicar(int nivel, CameraDoJogo camera)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = nivel == 0 ? 0.8f : 1f;
                urp.shadowDistance = nivel == 0 ? 32 : nivel == 1 ? 45 : 60;
                urp.shadowCascadeCount = nivel == 0 ? 1 : nivel == 1 ? 2 : 4;
                AtivarOclusao(urp, nivel >= 1);
            }
            if (camera != null && camera.Cam != null)
            {
                UniversalAdditionalCameraData d = camera.Cam.GetUniversalAdditionalCameraData();
                d.antialiasing = nivel == 0 ? AntialiasingMode.None : nivel == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
        }

        /// <summary>O SSAO e uma "renderer feature" do PC_Renderer; o URP nao expoe a lista, entao vai por reflexao.</summary>
        private static void AtivarOclusao(UniversalRenderPipelineAsset urp, bool ligado)
        {
            try
            {
                FieldInfo campo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (!(campo?.GetValue(urp) is ScriptableRendererData[] lista)) return;
                foreach (ScriptableRendererData r in lista)
                {
                    if (r == null) continue;
                    foreach (ScriptableRendererFeature f in r.rendererFeatures)
                        if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) f.SetActive(ligado);
                }
            }
            catch (Exception e)
            {
                Registro.Aviso("qualidade: " + e.Message);
            }
        }
    }
}
