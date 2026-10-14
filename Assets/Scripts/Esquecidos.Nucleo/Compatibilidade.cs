// O Unity compila com .NET Standard 2.1, que nao traz o tipo que o C# 9 usa
// para os "record". Declarado aqui, os records do nucleo funcionam nos dois
// lugares: no Unity e no projeto de testes.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
