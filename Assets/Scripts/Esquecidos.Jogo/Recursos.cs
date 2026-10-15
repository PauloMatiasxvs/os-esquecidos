using System.Collections.Generic;
using System.Linq;
using Esquecidos.Motor;
using Esquecidos.Nucleo;
using Esquecidos.Nucleo.Dados;
using UnityEngine;

namespace Esquecidos.Jogo
{
    /// <summary>Le os arquivos de dados e roteiros de Resources e monta o catalogo.</summary>
    public static class Recursos
    {
        public static IEnumerable<(string nome, string conteudo)> Textos(string pasta)
        {
            return Resources.LoadAll<TextAsset>(pasta)
                .OrderBy(t => t.name, System.StringComparer.Ordinal)
                .Select(t => (t.name + ".txt", t.text))
                .ToList();
        }

        /// <summary>Liga o idioma e carrega as traducoes dele (Resources/Traducoes/en/*.txt).</summary>
        public static void AplicarIdioma(string idioma)
        {
            Idioma.Definir(idioma, idioma == Idioma.Portugues ? null : Textos("Traducoes/" + idioma).Select(t => t.conteudo));
            Registro.Info($"idioma: {Idioma.Atual}, {Idioma.Tabela.Count} textos traduzidos");
        }

        public static Catalogo Catalogo()
        {
            var c = new Catalogo();
            foreach ((string nome, string conteudo) in Textos("Dados")) c.Carregar(conteudo, nome);
            c.Traduzir();
            Registro.Info($"catalogo: {c.Itens.Count} itens, {c.Poderes.Count} poderes, {c.Inimigos.Count} inimigos, {c.Missoes.Count} missoes");
            return c;
        }
    }
}
