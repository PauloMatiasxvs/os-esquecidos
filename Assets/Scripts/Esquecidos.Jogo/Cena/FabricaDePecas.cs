using System;
using Esquecidos.Motor.Malhas;
using Esquecidos.Nucleo.Mapas;
using UnityEngine;
using Random = System.Random;

namespace Esquecidos.Jogo.Cena
{
    /// <summary>
    /// Os enfeites do cenario, cada um uma receita em codigo. As pecas olham
    /// para +X local (o angulo zero da simulacao); quem posiciona gira pelo
    /// angulo da peca. A cabana e a excecao: ela ja vem nas dimensoes do mapa,
    /// com a porta do lado pedido.
    /// </summary>
    public static class FabricaDePecas
    {
        private static readonly Color Madeira = new Color(0.42f, 0.29f, 0.18f);
        private static readonly Color MadeiraClara = new Color(0.58f, 0.44f, 0.29f);
        private static readonly Color MadeiraVelha = new Color(0.36f, 0.31f, 0.25f);
        private static readonly Color Ferro = new Color(0.36f, 0.36f, 0.38f);
        private static readonly Color Ferrugem = new Color(0.46f, 0.24f, 0.12f);
        private static readonly Color PedraCor = new Color(0.46f, 0.45f, 0.42f);
        private static readonly Color Folha = new Color(0.22f, 0.36f, 0.13f);
        private static readonly Color FolhaEscura = new Color(0.13f, 0.24f, 0.09f);
        private static readonly Color Osso = new Color(0.82f, 0.78f, 0.68f);
        private static readonly Color Carne = new Color(0.55f, 0.5f, 0.45f);

        /// <summary>Pecas pequenas e numerosas nao ganham contorno: viram textura do chao.</summary>
        public static bool SemContorno(string tipo)
        {
            switch (tipo)
            {
                case "folhas":
                case "pedra":
                case "osso":
                case "galhos":
                case "parafusos":
                case "papeis":
                case "cabo":
                case "marca_corredor":
                case "canteiro":
                case "entulho":
                case "sucata":
                    return true;
            }
            return false;
        }

        /// <summary>Escreve a peca no construtor, ja na posicao e rotacao dela.</summary>
        public static void Desenhar(ConstrutorDeMalha c, Peca p)
        {
            var rng = new Random(p.Semente == 0 ? Nucleo.Hash.Estavel(p.Tipo) + (int)(p.Pos.X * 7 + p.Pos.Y * 13) : p.Semente);
            Vector3 pos = new Vector3(p.Pos.X, 0, p.Pos.Y);
            if (p.Tipo == "cabana")
            {
                c.Empurrar(pos);
                Cabana(c, p, rng);
                c.Puxar();
                return;
            }
            c.Empurrar(pos, Quaternion.Euler(0, -p.Direcao * Mathf.Rad2Deg, 0), Vector3.one * p.Escala);
            switch (p.Tipo)
            {
                case "arvore": Arvore(c, rng, false); break;
                case "arvore_velha": Arvore(c, rng, true); break;
                case "arbusto": Arbusto(c, rng); break;
                case "fogueira": Fogueira(c, rng); break;
                case "banco": Banco(c); break;
                case "poco": Poco(c); break;
                case "canteiro": Canteiro(c, p.Variante, rng); break;
                case "cerca": Cerca(c, p.Largura); break;
                case "espantalho": Espantalho(c); break;
                case "cruz": Cruz(c, rng); break;
                case "poste": Poste(c); break;
                case "caixote": Caixote(c, rng); break;
                case "barril":
                case "tambor": Barril(c, rng, p.Tipo == "tambor"); break;
                case "pilha_sucata": PilhaDeSucata(c, rng, 1.2f); break;
                case "bancada": Bancada(c); break;
                case "mesa_remedios": MesaDeRemedios(c, rng); break;
                case "mesa_comprida": MesaComprida(c); break;
                case "fogao": Fogao(c); break;
                case "pavilhao": Pavilhao(c); break;
                case "monta_cargas": MontaCargas(c); break;
                case "folhas": Folhas(c, rng); break;
                case "pedra": Pedra(c, rng, 0.25f); break;
                case "pedra_grande": Pedra(c, rng, 0.85f); break;
                case "osso": Ossos(c, rng); break;
                case "galhos": Galhos(c, rng); break;
                case "marca_corredor": Marco(c, rng); break;
                case "carcaca": Carcaca(c, rng); break;
                case "tronco": Tronco(c, rng); break;
                case "sucata": PilhaDeSucata(c, rng, 0.35f); break;
                case "corrente": Corrente(c, rng); break;
                case "placa_metal": PlacaDeMetal(c, rng); break;
                case "cano": Cano(c, rng); break;
                case "entulho": Entulho(c, rng); break;
                case "cabo": Cabo(c, rng); break;
                case "buraco": Buraco(c, rng); break;
                case "engrenagem_chao": Engrenagem(c, 0.9f + (float)rng.NextDouble() * 0.6f, 0.18f, Ferro); break;
                case "parafusos": Parafusos(c, rng); break;
                case "pistao": Pistao(c); break;
                case "cristal": Cristal(c, rng, 0.35f); break;
                case "cristal_grande": Cristal(c, rng, 1.2f); break;
                case "painel_quebrado": PainelQuebrado(c, rng); break;
                case "coluna_dados": ColunaDeDados(c, rng); break;
                case "papeis": Papeis(c, rng); break;
                case "cadeira": Cadeira(c); break;
                case "monitor": Monitor(c); break;
                case "mesa_lab": MesaDeLab(c, rng); break;
                case "armario": Armario(c); break;
                case "tanque": Tanque(c, rng); break;
                default: Caixote(c, rng); break;
            }
            c.Puxar();
        }

        private static float R(Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        // ------------------------------------------------------------------ Patio

        private static void Cabana(ConstrutorDeMalha c, Peca p, Random rng)
        {
            float w = p.Largura, d = p.Profundidade, h = p.Altura;
            bool bunker = p.Variante == "sala_dos_mapas";
            // Lado da porta: o angulo diz para onde ela abre.
            Vector3 frente = new Vector3(Mathf.Cos(p.Direcao), 0, Mathf.Sin(p.Direcao));
            frente = Mathf.Abs(frente.x) > Mathf.Abs(frente.z) ? new Vector3(Mathf.Sign(frente.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(frente.z));
            float meiaFrente = Mathf.Abs(frente.x) > 0 ? w / 2 : d / 2;
            float larguraFrente = Mathf.Abs(frente.x) > 0 ? d : w;
            Vector3 lado = new Vector3(frente.z, 0, -frente.x);

            if (bunker)
            {
                c.Cor(new Color(0.5f, 0.49f, 0.46f)).Caixa(new Vector3(0, h / 2, 0), new Vector3(w, h, d), 0.12f);
                c.Cor(new Color(0.42f, 0.41f, 0.39f)).Caixa(new Vector3(0, h + 0.15f, 0), new Vector3(w + 0.4f, 0.3f, d + 0.4f), 0.06f);
                c.Cor(Ferro).Tubo(new Vector3(w * 0.3f, h + 0.3f, -d * 0.2f), new Vector3(w * 0.3f, h + 3.2f, -d * 0.2f), 0.05f, 0.03f, 6);
                c.Tubo(new Vector3(w * 0.3f, h + 2.6f, -d * 0.2f), new Vector3(w * 0.3f + 0.6f, h + 2.9f, -d * 0.2f), 0.02f, 0.02f, 4);
                c.Cor(new Color(1f, 0.25f, 0.1f), 0f).Esfera(new Vector3(w * 0.3f, h + 3.25f, -d * 0.2f), Vector3.one * 0.07f, 3, 5);
            }
            else
            {
                // Paredes de tabua: faixas alternando o tom da madeira.
                int tabuas = Mathf.CeilToInt(h / 0.35f);
                for (int i = 0; i < tabuas; i++)
                {
                    Color t = Color.Lerp(MadeiraVelha, MadeiraClara, (float)rng.NextDouble() * 0.6f + (i % 2) * 0.15f);
                    c.Cor(t).Caixa(new Vector3(0, i * 0.35f + 0.175f, 0), new Vector3(w, 0.34f, d), 0.03f);
                }
                c.Cor(Madeira);
                foreach (Vector3 canto in new[] { new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2), new Vector3(-w / 2, 0, d / 2), new Vector3(w / 2, 0, d / 2) })
                    c.Caixa(canto + Vector3.up * h / 2, new Vector3(0.28f, h + 0.1f, 0.28f), 0.04f);
                // Telhado de duas aguas em chapa ondulada enferrujada.
                bool cumeeiraEmX = w >= d;
                float vao = (cumeeiraEmX ? d : w) / 2 + 0.6f;
                float comp = (cumeeiraEmX ? w : d) + 1.0f;
                float alturaTelhado = Mathf.Min(2.2f, vao * 0.55f);
                for (int s = -1; s <= 1; s += 2)
                {
                    int faixas = 8;
                    for (int f = 0; f < faixas; f++)
                    {
                        Color tom = Color.Lerp(Ferrugem, new Color(0.5f, 0.45f, 0.38f), (float)rng.NextDouble() * 0.7f);
                        c.Cor(tom);
                        float a0 = f / (float)faixas, a1 = (f + 1) / (float)faixas;
                        Vector3 baixo0, baixo1, cima0, cima1;
                        if (cumeeiraEmX)
                        {
                            float x0 = -comp / 2 + a0 * comp, x1 = -comp / 2 + a1 * comp;
                            baixo0 = new Vector3(x0, h, s * vao); baixo1 = new Vector3(x1, h, s * vao);
                            cima0 = new Vector3(x0, h + alturaTelhado, 0); cima1 = new Vector3(x1, h + alturaTelhado, 0);
                        }
                        else
                        {
                            float z0 = -comp / 2 + a0 * comp, z1 = -comp / 2 + a1 * comp;
                            baixo0 = new Vector3(s * vao, h, z0); baixo1 = new Vector3(s * vao, h, z1);
                            cima0 = new Vector3(0, h + alturaTelhado, z0); cima1 = new Vector3(0, h + alturaTelhado, z1);
                        }
                        Vector3 n = Vector3.Cross(baixo1 - baixo0, cima0 - baixo0).normalized;
                        if (n.y < 0) n = -n;
                        c.Quad(baixo0, baixo1, cima1, cima0, n);
                        c.Quad(baixo0 + Vector3.down * 0.05f, cima0 + Vector3.down * 0.05f, cima1 + Vector3.down * 0.05f, baixo1 + Vector3.down * 0.05f, -n);
                    }
                }
                // Empena: triangulo fechando as pontas do telhado.
                c.Cor(MadeiraVelha);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a, b, t;
                    if (cumeeiraEmX)
                    {
                        a = new Vector3(s * w / 2, h, -vao + 0.6f); b = new Vector3(s * w / 2, h, vao - 0.6f); t = new Vector3(s * w / 2, h + alturaTelhado * 0.75f, 0);
                    }
                    else
                    {
                        a = new Vector3(-vao + 0.6f, h, s * d / 2); b = new Vector3(vao - 0.6f, h, s * d / 2); t = new Vector3(0, h + alturaTelhado * 0.75f, s * d / 2);
                    }
                    c.TrianguloChapado(a, b, t, cumeeiraEmX ? new Vector3(s, 0, 0) : new Vector3(0, 0, s));
                }
            }

            // Porta e janelas na frente.
            Vector3 face = frente * (meiaFrente + 0.02f);
            c.Cor(new Color(0.08f, 0.07f, 0.06f)).Caixa(face + Vector3.up * 1.15f, Abs(lado * 1.4f) + Abs(frente * 0.05f) + Vector3.up * 2.3f);
            c.Cor(Madeira).Caixa(face + frente * 0.03f + Vector3.up * 2.35f, Abs(lado * 1.8f) + Abs(frente * 0.1f) + Vector3.up * 0.18f, 0.02f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 janela = face + lado * s * larguraFrente * 0.3f + Vector3.up * 1.6f;
                c.Cor(new Color(1f, 0.75f, 0.4f), 0.35f).Caixa(janela, Abs(lado * 0.8f) + Abs(frente * 0.04f) + Vector3.up * 0.6f);
                c.Cor(Madeira).Caixa(janela + frente * 0.03f, Abs(lado * 0.9f) + Abs(frente * 0.06f) + Vector3.up * 0.08f);
            }
            // Detalhe de cada cabana.
            switch (p.Variante)
            {
                case "enfermaria":
                {
                    Vector3 placa = face + frente * 0.05f + Vector3.up * (h + 0.5f);
                    c.Cor(new Color(0.92f, 0.9f, 0.86f)).Caixa(placa, Abs(lado * 1.2f) + Abs(frente * 0.06f) + Vector3.up * 0.7f, 0.02f);
                    c.Cor(new Color(0.75f, 0.12f, 0.1f)).Caixa(placa + frente * 0.04f, Abs(lado * 0.5f) + Abs(frente * 0.03f) + Vector3.up * 0.16f);
                    c.Caixa(placa + frente * 0.04f, Abs(lado * 0.16f) + Abs(frente * 0.03f) + Vector3.up * 0.5f);
                    break;
                }
                case "oficina":
                    c.Cor(Ferro).Tubo(new Vector3(-w * 0.3f, h, d * 0.2f), new Vector3(-w * 0.3f, h + 3.5f, d * 0.2f), 0.18f, 0.16f, 8);
                    c.Cor(Ferrugem).Tubo(new Vector3(-w * 0.3f, h + 3.5f, d * 0.2f), new Vector3(-w * 0.3f, h + 3.7f, d * 0.2f), 0.24f, 0.24f, 8);
                    for (int i = 0; i < 3; i++)
                        c.Cor(Ferro).Tubo(face + lado * (i - 1) * 0.5f + Vector3.up * (h - 0.5f) + frente * 0.05f, face + lado * (i - 1) * 0.5f + Vector3.up * (h - 1.1f) + frente * 0.1f, 0.03f, 0.03f, 4);
                    break;
                case "redario":
                    c.Cor(new Color(0.75f, 0.68f, 0.5f));
                    for (int i = 0; i < 3; i++) c.Caixa(face + lado * (i - 1) * 2.8f + frente * 0.4f + Vector3.up * 0.25f, Abs(lado * 1.6f) + Abs(frente * 0.5f) + Vector3.up * 0.5f, 0.05f);
                    break;
                case "sala_dos_mapas":
                    c.Cor(new Color(0.9f, 0.85f, 0.7f));
                    for (int i = 0; i < 4; i++) c.Caixa(face + lado * (i - 1.5f) * 0.9f + frente * 0.02f + Vector3.up * (2.2f + (i % 2) * 0.3f), Abs(lado * 0.7f) + Abs(frente * 0.02f) + Vector3.up * 0.5f);
                    break;
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static void Arvore(ConstrutorDeMalha c, Random rng, bool velha)
        {
            float s = velha ? 2.6f : 1f;
            Color casca = velha ? new Color(0.3f, 0.24f, 0.18f) : new Color(0.34f, 0.26f, 0.19f);
            float altura = (velha ? 6.5f : 3.4f) + R(rng, -0.4f, 0.6f);
            c.Cor(casca).Tubo(Vector3.zero, new Vector3(R(rng, -0.2f, 0.2f), altura, R(rng, -0.2f, 0.2f)), 0.32f * s, 0.16f * s, 10);
            if (velha)
            {
                // Raizes que saem do chao e se espalham.
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 0.9f + R(rng, -0.2f, 0.2f);
                    Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    c.Tubo(d * 0.4f + Vector3.up * 0.8f, d * 2.4f + Vector3.down * 0.1f, 0.35f, 0.08f, 6);
                }
            }
            int galhos = velha ? 8 : 4;
            for (int i = 0; i < galhos; i++)
            {
                float a = i * 2.4f + R(rng, -0.3f, 0.3f);
                float y = altura * R(rng, 0.55f, 0.9f);
                Vector3 d = new Vector3(Mathf.Cos(a), R(rng, 0.3f, 0.7f), Mathf.Sin(a));
                Vector3 b = new Vector3(0, y, 0), f = b + d * R(rng, 1.2f, 2.0f) * s;
                c.Cor(casca).Tubo(b, f, 0.12f * s, 0.04f * s, 6);
                CopaDeFolhas(c, f, R(rng, 0.9f, 1.4f) * s, rng);
            }
            CopaDeFolhas(c, new Vector3(0, altura + 0.3f * s, 0), 1.6f * s, rng);
            if (velha)
            {
                // Cipo pendurado.
                c.Cor(FolhaEscura);
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 0.7f;
                    Vector3 topo = new Vector3(Mathf.Cos(a) * 3.2f, altura * 0.85f, Mathf.Sin(a) * 3.2f);
                    c.Tubo(topo, topo + Vector3.down * R(rng, 1.5f, 3.5f), 0.04f, 0.015f, 4);
                }
            }
        }

        private static void CopaDeFolhas(ConstrutorDeMalha c, Vector3 centro, float raio, Random rng)
        {
            int n = 5;
            for (int i = 0; i < n; i++)
            {
                Vector3 off = new Vector3(R(rng, -1, 1), R(rng, -0.4f, 0.6f), R(rng, -1, 1)) * raio * 0.5f;
                Color f = Color.Lerp(FolhaEscura, Folha, R(rng, 0.2f, 1f));
                c.Cor(f).Esfera(centro + off, new Vector3(R(rng, 0.7f, 1f), R(rng, 0.55f, 0.75f), R(rng, 0.7f, 1f)) * raio, 6, 8);
            }
        }

        private static void Arbusto(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 off = new Vector3(R(rng, -0.5f, 0.5f), R(rng, 0.2f, 0.5f), R(rng, -0.5f, 0.5f));
                c.Cor(Color.Lerp(FolhaEscura, Folha, R(rng, 0, 1))).Esfera(off, new Vector3(0.6f, 0.45f, 0.6f) * R(rng, 0.7f, 1.1f), 5, 7);
            }
        }

        private static void Fogueira(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                c.Cor(Color.Lerp(PedraCor, new Color(0.3f, 0.28f, 0.26f), R(rng, 0, 1))).Esfera(new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * 0.75f, new Vector3(0.2f, 0.15f, 0.2f), 4, 6);
            }
            c.Cor(new Color(0.12f, 0.1f, 0.08f)).Cilindro(Vector3.zero, 0.6f, 0.04f, 12);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + 0.4f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                c.Cor(new Color(0.25f, 0.17f, 0.1f)).Tubo(d * 0.5f + Vector3.up * 0.08f, -d * 0.05f + Vector3.up * 0.4f, 0.07f, 0.06f, 6);
            }
            c.Cor(new Color(1f, 0.45f, 0.1f), 0f).Esfera(new Vector3(0, 0.12f, 0), new Vector3(0.3f, 0.06f, 0.3f), 4, 8);
        }

        private static void Banco(ConstrutorDeMalha c)
        {
            c.Cor(MadeiraClara).Caixa(new Vector3(0, 0.45f, 0), new Vector3(2.5f, 0.08f, 0.45f), 0.02f);
            c.Cor(Madeira);
            c.Caixa(new Vector3(-1.0f, 0.22f, 0), new Vector3(0.1f, 0.44f, 0.38f));
            c.Caixa(new Vector3(1.0f, 0.22f, 0), new Vector3(0.1f, 0.44f, 0.38f));
        }

        private static void Poco(ConstrutorDeMalha c)
        {
            c.Cor(PedraCor).Tubo(Vector3.zero, new Vector3(0, 0.85f, 0), 1.1f, 1.05f, 16);
            c.Cor(new Color(0.03f, 0.04f, 0.05f)).Cilindro(new Vector3(0, 0.84f, 0), 0.85f, 0.02f, 16);
            c.Cor(Madeira);
            c.Caixa(new Vector3(-0.95f, 1.5f, 0), new Vector3(0.12f, 1.4f, 0.12f));
            c.Caixa(new Vector3(0.95f, 1.5f, 0), new Vector3(0.12f, 1.4f, 0.12f));
            c.Tubo(new Vector3(-1f, 1.9f, 0), new Vector3(1f, 1.9f, 0), 0.07f, 0.07f, 6);
            c.Cor(Ferrugem).Caixa(new Vector3(-0.65f, 2.35f, 0), new Vector3(1.3f, 0.05f, 1.6f), 0.01f);
            c.Caixa(new Vector3(0.65f, 2.35f, 0), new Vector3(1.3f, 0.05f, 1.6f), 0.01f);
            c.Cor(new Color(0.35f, 0.3f, 0.22f)).Tubo(new Vector3(0, 1.88f, 0), new Vector3(0, 1.3f, 0), 0.01f, 0.01f, 3);
            c.Cor(Ferro).Tubo(new Vector3(0, 1.05f, 0), new Vector3(0, 1.3f, 0), 0.13f, 0.15f, 8);
        }

        private static void Canteiro(ConstrutorDeMalha c, string variante, Random rng)
        {
            c.Cor(new Color(0.24f, 0.17f, 0.1f)).Caixa(new Vector3(0, 0.08f, 0), new Vector3(15f, 0.16f, 1.2f), 0.06f);
            for (float x = -7; x <= 7; x += 0.9f)
            {
                Vector3 b = new Vector3(x + R(rng, -0.15f, 0.15f), 0.15f, R(rng, -0.2f, 0.2f));
                switch (variante)
                {
                    case "milho":
                        c.Cor(new Color(0.42f, 0.5f, 0.2f)).Tubo(b, b + new Vector3(0, R(rng, 1.3f, 1.8f), 0), 0.035f, 0.02f, 4);
                        for (int f = 0; f < 3; f++)
                        {
                            float a = f * 2.1f + R(rng, 0, 1);
                            c.Cor(Folha).Tubo(b + Vector3.up * (0.4f + f * 0.35f), b + Vector3.up * (0.7f + f * 0.35f) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.45f, 0.03f, 0.005f, 3);
                        }
                        c.Cor(new Color(0.9f, 0.75f, 0.3f)).Esfera(b + new Vector3(0.06f, 1.0f, 0), new Vector3(0.05f, 0.12f, 0.05f), 4, 5);
                        break;
                    case "couve":
                        c.Cor(Color.Lerp(new Color(0.25f, 0.45f, 0.2f), new Color(0.35f, 0.55f, 0.3f), R(rng, 0, 1))).Esfera(b + Vector3.up * 0.15f, new Vector3(0.3f, 0.2f, 0.3f), 5, 7);
                        break;
                    default:
                        c.Cor(Folha).Esfera(b + Vector3.up * 0.05f, new Vector3(0.35f, 0.08f, 0.35f), 4, 6);
                        if (rng.NextDouble() < 0.6) c.Cor(new Color(0.9f, 0.5f, 0.12f)).Esfera(b + new Vector3(0.15f, 0.15f, 0), new Vector3(0.22f, 0.17f, 0.22f), 5, 8);
                        break;
                }
            }
        }

        private static void Cerca(ConstrutorDeMalha c, float comprimento)
        {
            int postes = Mathf.Max(2, Mathf.CeilToInt(comprimento / 1.6f) + 1);
            for (int i = 0; i < postes; i++)
            {
                float x = -comprimento / 2 + comprimento * i / (postes - 1);
                c.Cor(MadeiraVelha).Caixa(new Vector3(x, 0.55f, 0), new Vector3(0.12f, 1.1f, 0.12f), 0.02f);
            }
            c.Cor(Madeira);
            c.Caixa(new Vector3(0, 0.85f, 0), new Vector3(comprimento, 0.08f, 0.05f), 0.01f);
            c.Caixa(new Vector3(0, 0.45f, 0), new Vector3(comprimento, 0.08f, 0.05f), 0.01f);
        }

        private static void Espantalho(ConstrutorDeMalha c)
        {
            c.Cor(Madeira).Tubo(Vector3.zero, new Vector3(0, 2.1f, 0), 0.05f, 0.04f, 6);
            c.Tubo(new Vector3(-0.7f, 1.55f, 0), new Vector3(0.7f, 1.55f, 0), 0.04f, 0.04f, 6);
            c.Cor(new Color(0.55f, 0.35f, 0.3f)).Caixa(new Vector3(0, 1.35f, 0), new Vector3(0.55f, 0.65f, 0.25f), 0.05f);
            c.Cor(new Color(0.8f, 0.7f, 0.45f)).Esfera(new Vector3(0, 1.95f, 0), Vector3.one * 0.18f, 6, 8);
            c.Cor(new Color(0.3f, 0.25f, 0.15f)).Cilindro(new Vector3(0, 2.07f, 0), 0.32f, 0.03f, 10);
            c.Cilindro(new Vector3(0, 2.07f, 0), 0.15f, 0.18f, 10);
        }

        private static void Cruz(ConstrutorDeMalha c, Random rng)
        {
            c.Cor(new Color(0.3f, 0.26f, 0.2f)).Esfera(new Vector3(0, 0, -0.4f), new Vector3(0.45f, 0.18f, 0.8f), 5, 8);
            Color m = Color.Lerp(MadeiraVelha, MadeiraClara, R(rng, 0, 0.5f));
            c.Cor(m).Caixa(new Vector3(0, 0.6f, 0), new Vector3(0.09f, 1.2f, 0.07f), 0.01f);
            c.Caixa(new Vector3(0, 0.88f, 0), new Vector3(0.09f, 0.08f, 0.55f), 0.01f);
            if (rng.NextDouble() < 0.4) c.Cor(new Color(0.85f, 0.85f, 0.4f)).Esfera(new Vector3(0.05f, 0.1f, -0.3f), Vector3.one * 0.06f, 3, 4);
        }

        private static void Poste(ConstrutorDeMalha c)
        {
            c.Cor(MadeiraVelha).Tubo(Vector3.zero, new Vector3(0, 3.2f, 0), 0.1f, 0.08f, 6);
            c.Tubo(new Vector3(0, 3f, 0), new Vector3(0.6f, 3.1f, 0), 0.04f, 0.04f, 4);
            c.Cor(Ferro).Tubo(new Vector3(0.6f, 3.08f, 0), new Vector3(0.6f, 2.85f, 0), 0.12f, 0.16f, 8);
            c.Cor(new Color(1f, 0.78f, 0.45f), 0.1f).Esfera(new Vector3(0.6f, 2.8f, 0), Vector3.one * 0.09f, 4, 6);
        }

        public static void Caixote(ConstrutorDeMalha c, Random rng)
        {
            float t = R(rng, 0.9f, 1.2f);
            Color m = Color.Lerp(MadeiraClara, MadeiraVelha, R(rng, 0, 1));
            c.Cor(m).Caixa(new Vector3(0, t / 2, 0), Vector3.one * t, 0.04f);
            c.Cor(Util.Escurecer(m, 0.7f));
            for (int i = 0; i < 2; i++)
            {
                c.Caixa(new Vector3(0, t / 2, (i - 0.5f) * t * 1.002f), new Vector3(t * 1.02f, t * 0.12f, 0.03f));
                c.Caixa(new Vector3((i - 0.5f) * t * 1.002f, t / 2, 0), new Vector3(0.03f, t * 0.12f, t * 1.02f));
            }
        }

        private static void Barril(ConstrutorDeMalha c, Random rng, bool metal)
        {
            Color cor = metal ? Color.Lerp(new Color(0.25f, 0.35f, 0.5f), Ferrugem, R(rng, 0, 0.7f)) : Color.Lerp(Madeira, MadeiraClara, R(rng, 0, 1));
            c.Cor(cor).Tubo(Vector3.zero, new Vector3(0, 0.95f, 0), 0.42f, 0.42f, 12);
            c.Cor(Util.Escurecer(cor, 0.6f));
            for (int i = 0; i < 3; i++) c.Tubo(new Vector3(0, 0.15f + i * 0.33f, 0), new Vector3(0, 0.2f + i * 0.33f, 0), 0.435f, 0.435f, 12);
        }

        private static void PilhaDeSucata(ConstrutorDeMalha c, Random rng, float tamanho)
        {
            int n = (int)(8 * tamanho) + 3;
            for (int i = 0; i < n; i++)
            {
                Color cor = Color.Lerp(Ferro, Ferrugem, R(rng, 0, 1));
                Vector3 p = new Vector3(R(rng, -1, 1) * tamanho, R(rng, 0, 0.5f) * tamanho, R(rng, -1, 1) * tamanho);
                c.Empurrar(p, Quaternion.Euler(R(rng, -40, 40), R(rng, 0, 360), R(rng, -40, 40)));
                if (rng.NextDouble() < 0.5) c.Cor(cor).Caixa(Vector3.zero, new Vector3(R(rng, 0.2f, 0.8f), R(rng, 0.05f, 0.3f), R(rng, 0.2f, 0.6f)) * tamanho, 0.01f);
                else c.Cor(cor).Tubo(Vector3.zero, new Vector3(R(rng, 0.3f, 0.9f) * tamanho, 0, 0), 0.06f * tamanho, 0.06f * tamanho, 6);
                c.Puxar();
            }
        }

        private static void Bancada(ConstrutorDeMalha c)
        {
            c.Cor(MadeiraClara).Caixa(new Vector3(0, 0.9f, 0), new Vector3(3.0f, 0.1f, 1.0f), 0.02f);
            c.Cor(Madeira);
            foreach (float x in new[] { -1.4f, 1.4f })
            foreach (float z in new[] { -0.42f, 0.42f })
                c.Caixa(new Vector3(x, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f));
            c.Cor(Ferro).Caixa(new Vector3(-1.1f, 1.05f, 0.2f), new Vector3(0.25f, 0.2f, 0.18f), 0.02f);
            c.Cor(new Color(0.6f, 0.15f, 0.1f)).Caixa(new Vector3(0.6f, 1.0f, -0.1f), new Vector3(0.5f, 0.1f, 0.06f));
            c.Cor(Ferro).Tubo(new Vector3(0.2f, 0.97f, 0.2f), new Vector3(0.8f, 0.97f, 0.3f), 0.025f, 0.025f, 4);
            PilhaDeSucata(c, new Random(4), 0.25f);
        }

        private static void MesaDeRemedios(ConstrutorDeMalha c, Random rng)
        {
            c.Cor(new Color(0.85f, 0.83f, 0.78f)).Caixa(new Vector3(0, 0.85f, 0), new Vector3(2.2f, 0.08f, 0.9f), 0.02f);
            c.Cor(Madeira);
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -0.38f, 0.38f })
                c.Caixa(new Vector3(x, 0.42f, z), new Vector3(0.08f, 0.85f, 0.08f));
            for (int i = 0; i < 7; i++)
            {
                Vector3 b = new Vector3(-0.9f + i * 0.3f, 0.89f, R(rng, -0.25f, 0.25f));
                c.Cor(Color.Lerp(new Color(0.45f, 0.75f, 0.55f), new Color(0.8f, 0.5f, 0.35f), R(rng, 0, 1))).Tubo(b, b + Vector3.up * R(rng, 0.15f, 0.3f), 0.05f, 0.04f, 6);
            }
        }

        private static void MesaComprida(ConstrutorDeMalha c)
        {
            c.Cor(MadeiraClara).Caixa(new Vector3(0, 0.8f, 0), new Vector3(4.2f, 0.08f, 1.0f), 0.02f);
            c.Cor(Madeira);
            foreach (float x in new[] { -1.9f, 1.9f }) c.Caixa(new Vector3(x, 0.4f, 0), new Vector3(0.1f, 0.8f, 0.8f));
            c.Cor(MadeiraVelha);
            foreach (float z in new[] { -0.75f, 0.75f }) c.Caixa(new Vector3(0, 0.45f, z), new Vector3(4.0f, 0.06f, 0.3f), 0.01f);
            c.Cor(new Color(0.8f, 0.78f, 0.72f));
            for (int i = 0; i < 5; i++) c.Cilindro(new Vector3(-1.6f + i * 0.8f, 0.84f, 0.2f * (i % 2 == 0 ? 1 : -1)), 0.12f, 0.04f, 8);
        }

        private static void Fogao(ConstrutorDeMalha c)
        {
            c.Cor(new Color(0.55f, 0.3f, 0.22f)).Caixa(new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.9f, 0.9f), 0.04f);
            c.Cor(new Color(0.1f, 0.08f, 0.07f)).Caixa(new Vector3(0, 0.35f, 0.46f), new Vector3(0.6f, 0.35f, 0.02f));
            c.Cor(new Color(1f, 0.4f, 0.1f), 0.15f).Caixa(new Vector3(0, 0.25f, 0.465f), new Vector3(0.5f, 0.1f, 0.01f));
            c.Cor(Ferro).Tubo(new Vector3(0.35f, 0.9f, 0), new Vector3(0.35f, 1.2f, 0), 0.25f, 0.22f, 10);
            c.Tubo(new Vector3(-0.5f, 0.9f, -0.3f), new Vector3(-0.5f, 3.4f, -0.3f), 0.1f, 0.1f, 6);
        }

        private static void Pavilhao(ConstrutorDeMalha c)
        {
            c.Cor(MadeiraVelha);
            foreach (float x in new[] { -4.5f, 4.5f })
            foreach (float z in new[] { -3f, 3f })
                c.Caixa(new Vector3(x, 1.6f, z), new Vector3(0.22f, 3.2f, 0.22f), 0.03f);
            c.Cor(Madeira).Caixa(new Vector3(0, 3.2f, -3f), new Vector3(9.4f, 0.2f, 0.2f));
            c.Caixa(new Vector3(0, 3.2f, 3f), new Vector3(9.4f, 0.2f, 0.2f));
            for (int i = 0; i < 10; i++)
            {
                Color t = i % 2 == 0 ? Ferrugem : new Color(0.48f, 0.44f, 0.38f);
                c.Cor(t).Caixa(new Vector3(-4.7f + i * 1.04f, 3.45f, 0), new Vector3(1.0f, 0.06f, 7.2f), 0.01f);
            }
        }

        private static void MontaCargas(ConstrutorDeMalha c)
        {
            Color metal = new Color(0.32f, 0.33f, 0.35f);
            c.Cor(new Color(0.06f, 0.06f, 0.07f)).Caixa(new Vector3(0, 0.02f, 0), new Vector3(5.4f, 0.04f, 5.4f));
            c.Cor(new Color(0.25f, 0.25f, 0.27f));
            for (int i = 0; i < 9; i++) c.Caixa(new Vector3(-2.4f + i * 0.6f, 0.05f, 0), new Vector3(0.06f, 0.04f, 5.2f));
            c.Cor(metal);
            foreach (float x in new[] { -2.6f, 2.6f })
            foreach (float z in new[] { -2.6f, 2.6f })
                c.Caixa(new Vector3(x, 1.7f, z), new Vector3(0.22f, 3.4f, 0.22f), 0.03f);
            c.Caixa(new Vector3(0, 3.45f, -2.6f), new Vector3(5.4f, 0.18f, 0.18f));
            c.Caixa(new Vector3(0, 3.45f, 2.6f), new Vector3(5.4f, 0.18f, 0.18f));
            c.Caixa(new Vector3(-2.6f, 3.45f, 0), new Vector3(0.18f, 0.18f, 5.4f));
            c.Caixa(new Vector3(2.6f, 3.45f, 0), new Vector3(0.18f, 0.18f, 5.4f));
            // Grades nos tres lados fechados (a frente, +X, fica aberta).
            c.Cor(Util.Escurecer(metal, 0.8f));
            for (int i = 0; i < 12; i++)
            {
                float t = -2.4f + i * 0.44f;
                c.Tubo(new Vector3(t, 0.05f, -2.6f), new Vector3(t, 3.35f, -2.6f), 0.025f, 0.025f, 4);
                c.Tubo(new Vector3(t, 0.05f, 2.6f), new Vector3(t, 3.35f, 2.6f), 0.025f, 0.025f, 4);
                c.Tubo(new Vector3(-2.6f, 0.05f, t), new Vector3(-2.6f, 3.35f, t), 0.025f, 0.025f, 4);
            }
            c.Caixa(new Vector3(0, 1.7f, -2.6f), new Vector3(5.2f, 0.08f, 0.06f));
            c.Caixa(new Vector3(0, 1.7f, 2.6f), new Vector3(5.2f, 0.08f, 0.06f));
            c.Caixa(new Vector3(-2.6f, 1.7f, 0), new Vector3(0.06f, 0.08f, 5.2f));
            // Polia e correntes do guincho.
            c.Cor(Ferrugem).Caixa(new Vector3(0, 3.8f, 0), new Vector3(1.2f, 0.5f, 0.8f), 0.05f);
            c.Cor(metal).Tubo(new Vector3(0, 3.55f, -0.3f), new Vector3(0, 6.5f, -0.3f), 0.04f, 0.04f, 4);
            c.Tubo(new Vector3(0, 3.55f, 0.3f), new Vector3(0, 6.5f, 0.3f), 0.04f, 0.04f, 4);
            c.Cor(new Color(1f, 0.6f, 0.1f), 0.2f).Esfera(new Vector3(2.6f, 3.65f, 2.6f), Vector3.one * 0.1f, 3, 5);
        }

        // ------------------------------------------------------------------ labirinto

        private static void Folhas(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                Color f = Color.Lerp(new Color(0.42f, 0.28f, 0.12f), new Color(0.32f, 0.36f, 0.12f), R(rng, 0, 1));
                Vector3 p = new Vector3(R(rng, -0.8f, 0.8f), 0.02f, R(rng, -0.8f, 0.8f));
                c.Cor(f).Esfera(p, new Vector3(0.14f, 0.015f, 0.08f), 2, 5);
            }
        }

        private static void Pedra(ConstrutorDeMalha c, Random rng, float tam)
        {
            int n = tam > 0.5f ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                Color cor = Color.Lerp(PedraCor, new Color(0.35f, 0.38f, 0.3f), R(rng, 0, 0.6f));
                Vector3 p = new Vector3(R(rng, -0.4f, 0.4f) * tam, tam * 0.3f, R(rng, -0.4f, 0.4f) * tam);
                c.Cor(cor).Esfera(p, new Vector3(R(rng, 0.6f, 1f), R(rng, 0.4f, 0.7f), R(rng, 0.6f, 1f)) * tam, 4, 6);
            }
        }

        private static void Ossos(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = new Vector3(R(rng, -0.4f, 0.4f), 0.04f, R(rng, -0.4f, 0.4f));
                Vector3 b = a + new Vector3(R(rng, -0.5f, 0.5f), 0, R(rng, -0.5f, 0.5f));
                c.Cor(Osso).Capsula(a, b, 0.035f, 4);
            }
            if (rng.NextDouble() < 0.3) c.Cor(Osso).Esfera(new Vector3(0.2f, 0.1f, 0.1f), new Vector3(0.11f, 0.1f, 0.13f), 4, 6);
        }

        private static void Galhos(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = new Vector3(R(rng, -0.6f, 0.6f), 0.03f, R(rng, -0.6f, 0.6f));
                c.Cor(MadeiraVelha).Tubo(a, a + new Vector3(R(rng, -0.7f, 0.7f), 0.02f, R(rng, -0.7f, 0.7f)), 0.03f, 0.015f, 4);
            }
        }

        private static void Marco(ConstrutorDeMalha c, Random rng)
        {
            // Pilha de pedras que os Corredores deixam para marcar caminho.
            float y = 0;
            for (int i = 0; i < 4; i++)
            {
                float t = 0.22f - i * 0.04f;
                c.Cor(Color.Lerp(PedraCor, new Color(0.6f, 0.58f, 0.52f), R(rng, 0, 1))).Esfera(new Vector3(R(rng, -0.03f, 0.03f), y + t * 0.6f, 0), new Vector3(t, t * 0.6f, t), 4, 6);
                y += t * 1.1f;
            }
            c.Cor(new Color(0.8f, 0.2f, 0.12f)).Caixa(new Vector3(0, y + 0.05f, 0), new Vector3(0.04f, 0.3f, 0.02f));
        }

        private static void Carcaca(ConstrutorDeMalha c, Random rng)
        {
            Color casca = Color.Lerp(Carne, new Color(0.4f, 0.4f, 0.38f), R(rng, 0, 1));
            c.Cor(casca).Esfera(new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.32f, 0.7f), 7, 10);
            c.Cor(new Color(0.3f, 0.32f, 0.35f)).Caixa(new Vector3(0, 0.55f, 0.1f), new Vector3(0.4f, 0.08f, 0.5f), 0.03f);
            for (int i = 0; i < 6; i++)
            {
                float lado = i % 2 == 0 ? -1 : 1;
                float z = (i / 2 - 1) * 0.35f;
                Vector3 q = new Vector3(lado * 0.35f, 0.3f, z);
                Vector3 j = q + new Vector3(lado * 0.4f, 0.4f, R(rng, -0.2f, 0.2f));
                Vector3 p = j + new Vector3(-lado * 0.25f, -0.55f, R(rng, -0.2f, 0.2f));
                c.Cor(new Color(0.33f, 0.34f, 0.36f)).Tubo(q, j, 0.05f, 0.04f, 5);
                c.Tubo(j, p, 0.035f, 0.01f, 5);
            }
            c.Cor(new Color(0.12f, 0.1f, 0.1f)).Esfera(new Vector3(0, 0.35f, 0.55f), Vector3.one * 0.12f, 4, 6);
        }

        private static void Tronco(ConstrutorDeMalha c, Random rng)
        {
            c.Cor(new Color(0.3f, 0.24f, 0.17f)).Tubo(new Vector3(-1.4f, 0.3f, 0), new Vector3(1.4f, 0.25f, R(rng, -0.3f, 0.3f)), 0.32f, 0.26f, 8);
            c.Cor(Folha).Esfera(new Vector3(0.3f, 0.55f, 0), new Vector3(0.6f, 0.15f, 0.3f), 4, 6);
        }

        private static void Corrente(ConstrutorDeMalha c, Random rng)
        {
            Vector3 p = new Vector3(-0.9f, 0.04f, 0);
            for (int i = 0; i < 12; i++)
            {
                c.Empurrar(p, Quaternion.Euler(i % 2 == 0 ? 0 : 90, R(rng, -20, 20), 90));
                c.Cor(Color.Lerp(Ferro, Ferrugem, R(rng, 0, 1))).Toro(Vector3.zero, 0.06f, 0.015f, 8, 4);
                c.Puxar();
                p += new Vector3(0.13f, 0, R(rng, -0.05f, 0.05f));
            }
        }

        private static void PlacaDeMetal(ConstrutorDeMalha c, Random rng)
        {
            c.Empurrar(new Vector3(0, 0.05f, 0), Quaternion.Euler(R(rng, -8, 8), 0, R(rng, -8, 8)));
            c.Cor(Color.Lerp(Ferro, Ferrugem, R(rng, 0.2f, 0.9f))).Caixa(Vector3.zero, new Vector3(1.6f, 0.03f, 1.1f), 0.005f);
            c.Cor(Ferro);
            foreach (float x in new[] { -0.7f, 0.7f })
            foreach (float z in new[] { -0.45f, 0.45f })
                c.Esfera(new Vector3(x, 0.02f, z), Vector3.one * 0.03f, 3, 4);
            c.Puxar();
        }

        private static void Cano(ConstrutorDeMalha c, Random rng)
        {
            Color cor = Color.Lerp(Ferro, Ferrugem, R(rng, 0, 1));
            c.Cor(cor).Tubo(new Vector3(-1.2f, 0.2f, 0), new Vector3(1.2f, 0.2f, 0), 0.18f, 0.18f, 10);
            c.Cor(Util.Escurecer(cor, 0.7f)).Tubo(new Vector3(-1.25f, 0.2f, 0), new Vector3(-1.05f, 0.2f, 0), 0.23f, 0.23f, 10);
        }

        private static void Entulho(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(R(rng, -0.7f, 0.7f), 0.08f, R(rng, -0.7f, 0.7f));
                c.Empurrar(p, Quaternion.Euler(R(rng, 0, 40), R(rng, 0, 360), 0));
                c.Cor(Color.Lerp(PedraCor, new Color(0.3f, 0.3f, 0.3f), R(rng, 0, 1))).Caixa(Vector3.zero, new Vector3(R(rng, 0.15f, 0.4f), R(rng, 0.08f, 0.2f), R(rng, 0.15f, 0.4f)), 0.02f);
                c.Puxar();
            }
        }

        private static void Cabo(ConstrutorDeMalha c, Random rng)
        {
            Vector3 a = new Vector3(-1.2f, 0.04f, R(rng, -0.4f, 0.4f));
            for (int i = 0; i < 5; i++)
            {
                Vector3 b = a + new Vector3(0.5f, 0, R(rng, -0.3f, 0.3f));
                c.Cor(new Color(0.12f, 0.12f, 0.13f)).Tubo(a, b, 0.04f, 0.04f, 4);
                a = b;
            }
        }

        private static void Buraco(ConstrutorDeMalha c, Random rng)
        {
            c.Cor(new Color(0.01f, 0.01f, 0.012f)).Cilindro(new Vector3(0, 0.01f, 0), 1.55f, 0.01f, 18);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2;
                c.Cor(Color.Lerp(PedraCor, new Color(0.2f, 0.2f, 0.22f), R(rng, 0, 1))).Esfera(new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * 1.6f, new Vector3(0.3f, 0.12f, 0.3f) * R(rng, 0.7f, 1.2f), 3, 5);
            }
            c.Cor(new Color(1f, 0.35f, 0.1f), 0.4f).Cilindro(new Vector3(0, 0.0f, 0), 0.5f, 0.015f, 10);
        }

        private static void Engrenagem(ConstrutorDeMalha c, float raio, float esp, Color cor)
        {
            var contorno = new System.Collections.Generic.List<Vector2>();
            int dentes = Mathf.Max(8, (int)(raio * 14));
            for (int i = 0; i < dentes * 4; i++)
            {
                float a = i / (float)(dentes * 4) * Mathf.PI * 2;
                float r = (i % 4 == 1 || i % 4 == 2) ? raio : raio * 0.84f;
                contorno.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            c.Cor(cor).Extrusao(contorno, esp, 0);
            c.Cor(Util.Escurecer(cor, 0.6f)).Cilindro(new Vector3(0, esp, 0), raio * 0.25f, 0.06f, 10);
        }

        private static void Parafusos(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(R(rng, -0.5f, 0.5f), 0.03f, R(rng, -0.5f, 0.5f));
                c.Cor(Color.Lerp(Ferro, Ferrugem, R(rng, 0, 1))).Tubo(p, p + new Vector3(R(rng, -0.1f, 0.1f), 0, 0.12f), 0.02f, 0.02f, 4);
            }
        }

        private static void Pistao(ConstrutorDeMalha c)
        {
            c.Cor(new Color(0.4f, 0.38f, 0.34f)).Caixa(new Vector3(0, 0.4f, 0), new Vector3(1.4f, 0.8f, 1.2f), 0.06f);
            c.Cor(new Color(0.7f, 0.7f, 0.72f)).Tubo(new Vector3(0, 0.8f, 0), new Vector3(0, 2.4f, 0), 0.18f, 0.18f, 10);
            c.Cor(Ferrugem).Tubo(new Vector3(0, 2.3f, 0), new Vector3(0, 2.6f, 0), 0.4f, 0.4f, 10);
            c.Cor(new Color(1f, 0.6f, 0.15f), 0.2f).Esfera(new Vector3(0.71f, 0.6f, 0.3f), Vector3.one * 0.06f, 3, 4);
        }

        private static void Cristal(ConstrutorDeMalha c, Random rng, float tam)
        {
            int n = tam > 0.5f ? 5 : 3;
            for (int i = 0; i < n; i++)
            {
                float a = i * 2.3f;
                Vector3 b = new Vector3(Mathf.Cos(a) * 0.2f * tam, 0, Mathf.Sin(a) * 0.2f * tam);
                Vector3 topo = b + new Vector3(Mathf.Cos(a) * 0.3f, R(rng, 0.8f, 1.5f), Mathf.Sin(a) * 0.3f) * tam;
                c.Cor(new Color(0.35f, 0.95f, 1f), 0.25f).Tubo(b, topo, 0.12f * tam, 0.0f, 5, true, true);
            }
        }

        private static void PainelQuebrado(ConstrutorDeMalha c, Random rng)
        {
            c.Empurrar(new Vector3(0, 0.1f, 0), Quaternion.Euler(R(rng, -20, 20), 0, R(rng, 60, 85)));
            c.Cor(new Color(0.25f, 0.26f, 0.28f)).Caixa(Vector3.zero, new Vector3(0.9f, 0.05f, 0.6f), 0.01f);
            c.Cor(new Color(0.2f, 0.8f, 1f), 0.4f).Caixa(new Vector3(0, 0.03f, 0), new Vector3(0.7f, 0.02f, 0.45f));
            c.Puxar();
        }

        private static void ColunaDeDados(ConstrutorDeMalha c, Random rng)
        {
            c.Cor(new Color(0.16f, 0.17f, 0.19f)).Caixa(new Vector3(0, 1.4f, 0), new Vector3(0.9f, 2.8f, 0.9f), 0.04f);
            for (int i = 0; i < 12; i++)
            {
                Color luz = rng.NextDouble() < 0.3 ? new Color(1f, 0.3f, 0.2f) : new Color(0.3f, 1f, 0.9f);
                c.Cor(luz, 0.1f).Caixa(new Vector3(0.455f, 0.3f + i * 0.2f, R(rng, -0.3f, 0.3f)), new Vector3(0.02f, 0.03f, 0.12f));
            }
        }

        private static void Papeis(ConstrutorDeMalha c, Random rng)
        {
            for (int i = 0; i < 6; i++)
            {
                c.Empurrar(new Vector3(R(rng, -0.6f, 0.6f), 0.01f, R(rng, -0.6f, 0.6f)), Quaternion.Euler(0, R(rng, 0, 360), 0));
                c.Cor(new Color(0.92f, 0.9f, 0.85f)).Caixa(Vector3.zero, new Vector3(0.21f, 0.005f, 0.3f));
                c.Puxar();
            }
        }

        private static void Cadeira(ConstrutorDeMalha c)
        {
            c.Cor(new Color(0.2f, 0.22f, 0.25f)).Caixa(new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.06f, 0.5f), 0.02f);
            c.Caixa(new Vector3(-0.22f, 0.8f, 0), new Vector3(0.06f, 0.65f, 0.5f), 0.02f);
            c.Cor(new Color(0.6f, 0.62f, 0.65f)).Tubo(new Vector3(0, 0.05f, 0), new Vector3(0, 0.45f, 0), 0.04f, 0.04f, 6);
        }

        private static void Monitor(ConstrutorDeMalha c)
        {
            c.Cor(new Color(0.15f, 0.15f, 0.17f)).Caixa(new Vector3(0, 0.35f, 0), new Vector3(0.1f, 0.4f, 0.6f), 0.02f);
            c.Cor(new Color(0.2f, 0.7f, 0.9f), 0.35f).Caixa(new Vector3(0.055f, 0.37f, 0), new Vector3(0.01f, 0.32f, 0.52f));
        }

        private static void MesaDeLab(ConstrutorDeMalha c, Random rng)
        {
            c.Cor(new Color(0.88f, 0.89f, 0.9f)).Caixa(new Vector3(0, 0.9f, 0), new Vector3(1.2f, 0.06f, 2.0f), 0.02f);
            c.Cor(new Color(0.6f, 0.62f, 0.66f)).Caixa(new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.85f, 1.8f), 0.02f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 b = new Vector3(R(rng, -0.4f, 0.4f), 0.93f, R(rng, -0.8f, 0.8f));
                c.Cor(new Color(0.5f, 1f, 0.6f), 0.4f).Tubo(b, b + Vector3.up * 0.2f, 0.04f, 0.04f, 6);
            }
        }

        private static void Armario(ConstrutorDeMalha c)
        {
            c.Cor(new Color(0.7f, 0.72f, 0.75f)).Caixa(new Vector3(0, 1f, 0), new Vector3(0.6f, 2f, 1.0f), 0.02f);
            c.Cor(new Color(0.4f, 0.42f, 0.45f)).Caixa(new Vector3(0.31f, 1f, 0), new Vector3(0.01f, 1.9f, 0.02f));
        }

        private static void Tanque(ConstrutorDeMalha c, Random rng)
        {
            c.Cor(new Color(0.55f, 0.57f, 0.6f)).Tubo(Vector3.zero, new Vector3(0, 0.3f, 0), 0.6f, 0.6f, 14);
            c.Tubo(new Vector3(0, 2.2f, 0), new Vector3(0, 2.45f, 0), 0.6f, 0.55f, 14);
            c.Cor(new Color(0.3f, 0.8f, 0.75f), 0.42f).Tubo(new Vector3(0, 0.3f, 0), new Vector3(0, 2.2f, 0), 0.52f, 0.52f, 14);
            // A coisa dentro do tanque.
            c.Cor(new Color(0.45f, 0.48f, 0.45f)).Esfera(new Vector3(0, 1.2f, 0), new Vector3(0.25f, 0.4f, 0.2f), 6, 8);
        }
    }
}
