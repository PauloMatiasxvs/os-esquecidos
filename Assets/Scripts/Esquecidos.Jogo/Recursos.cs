using System.Collections.Generic;
using System.Linq;
using Esquecidos.Motor;
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

        public static Catalogo Catalogo()
        {
            var c = new Catalogo();
            foreach ((string nome, string conteudo) in Textos("Dados")) c.Carregar(conteudo, nome);
            Registro.Info($"catalogo: {c.Itens.Count} itens, {c.Poderes.Count} poderes, {c.Inimigos.Count} inimigos, {c.Missoes.Count} missoes");
            return c;
        }
    }
}
