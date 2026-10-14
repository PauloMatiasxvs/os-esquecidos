using UnityEngine;

namespace Esquecidos.Motor
{
    /// <summary>Log do jogo, com prefixo para filtrar no Player.log.</summary>
    public static class Registro
    {
        public static void Info(string texto) => Debug.Log("[Esquecidos] " + texto);
        public static void Aviso(string texto) => Debug.LogWarning("[Esquecidos] " + texto);
        public static void Erro(string texto) => Debug.LogError("[Esquecidos] " + texto);
    }
}
