using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Esquecidos.Editor
{
    /// <summary>
    /// Tudo que se faz no editor pela linha de comando, sem abrir janela:
    /// configurar o projeto e gerar o executavel. A cena e vazia de proposito:
    /// o jogo inteiro se monta em codigo quando ela carrega.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Esquecidos.Editor.Construcao.Configurar
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Esquecidos.Editor.Construcao.Windows
    /// </summary>
    public static class Construcao
    {
        public const string Cena = "Assets/Cenas/Jogo.unity";

        [MenuItem("Os Esquecidos/Configurar projeto")]
        public static void Configurar()
        {
            Directory.CreateDirectory("Assets/Cenas");
            if (!File.Exists(Cena))
            {
                var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(s, Cena);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Cena, true) };

            PlayerSettings.companyName = "paulo";
            PlayerSettings.productName = "Os Esquecidos";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            QualitySettings.vSyncCount = 1;
            AssetDatabase.SaveAssets();
            Debug.Log("[Os Esquecidos] projeto configurado");
        }

        [MenuItem("Os Esquecidos/Gerar executável Windows")]
        public static void Windows()
        {
            Configurar();
            string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "dist", "OsEsquecidos"));
            Directory.CreateDirectory(pasta);
            var opcoes = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(pasta, "Os Esquecidos.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport r = BuildPipeline.BuildPlayer(opcoes);
            Debug.Log($"[Os Esquecidos] build: {r.summary.result}, {r.summary.totalErrors} erros, {r.summary.totalSize / (1024 * 1024)} MB em {r.summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
