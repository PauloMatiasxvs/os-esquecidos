namespace Esquecidos.Nucleo
{
    /// <summary>
    /// Hash de texto que da o mesmo numero em toda execucao. O GetHashCode de
    /// string no .NET muda a cada processo, e semente feita com ele faria o
    /// labirinto de hoje sair diferente depois de carregar o jogo.
    /// </summary>
    public static class Hash
    {
        public static int Estavel(string texto)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in texto)
                {
                    h ^= c;
                    h *= 16777619;
                }
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
