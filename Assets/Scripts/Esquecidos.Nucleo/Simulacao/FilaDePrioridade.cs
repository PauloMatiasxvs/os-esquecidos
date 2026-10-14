using System.Collections.Generic;

namespace Esquecidos.Nucleo.Simulacao
{
    /// <summary>
    /// Heap binario de minimo. O PriorityQueue do .NET 6 nao existe no
    /// .NET Standard 2.1 do Unity, entao o A* usa este.
    /// </summary>
    public sealed class FilaDePrioridade
    {
        private readonly List<(int item, float prioridade)> _heap = new List<(int, float)>();

        public int Count => _heap.Count;

        public void Limpar() => _heap.Clear();

        public void Enfileirar(int item, float prioridade)
        {
            _heap.Add((item, prioridade));
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int pai = (i - 1) / 2;
                if (_heap[pai].prioridade <= _heap[i].prioridade) break;
                (_heap[pai], _heap[i]) = (_heap[i], _heap[pai]);
                i = pai;
            }
        }

        public int Desenfileirar()
        {
            int topo = _heap[0].item;
            int ultimo = _heap.Count - 1;
            _heap[0] = _heap[ultimo];
            _heap.RemoveAt(ultimo);
            int i = 0;
            int n = _heap.Count;
            while (true)
            {
                int esq = 2 * i + 1, dir = esq + 1, menor = i;
                if (esq < n && _heap[esq].prioridade < _heap[menor].prioridade) menor = esq;
                if (dir < n && _heap[dir].prioridade < _heap[menor].prioridade) menor = dir;
                if (menor == i) break;
                (_heap[menor], _heap[i]) = (_heap[i], _heap[menor]);
                i = menor;
            }
            return topo;
        }
    }
}
