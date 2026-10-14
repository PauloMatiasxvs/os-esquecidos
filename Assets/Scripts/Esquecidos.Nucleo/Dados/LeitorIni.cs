using System;
using System.Collections.Generic;
using System.Globalization;

namespace Esquecidos.Nucleo.Dados
{
    /// <summary>
    /// Um bloco [nome] de um arquivo de dados, com as chaves na ordem em que
    /// apareceram. Chave repetida e permitida e tem sentido (cada "etapa=" de
    /// uma missao, cada "bonus=" de um item), como no formato do Flare.
    /// </summary>
    public sealed class Secao
    {
        public string Nome { get; }
        public string Arquivo { get; }
        public int Linha { get; }
        public List<KeyValuePair<string, string>> Pares { get; } = new();

        public Secao(string nome, string arquivo, int linha)
        {
            Nome = nome;
            Arquivo = arquivo;
            Linha = linha;
        }

        public bool Tem(string chave) => IndiceDe(chave) >= 0;

        public string Texto(string chave, string padrao = null)
        {
            int i = IndiceDe(chave);
            return i >= 0 ? Pares[i].Value : padrao;
        }

        public string TextoObrigatorio(string chave)
        {
            string valor = Texto(chave);
            if (string.IsNullOrEmpty(valor))
                throw new FormatException($"{Arquivo}:{Linha} [{Nome}] sem '{chave}'");
            return valor;
        }

        public int Inteiro(string chave, int padrao = 0)
        {
            string v = Texto(chave);
            return v == null ? padrao : LerInteiro(v, chave);
        }

        public float Real(string chave, float padrao = 0f)
        {
            string v = Texto(chave);
            return v == null ? padrao : LerReal(v, chave);
        }

        public bool Logico(string chave, bool padrao = false)
        {
            string v = Texto(chave);
            if (v == null) return padrao;
            v = v.Trim().ToLowerInvariant();
            return v == "1" || v == "sim" || v == "true" || v == "verdadeiro";
        }

        public IEnumerable<string> Todos(string chave)
        {
            foreach (KeyValuePair<string, string> par in Pares)
                if (par.Key == chave) yield return par.Value;
        }

        /// <summary>"a,b,c" vira {"a","b","c"} sem espacos nas pontas.</summary>
        public string[] Lista(string chave)
        {
            string v = Texto(chave);
            return v == null ? Array.Empty<string>() : Partir(v);
        }

        public (float, float) Par(string chave, float padraoA = 0, float padraoB = 0)
        {
            string v = Texto(chave);
            if (v == null) return (padraoA, padraoB);
            string[] p = Partir(v);
            float a = LerReal(p[0], chave);
            float b = p.Length > 1 ? LerReal(p[1], chave) : a;
            return (a, b);
        }

        public static string[] Partir(string valor, char separador = ',')
        {
            string[] partes = valor.Split(separador);
            for (int i = 0; i < partes.Length; i++) partes[i] = partes[i].Trim();
            return partes;
        }

        public int LerInteiro(string v, string chave)
        {
            if (int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return r;
            throw new FormatException($"{Arquivo}:{Linha} [{Nome}] '{chave}={v}' nao e inteiro");
        }

        public float LerReal(string v, string chave)
        {
            if (float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r)) return r;
            throw new FormatException($"{Arquivo}:{Linha} [{Nome}] '{chave}={v}' nao e numero");
        }

        private int IndiceDe(string chave)
        {
            for (int i = 0; i < Pares.Count; i++)
                if (Pares[i].Key == chave) return i;
            return -1;
        }
    }

    /// <summary>
    /// Le o formato de dados do jogo: "# comentario", "[secao]", "chave=valor".
    /// O que vem antes da primeira secao vai para uma secao sem nome, como os
    /// arquivos de configuracao do Flare.
    /// </summary>
    public static class LeitorIni
    {
        public static List<Secao> Ler(string conteudo, string arquivo = "?")
        {
            var secoes = new List<Secao>();
            Secao atual = null;
            string[] linhas = conteudo.Replace("\r\n", "\n").Split('\n');
            for (int n = 0; n < linhas.Length; n++)
            {
                string linha = linhas[n].Trim();
                if (linha.Length == 0 || linha[0] == '#' || linha[0] == ';') continue;
                if (linha[0] == '[')
                {
                    int fim = linha.IndexOf(']');
                    if (fim < 0) throw new FormatException($"{arquivo}:{n + 1} secao sem ']'");
                    atual = new Secao(linha.Substring(1, fim - 1).Trim(), arquivo, n + 1);
                    secoes.Add(atual);
                    continue;
                }
                int igual = linha.IndexOf('=');
                if (igual <= 0) throw new FormatException($"{arquivo}:{n + 1} linha sem '=': {linha}");
                if (atual == null)
                {
                    atual = new Secao("", arquivo, n + 1);
                    secoes.Add(atual);
                }
                atual.Pares.Add(new KeyValuePair<string, string>(
                    linha.Substring(0, igual).Trim(), linha.Substring(igual + 1).Trim()));
            }
            return secoes;
        }
    }
}
