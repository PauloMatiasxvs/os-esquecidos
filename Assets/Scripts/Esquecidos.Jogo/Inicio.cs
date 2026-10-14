using UnityEngine;

namespace Esquecidos.Jogo
{
    /// <summary>
    /// Ponto de entrada. A cena do projeto e vazia: quando ela carrega, este
    /// metodo cria o objeto raiz e o resto do jogo se monta em codigo, como o
    /// Pal3.Unity faz a partir de um unico objeto de inicializacao.
    /// </summary>
    public static class Inicio
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Comecar()
        {
            if (Object.FindAnyObjectByType<Principal>() != null) return;
            var raiz = new GameObject("Os Esquecidos");
            Object.DontDestroyOnLoad(raiz);
            raiz.AddComponent<Principal>();
        }
    }
}
