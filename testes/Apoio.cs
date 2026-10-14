using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Partidas;

namespace Esquecidos.Testes
{
    /// <summary>Acha os dados do jogo no repositorio e monta catalogo e partida como o Unity monta.</summary>
    public static class Apoio
    {
        private static string _raiz;

        public static string Raiz
        {
            get
            {
                if (_raiz != null) return _raiz;
                string d = AppContext.BaseDirectory;
                while (d != null && !Directory.Exists(Path.Combine(d, "Assets", "Resources", "Dados"))) d = Path.GetDirectoryName(d);
                _raiz = d ?? throw new DirectoryNotFoundException("Assets/Resources/Dados nao encontrado acima de " + AppContext.BaseDirectory);
                return _raiz;
            }
        }

        public static IEnumerable<(string nome, string conteudo)> Arquivos(string pasta)
        {
            foreach (string f in Directory.GetFiles(Path.Combine(Raiz, "Assets", "Resources", pasta), "*.txt").OrderBy(x => x, StringComparer.Ordinal))
                yield return (Path.GetFileName(f), File.ReadAllText(f));
        }

        public static Catalogo Catalogo()
        {
            var c = new Catalogo();
            foreach ((string nome, string conteudo) in Arquivos("Dados")) c.Carregar(conteudo, nome);
            return c;
        }

        public static Partida Partida(string classe = "corredor", int semente = 12345)
        {
            var p = new Partida(Catalogo(), () => Arquivos("Roteiros"));
            p.NovaPartida(classe, "Teste", null, semente);
            return p;
        }

        /// <summary>Roda a partida por N segundos em passos de 1/60, como o jogo.</summary>
        public static void Rodar(Partida p, float segundos)
        {
            int passos = (int)(segundos * 60);
            for (int i = 0; i < passos; i++) p.Passo(1f / 60f);
        }

        /// <summary>Termina roteiros que esperam a tela (falas, escolhas sempre na primeira opcao).</summary>
        public static void PularCenas(Partida p, int escolha = 0, int limite = 2000)
        {
            for (int i = 0; i < limite && p.EmCena; i++)
            {
                p.Roteiros.Escolher(escolha);
                p.Passo(1f / 30f);
            }
        }
    }
}
