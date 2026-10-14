using System;
using System.Collections.Generic;
using System.Numerics;

namespace Esquecidos.Nucleo.Simulacao
{
    /// <summary>
    /// A* na grade, com oito vizinhos, sem cortar quina e respeitando a folga
    /// que o raio de quem anda exige. O caminho sai esticado: so os pontos onde
    /// a linha reta deixa de passar.
    /// </summary>
    public static class Caminhos
    {
        private const float Diagonal = 1.41421356f;
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

        [ThreadStatic] private static float[] _custo;
        [ThreadStatic] private static int[] _veioDe;
        [ThreadStatic] private static int[] _marca;
        [ThreadStatic] private static int _geracao;
        [ThreadStatic] private static FilaDePrioridade _aberta;

        public static List<Vector2> Buscar(Grade g, Vector2 de, Vector2 para, float raio, int limiteDeNos = 30000)
        {
            float folgaNecessaria = MathF.Max(0.05f, raio - 0.08f);
            if (!g.LivreMaisProximo(para, folgaNecessaria, out Vector2 destino)) return null;
            if (g.CaminhoLivre(de, destino, folgaNecessaria)) return new List<Vector2> { destino };

            int n = g.Largura * g.Altura;
            if (_custo == null || _custo.Length < n)
            {
                _custo = new float[n];
                _veioDe = new int[n];
                _marca = new int[n];
                _geracao = 0;
            }
            _geracao++;

            (int sx, int sz) = g.CelulaDe(de);
            if (g.Folga(sx, sz) < folgaNecessaria && g.LivreMaisProximo(de, folgaNecessaria, out Vector2 inicioLivre, 6))
                (sx, sz) = g.CelulaDe(inicioLivre);
            (int ex, int ez) = g.CelulaDe(destino);
            int inicio = sz * g.Largura + sx, fim = ez * g.Largura + ex;
            if (!g.Dentro(sx, sz)) return null;

            var aberta = _aberta ??= new FilaDePrioridade();
            aberta.Limpar();
            Visitar(inicio, 0, -1);
            aberta.Enfileirar(inicio, Heuristica(sx, sz, ex, ez));
            int expandidos = 0;
            bool achou = false;
            while (aberta.Count > 0)
            {
                int atual = aberta.Desenfileirar();
                if (atual == fim)
                {
                    achou = true;
                    break;
                }
                if (++expandidos > limiteDeNos) break;
                int ax = atual % g.Largura, az = atual / g.Largura;
                float custoAtual = _custo[atual];
                for (int k = 0; k < 8; k++)
                {
                    int nx = ax + Dx[k], nz = az + Dz[k];
                    if (g.Folga(nx, nz) < folgaNecessaria) continue;
                    if (k >= 4 && (g.Folga(ax + Dx[k], az) < folgaNecessaria || g.Folga(ax, az + Dz[k]) < folgaNecessaria)) continue;
                    int vizinho = nz * g.Largura + nx;
                    float novo = custoAtual + (k >= 4 ? Diagonal : 1f);
                    if (_marca[vizinho] == _geracao && _custo[vizinho] <= novo) continue;
                    Visitar(vizinho, novo, atual);
                    aberta.Enfileirar(vizinho, novo + Heuristica(nx, nz, ex, ez));
                }
            }
            if (!achou) return null;

            var celulas = new List<Vector2>();
            for (int c = fim; c != -1 && c != inicio; c = _veioDe[c])
                celulas.Add(g.Centro(c % g.Largura, c / g.Largura));
            celulas.Reverse();
            if (celulas.Count > 0) celulas[celulas.Count - 1] = destino;
            return Esticar(g, de, celulas, folgaNecessaria);
        }

        private static void Visitar(int celula, float custo, int veioDe)
        {
            _marca[celula] = _geracao;
            _custo[celula] = custo;
            _veioDe[celula] = veioDe;
        }

        private static float Heuristica(int x, int z, int ex, int ez)
        {
            int dx = Math.Abs(x - ex), dz = Math.Abs(z - ez);
            return dx + dz + (Diagonal - 2) * Math.Min(dx, dz);
        }

        private static List<Vector2> Esticar(Grade g, Vector2 de, List<Vector2> pontos, float folga)
        {
            // Avanca enquanto a reta a partir da ancora continua livre; onde ela
            // deixa de passar, o ponto anterior vira curva. Uma checagem por
            // ponto, em vez de testar todos contra todos.
            var saida = new List<Vector2>();
            Vector2 ancora = de;
            for (int i = 0; i < pontos.Count; i++)
            {
                bool ultimo = i == pontos.Count - 1;
                if (ultimo)
                {
                    saida.Add(pontos[i]);
                    break;
                }
                if (!g.CaminhoLivre(ancora, pontos[i + 1], folga))
                {
                    saida.Add(pontos[i]);
                    ancora = pontos[i];
                }
            }
            return saida;
        }
    }
}
