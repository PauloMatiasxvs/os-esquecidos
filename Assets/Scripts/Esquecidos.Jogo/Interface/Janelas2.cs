using System.Collections.Generic;
using System.Linq;
using Esquecidos.Nucleo.Dados;
using Esquecidos.Nucleo.Itens;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Progresso;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using UnityEngine.UI;

namespace Esquecidos.Jogo.Interface
{
    public sealed partial class Janelas
    {
        // ------------------------------------------------------------------ diario

        private void Diario()
        {
            RectTransform j = Janela("diario", "Diário", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1100, 780));
            string[] abas = { "missoes", "registros" };
            string[] nomes = { "Missões", "Registros da ARCA" };
            for (int i = 0; i < abas.Length; i++)
            {
                string aba = abas[i];
                Button b = IU.Botao(j, "aba", nomes[i], () =>
                {
                    _abaDiario = aba;
                    _missaoSelecionada = null;
                    Sujar();
                }, 20);
                b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 0.5f) * 230, -56), new Vector2(220, 40));
                if (aba == _abaDiario) b.GetComponent<Image>().color = IU.Ambar;
            }
            RectTransform lista = IU.Novo(j, "lista").Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -110), new Vector2(380, 640));
            Text detalhe = IU.Texto(j, "detalhe", "", 20, IU.CorTexto);
            detalhe.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(430, -110), new Vector2(640, 640));

            if (_abaDiario == "missoes")
            {
                Nucleo.Progresso.Diario d = Partida.Diario;
                List<string> ordem = d.Ordem.Where(id => !d.Concluida(id)).Concat(d.Ordem.Where(d.Concluida).Reverse()).ToList();
                if (_missaoSelecionada == null && ordem.Count > 0) _missaoSelecionada = ordem[0];
                float y = 0;
                foreach (string id in ordem)
                {
                    MissaoDef m = C.Missao(id);
                    bool feita = d.Concluida(id);
                    string rotulo = (feita ? "✓ " : m.Principal ? "◆ " : "◇ ") + m.Nome;
                    string mid = id;
                    Button b = IU.Botao(lista, "missao", rotulo, () =>
                    {
                        _missaoSelecionada = mid;
                        Sujar();
                    }, 18);
                    b.GetComponent<RectTransform>().Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(380, 38));
                    Text t = b.GetComponentInChildren<Text>();
                    t.alignment = TextAnchor.MiddleLeft;
                    t.rectTransform.offsetMin = new Vector2(12, 2);
                    t.color = feita ? IU.CorFraca : m.Principal ? IU.Ambar : IU.CorTexto;
                    if (mid == _missaoSelecionada) b.GetComponent<Image>().color = new Color(1.4f, 1.2f, 0.8f);
                    y -= 42;
                }
                if (_missaoSelecionada != null)
                {
                    MissaoDef m = C.Missao(_missaoSelecionada);
                    EstadoDeMissao est = d.Missoes[_missaoSelecionada];
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"<size=28><b>{m.Nome}</b></size>\n");
                    sb.Append(IU.Cor(m.Principal ? "missão principal" : "missão secundária", "#8a8070")).Append("\n\n");
                    sb.Append(IU.Cor($"<i>{m.Descricao}</i>", "#c8c0a8")).Append("\n\n");
                    for (int i = 0; i < m.Etapas.Count; i++)
                    {
                        if (i < est.Etapa || est.Concluida) sb.Append(IU.Cor("✓ " + m.Etapas[i].Texto, "#7a9070")).Append('\n');
                        else if (i == est.Etapa)
                        {
                            sb.Append(IU.Cor("▸ " + d.TextoDaEtapa(_missaoSelecionada), "#ffe0a0")).Append('\n');
                            break;
                        }
                    }
                    var recompensa = new List<string>();
                    if (m.RecompensaXp > 0) recompensa.Add($"{m.RecompensaXp} XP");
                    if (m.RecompensaSucata > 0) recompensa.Add($"{m.RecompensaSucata} de sucata");
                    foreach (string it in m.RecompensaItens) recompensa.Add(C.Item(it).Nome);
                    if (recompensa.Count > 0) sb.Append('\n').Append(IU.Cor("Recompensa: " + string.Join(", ", recompensa), "#e0c890"));
                    detalhe.text = sb.ToString();
                }
                else detalhe.text = IU.Cor("Nenhuma missão ainda.", "#8a8070");
            }
            else
            {
                List<string> lidos = Partida.RegistrosLidos.ToList();
                if (_missaoSelecionada == null && lidos.Count > 0) _missaoSelecionada = lidos[lidos.Count - 1];
                float y = 0;
                foreach (string id in lidos)
                {
                    RegistroDef r = C.Registros[id];
                    string rid = id;
                    Button b = IU.Botao(lista, "reg", r.Titulo, () =>
                    {
                        _missaoSelecionada = rid;
                        Sujar();
                    }, 18);
                    b.GetComponent<RectTransform>().Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(380, 38));
                    Text t = b.GetComponentInChildren<Text>();
                    t.alignment = TextAnchor.MiddleLeft;
                    t.rectTransform.offsetMin = new Vector2(12, 2);
                    if (rid == _missaoSelecionada) b.GetComponent<Image>().color = new Color(1.4f, 1.2f, 0.8f);
                    y -= 42;
                }
                if (_missaoSelecionada != null && C.Registros.TryGetValue(_missaoSelecionada, out RegistroDef reg))
                    detalhe.text = $"<size=28><b>{reg.Titulo}</b></size>\n{IU.Cor(reg.Autor, "#8a8070")}\n\n{reg.Texto}";
                else
                    detalhe.text = IU.Cor($"Nenhum registro lido ainda. Os terminais da ARCA ficam nos becos do labirinto. ({Partida.RegistrosLidos.Count}/{C.Registros.Count})", "#8a8070");
            }
        }

        // ------------------------------------------------------------------ mapa

        private void Mapa()
        {
            Nucleo.Mapas.Mapa m = Partida.Mapa;
            RectTransform j = Janela("mapa", _mesaDosMapas ? "Mesa dos Mapas" : m?.Nome ?? "Mapa", new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(940, 900));
            if (m == null) return;
            Grade g = m.Grade;
            var tex = new Texture2D(g.Largura, g.Altura, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[g.Largura * g.Altura];
            HashSet<int> explorado = Partida.ChaveDeExploracao() != null && Partida.Explorado.TryGetValue(Partida.ChaveDeExploracao(), out HashSet<int> e) ? e : null;
            PlantaDoLabirinto pl = m.Planta;
            bool[] alcancavel = pl != null && Partida.Jogador != null ? Alcancavel(g, Partida.Jogador.Pos) : null;
            for (int z = 0; z < g.Altura; z++)
            for (int x = 0; x < g.Largura; x++)
            {
                bool solido = g.Solido(x, z);
                bool visto = true;
                if (pl != null)
                {
                    (int cx, int cz) = pl.CelulaEm(g.Centro(x, z));
                    // Fora do labirinto so aparece o chao por onde da para andar (a entrada); o resto e o vazio atras dos muros.
                    visto = pl.Dentro(cx, cz) ? explorado != null && explorado.Contains(cz * pl.Colunas + cx) : alcancavel[z * g.Largura + x] || Vizinho(alcancavel, g, x, z);
                }
                Color32 c = !visto ? new Color32(18, 17, 16, 255) : solido ? new Color32(40, 36, 30, 255) : (m.Tema == Tema.Patio ? new Color32(84, 104, 62, 255) : new Color32(150, 142, 124, 255));
                px[z * g.Largura + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            float lado = 740;
            float esc = Mathf.Min(lado / g.LarguraMetros, lado / g.AlturaMetros);
            Vector2 tam = new Vector2(g.LarguraMetros, g.AlturaMetros) * esc;
            RawImage img = IU.Novo(j, "mapa").gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), tam);
            img.raycastTarget = false;
            Vector2 origem = img.rectTransform.anchoredPosition + new Vector2(-tam.x / 2, -tam.y);
            void Marca(System.Numerics.Vector2 p, string simbolo, Color cor, int tamanho)
            {
                Text t = IU.Texto(img.transform, "marca", simbolo, tamanho, cor, TextAnchor.MiddleCenter);
                t.rectTransform.Lugar(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(p.X * esc, p.Y * esc), new Vector2(40, 40));
            }
            // So aparece no mapa o que ja foi visto hoje.
            bool Visivel(System.Numerics.Vector2 p)
            {
                if (pl == null) return true;
                (int cx, int cz) = pl.CelulaEm(p);
                if (pl.Dentro(cx, cz)) return explorado != null && explorado.Contains(cz * pl.Colunas + cx);
                (int gx, int gz) = g.CelulaDe(p);
                return alcancavel != null && g.Dentro(gx, gz) && (alcancavel[gz * g.Largura + gx] || Vizinho(alcancavel, g, gx, gz));
            }
            foreach (ObjetoDeCena o in Partida.Mundo.Objetos)
            {
                if (!Visivel(o.Pos)) continue;
                if (o.Tipo == TipoObjeto.Saida) Marca(o.Pos, "⇧", new Color(0.4f, 0.75f, 1f), 26);
                else if (o.Tipo == TipoObjeto.Marco) Marca(o.Pos, "▲", new Color(1f, 0.7f, 0.3f), 22);
                else if (o.Tipo == TipoObjeto.Alavanca) Marca(o.Pos, o.Ligado ? "●" : "○", o.Ligado ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.55f, 0.2f), 18);
            }
            foreach (Entidade n in Partida.Mundo.Entidades.Where(x => x.NpcId != null && x.Faccao == Faccao.Neutro))
                Marca(n.Pos, "●", new Color(1f, 0.9f, 0.4f), 16);
            if (Partida.Jogador != null)
            {
                Marca(Partida.Jogador.Pos, "▲", new Color(1f, 0.95f, 0.5f), 28);
            }
            int total = pl != null ? pl.Colunas * pl.Linhas : 0;
            string rodape = pl != null ? $"Explorado hoje: {explorado?.Count ?? 0} de {total} trechos. Amanhã os muros terão mudado." : "O Pátio. Tudo que importa está aqui dentro.";
            Text info = IU.Texto(j, "info", rodape, 19, IU.CorFraca, TextAnchor.LowerCenter);
            info.rectTransform.Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 58), new Vector2(880, 30));
            if (_mesaDosMapas)
            {
                int novos = Partida.Explorado.Sum(kv => Mathf.Max(0, kv.Value.Count - (Partida.Entregue.TryGetValue(kv.Key, out int ja) ? ja : 0)));
                Button b = IU.Botao(j, "entregar", novos > 0 ? $"Entregar mapas ao Caio ({novos} trechos novos)" : "Nenhum trecho novo para entregar", () =>
                {
                    Partida.EntregarMapas();
                    Sujar();
                }, 20);
                b.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(520, 42));
                IU.Ativo(b, novos > 0);
            }
        }

        /// <summary>Celulas livres ligadas ao jogador, por inundacao.</summary>
        internal static bool[] Alcancavel(Grade g, System.Numerics.Vector2 de)
        {
            var r = new bool[g.Largura * g.Altura];
            (int x0, int z0) = g.CelulaDe(de);
            if (!g.Dentro(x0, z0)) return r;
            var fila = new Queue<int>();
            fila.Enqueue(z0 * g.Largura + x0);
            r[z0 * g.Largura + x0] = true;
            while (fila.Count > 0)
            {
                int i = fila.Dequeue();
                int x = i % g.Largura, z = i / g.Largura;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), nz = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!g.Dentro(nx, nz) || g.Solido(nx, nz)) continue;
                    int n = nz * g.Largura + nx;
                    if (r[n]) continue;
                    r[n] = true;
                    fila.Enqueue(n);
                }
            }
            return r;
        }

        /// <summary>Muro encostado em chao alcancavel tambem aparece, para o corredor ter borda.</summary>
        internal static bool Vizinho(bool[] a, Grade g, int x, int z)
        {
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (g.Dentro(x + dx, z + dz) && a[(z + dz) * g.Largura + x + dx]) return true;
            return false;
        }

        // ------------------------------------------------------------------ loja

        private void Loja()
        {
            if (_loja == null) return;
            LojaDef def = C.Lojas[_loja];
            RectTransform j = Janela("loja", def.Nome, new Vector2(0, 0.5f), new Vector2(30, 40), new Vector2(620, 860));
            j.pivot = new Vector2(0, 0.5f);
            Text ajuda = IU.Texto(j, "ajuda", "Clique para comprar. Na mochila, botão direito vende.", 17, IU.CorFraca, TextAnchor.UpperCenter);
            ajuda.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(560, 24));
            List<Item> estoque = Partida.Estoque(_loja);
            float tam = 64, pad = 5;
            for (int i = 0; i < estoque.Count && i < 70; i++)
            {
                int linha = i / 8, coluna = i % 8;
                Item item = estoque[i];
                int indice = i;
                Image s = SlotDeItem(j, new Vector2(24 + coluna * (tam + pad), -86 - linha * (tam + pad)), tam, item,
                    () =>
                    {
                        if (Partida.Comprar(_loja, indice)) _ui.P.Sons.UI("comprar");
                        else _ui.P.Sons.UI("erro");
                        Sujar();
                    },
                    () =>
                    {
                        if (Partida.Comprar(_loja, indice)) _ui.P.Sons.UI("comprar");
                        Sujar();
                    },
                    () => MostrarDica(TextoDoItem(item, true), Comparacao(item)));
                ItemBase b = C.Item(item.Base);
                Item unidade = item.Clonar();
                if (b.Empilhavel) unidade.Quantidade = 1;
                int preco = DescricaoDeItem.PrecoDeCompra(unidade, C);
                Text p = IU.Texto(s.transform, "preco", preco.ToString(), 13, preco <= Partida.Heroi.Sucata ? new Color(0.95f, 0.8f, 0.45f) : new Color(1f, 0.4f, 0.3f), TextAnchor.UpperRight);
                p.rectTransform.Esticar(3);
            }
            Text sucata = IU.Texto(j, "sucata", $"⚙ {Partida.Heroi.Sucata} de sucata", 22, new Color(0.95f, 0.8f, 0.45f), TextAnchor.LowerLeft);
            sucata.rectTransform.Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 16), new Vector2(400, 32));
        }

        // ------------------------------------------------------------------ oficina

        private void Oficina()
        {
            if (_estacao == null) return;
            string titulo = _estacao == "oficina" ? "Bancada da Oficina" : "Mesa da Enfermaria";
            RectTransform j = Janela("oficina", titulo, new Vector2(0, 0.5f), new Vector2(30, 40), new Vector2(620, 860));
            j.pivot = new Vector2(0, 0.5f);
            List<Receita> receitas = Partida.ReceitasDa(_estacao);
            if (_receitaSelecionada == null && receitas.Count > 0) _receitaSelecionada = receitas[0].Id;
            float y = -56;
            foreach (Receita r in receitas)
            {
                ItemBase b = C.Item(r.Resultado);
                string motivo = Partida.MotivoParaNaoFabricar(r);
                string rid = r.Id;
                Button bt = IU.Botao(j, "receita", $"{b.Nome}{(r.QuantidadeResultado > 1 ? " ×" + r.QuantidadeResultado : "")}", () =>
                {
                    _receitaSelecionada = rid;
                    Sujar();
                }, 18);
                bt.GetComponent<RectTransform>().Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, y), new Vector2(330, 38));
                Text t = bt.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.offsetMin = new Vector2(48, 2);
                t.color = motivo == null ? new Color(0.7f, 1f, 0.7f) : IU.CorFraca;
                Image ic = IU.Imagem(bt.transform, "icone", Color.white);
                ic.sprite = _ui.Estudio.Icone(b);
                ic.preserveAspect = true;
                ic.rectTransform.Lugar(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(34, 34));
                ic.raycastTarget = false;
                if (rid == _receitaSelecionada) bt.GetComponent<Image>().color = new Color(1.4f, 1.2f, 0.8f);
                y -= 42;
            }
            if (receitas.Count == 0)
            {
                Text nada = IU.Texto(j, "nada", "Nada para fazer aqui ainda.", 19, IU.CorFraca);
                nada.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -60), new Vector2(500, 30));
                return;
            }
            Receita sel = receitas.FirstOrDefault(r => r.Id == _receitaSelecionada) ?? receitas[0];
            ItemBase res = C.Item(sel.Resultado);
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=24><b>{res.Nome}</b></size>\n");
            sb.Append(IU.Cor(string.Join("\n", DescricaoDeItem.Linhas(new Item(res.Id) { Nivel = res.Nivel }, C)), "#b0a890")).Append("\n\nPrecisa de:\n");
            foreach ((string item, int qtd) in sel.Ingredientes)
            {
                int tem = Partida.Heroi.Mochila.Contar(item);
                sb.Append(IU.Cor($"  {C.Item(item).Nome} {tem}/{qtd}", tem >= qtd ? "#8fe08f" : "#ff7060")).Append('\n');
            }
            if (sel.Sucata > 0) sb.Append(IU.Cor($"  {sel.Sucata} de sucata ({Partida.Heroi.Sucata})", Partida.Heroi.Sucata >= sel.Sucata ? "#8fe08f" : "#ff7060")).Append('\n');
            if (sel.Nivel > 1) sb.Append(IU.Cor($"  nível {sel.Nivel}", Partida.Heroi.Nivel >= sel.Nivel ? "#8fe08f" : "#ff7060")).Append('\n');
            if (res.Equipavel) sb.Append(IU.Cor("\nSai da bancada pelo menos Reforçado.", "#9fd0ff"));
            Text det = IU.Texto(j, "detalhe", sb.ToString(), 18, IU.CorTexto);
            det.rectTransform.Lugar(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 80), new Vector2(570, 330));
            det.verticalOverflow = VerticalWrapMode.Overflow;
            det.rectTransform.pivot = new Vector2(0, 0);
            Button fab = IU.Botao(j, "fabricar", "Fabricar", () =>
            {
                Partida.Fabricar(sel);
                Sujar();
            }, 22);
            fab.GetComponent<RectTransform>().Lugar(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(240, 46));
            IU.Ativo(fab, Partida.MotivoParaNaoFabricar(sel) == null);
        }

        // ------------------------------------------------------------------ bau

        private void Guarda()
        {
            RectTransform j = Janela("guarda", "Baú do Redário", new Vector2(0, 0.5f), new Vector2(30, 40), new Vector2(620, 860));
            j.pivot = new Vector2(0, 0.5f);
            Text ajuda = IU.Texto(j, "ajuda", "Botão direito tira do baú. Na mochila, botão direito guarda.", 17, IU.CorFraca, TextAnchor.UpperCenter);
            ajuda.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(560, 24));
            float tam = 64, pad = 5;
            var g = Partida.Guarda;
            for (int i = 0; i < g.Tamanho; i++)
            {
                int linha = i / 8, coluna = i % 8;
                int indice = i;
                Item item = g.Espacos[i];
                SlotDeItem(j, new Vector2(24 + coluna * (tam + pad), -86 - linha * (tam + pad)), tam, item,
                    () =>
                    {
                        Partida.TirarDaGuarda(indice);
                        Sujar();
                    },
                    () =>
                    {
                        Partida.TirarDaGuarda(indice);
                        Sujar();
                    });
            }
        }

        // ------------------------------------------------------------------ registro

        private void Registro()
        {
            if (_registro == null || !C.Registros.TryGetValue(_registro, out RegistroDef r)) return;
            RectTransform j = Janela("registro", "Terminal da ARCA", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(860, 620));
            Image tela = IU.Painel(j, "tela", new Color(0.02f, 0.06f, 0.04f, 1));
            tela.rectTransform.Lugar(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(800, 540));
            Text t = IU.Texto(tela.transform, "texto", $"<b>{r.Titulo.ToUpperInvariant()}</b>\n<size=18>{r.Autor}</size>\n\n{r.Texto}", 22, new Color(0.55f, 1f, 0.7f));
            t.rectTransform.Lugar(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(752, 500));
            t.font = IU.Fonte;
        }
    }
}
