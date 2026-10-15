using System;
using System.Collections.Generic;
using System.Globalization;

namespace Esquecidos.Nucleo
{
    /// <summary>
    /// Traducao no estilo do gettext: o texto em portugues e a propria chave.
    /// O jogo e escrito em portugues; os arquivos de traducao trazem pares
    ///
    ///   Texto em português
    ///   = Text in the other language
    ///
    /// separados por linha em branco. "\n" vira quebra de linha. Texto sem
    /// traducao aparece em portugues e fica anotado em <see cref="Faltando"/>.
    /// </summary>
    public static class Idioma
    {
        public const string Portugues = "pt";
        public const string Ingles = "en";

        private static readonly Dictionary<string, string> _tabela = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string Atual { get; private set; } = Portugues;
        public static bool EmPortugues => Atual == Portugues;

        /// <summary>Textos pedidos que nao tinham traducao (para o relatorio e os testes).</summary>
        public static HashSet<string> Faltando { get; } = new HashSet<string>(StringComparer.Ordinal);

        public static IReadOnlyDictionary<string, string> Tabela => _tabela;

        public static void Definir(string idioma, IEnumerable<string> arquivos = null)
        {
            Atual = idioma == Ingles ? Ingles : Portugues;
            _tabela.Clear();
            Faltando.Clear();
            if (arquivos == null || EmPortugues) return;
            foreach (string a in arquivos)
                foreach (KeyValuePair<string, string> kv in Ler(a))
                    _tabela[kv.Key] = kv.Value;
        }

        public static List<KeyValuePair<string, string>> Ler(string texto)
        {
            var pares = new List<KeyValuePair<string, string>>();
            string origem = null;
            foreach (string linha in texto.Replace("\r", "").Split('\n'))
            {
                // Sem aparar: espaco nas pontas faz parte do texto (" ou ", " de duas mãos").
                if (linha.Trim().Length == 0 || linha.StartsWith("#", StringComparison.Ordinal))
                {
                    origem = null;
                    continue;
                }
                if (linha.StartsWith("= ", StringComparison.Ordinal) || linha == "=")
                {
                    if (origem != null) pares.Add(new KeyValuePair<string, string>(origem, Desescapar(linha.Length > 2 ? linha.Substring(2) : "")));
                    origem = null;
                    continue;
                }
                origem = Desescapar(linha);
            }
            return pares;
        }

        private static string Desescapar(string s) => s.Replace("\\n", "\n");

        public static string Escapar(string s) => s.Replace("\n", "\\n");

        /// <summary>O texto no idioma atual.</summary>
        public static string T(string pt)
        {
            if (EmPortugues || string.IsNullOrEmpty(pt)) return pt;
            if (_tabela.TryGetValue(pt, out string t)) return t;
            Faltando.Add(pt);
            return pt;
        }

        /// <summary>Molde traduzido e depois preenchido: F("{0} caiu.", nome).</summary>
        public static string F(string molde, params object[] args) => string.Format(Cultura, T(molde), args);

        /// <summary>Numeros do jeito de cada lingua: 1,5 em portugues, 1.5 em ingles.</summary>
        public static CultureInfo Cultura => EmPortugues ? CultureInfo.GetCultureInfo("pt-BR") : CultureInfo.GetCultureInfo("en-US");
    }
}
