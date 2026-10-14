using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Esquecidos.Nucleo.Comandos
{
    /// <summary>
    /// Marca de tudo que pode ser despachado. Um comando e so dado: quem sabe
    /// o que fazer com ele e o executor registrado para o tipo dele.
    /// </summary>
    public interface IComando
    {
    }

    public interface IExecutor<in T> where T : IComando
    {
        void Executar(T comando);
    }

    /// <summary>
    /// Quem executa cada tipo de comando. Um objeto se registra uma vez e passa
    /// a receber todos os comandos cujos IExecutor&lt;T&gt; ele implementa.
    /// </summary>
    public sealed class RegistroDeExecutores
    {
        private readonly Dictionary<Type, List<object>> _porTipo = new();

        public void Registrar(object executor)
        {
            foreach (Type tipo in TiposDeComandoDe(executor))
            {
                if (!_porTipo.TryGetValue(tipo, out List<object> lista))
                {
                    lista = new List<object>();
                    _porTipo[tipo] = lista;
                }
                if (!lista.Contains(executor)) lista.Add(executor);
            }
        }

        public void Remover(object executor)
        {
            foreach (Type tipo in TiposDeComandoDe(executor))
            {
                if (_porTipo.TryGetValue(tipo, out List<object> lista)) lista.Remove(executor);
            }
        }

        /// <summary>Copia, para um executor poder se registrar durante o despacho.</summary>
        public object[] ExecutoresDe(Type tipoDeComando)
        {
            return _porTipo.TryGetValue(tipoDeComando, out List<object> lista) ? lista.ToArray() : Array.Empty<object>();
        }

        private static IEnumerable<Type> TiposDeComandoDe(object executor)
        {
            return executor.GetType().GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IExecutor<>))
                .Select(i => i.GetGenericArguments()[0]);
        }
    }

    /// <summary>
    /// Entrega cada comando a todos os executores do tipo dele. O caminho
    /// generico e o do codigo; o caminho por reflexao e o dos roteiros, que so
    /// sabem o tipo do comando depois de ler o texto.
    /// </summary>
    public sealed class Despachante
    {
        private readonly RegistroDeExecutores _registro;
        private readonly Dictionary<Type, MethodInfo> _metodos = new();

        /// <summary>Todo comando despachado passa por aqui: console e testes escutam.</summary>
        public event Action<IComando> AoDespachar;

        public Despachante(RegistroDeExecutores registro)
        {
            _registro = registro ?? throw new ArgumentNullException(nameof(registro));
        }

        public RegistroDeExecutores Registro => _registro;

        public bool Despachar<T>(T comando) where T : IComando
        {
            AoDespachar?.Invoke(comando);
            object[] executores = _registro.ExecutoresDe(comando.GetType());
            if (executores.Length == 0) return false;
            foreach (object executor in executores)
            {
                if (executor is IExecutor<T> direto) direto.Executar(comando);
                else Invocar(executor, comando);
            }
            return true;
        }

        public bool DespacharDinamico(IComando comando)
        {
            AoDespachar?.Invoke(comando);
            object[] executores = _registro.ExecutoresDe(comando.GetType());
            if (executores.Length == 0) return false;
            foreach (object executor in executores) Invocar(executor, comando);
            return true;
        }

        private void Invocar(object executor, IComando comando)
        {
            Type tipo = comando.GetType();
            if (!_metodos.TryGetValue(tipo, out MethodInfo metodo))
            {
                metodo = typeof(IExecutor<>).MakeGenericType(tipo).GetMethod(nameof(IExecutor<IComando>.Executar));
                _metodos[tipo] = metodo;
            }
            try
            {
                metodo.Invoke(executor, new object[] { comando });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                // Sem isso o erro de verdade some dentro do embrulho da reflexao.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }
    }
}
