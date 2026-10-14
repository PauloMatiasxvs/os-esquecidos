using System;
using System.Collections.Generic;
using Esquecidos.Motor.Som;
using Esquecidos.Nucleo.Comandos;
using Esquecidos.Nucleo.Mapas;
using Esquecidos.Nucleo.Partidas;
using Esquecidos.Nucleo.Roteiro;
using Esquecidos.Nucleo.Simulacao;
using UnityEngine;
using S = Esquecidos.Motor.Som.Sintese;

namespace Esquecidos.Jogo.Audio
{
    /// <summary>
    /// Todos os sons do jogo, gerados na abertura por receitas de sintese, e
    /// a trilha generativa por mapa. Um nome de som (dos dados ou do codigo)
    /// cai numa receita com parametros; nome desconhecido toca um golpe neutro.
    /// </summary>
    public sealed class Sons : MonoBehaviour,
        IExecutor<NotSom>,
        IExecutor<NotAtaqueIniciado>,
        IExecutor<NotImpacto>,
        IExecutor<NotDano>,
        IExecutor<NotMorte>,
        IExecutor<NotProjetilFim>,
        IExecutor<NotAreaExplodiu>,
        IExecutor<NotCiclo>,
        IExecutor<NotNivelSubiu>,
        IExecutor<NotMissao>,
        IExecutor<NotItemRecebido>,
        IExecutor<NotSucataMudou>,
        IExecutor<TocarSom>,
        IExecutor<TrocarMusica>,
        IExecutor<NotObjetoMudou>
    {
        private readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        private readonly List<AudioSource> _fontes = new List<AudioSource>();
        private AudioSource _ui;
        private int _proxima;
        public MotorDeMusica Musica { get; private set; }
        public float VolumeEfeitos = 0.8f;
        private readonly Dictionary<string, TemaMusical> _temas = new Dictionary<string, TemaMusical>();
        private float _ultimoPasso;

        public static Sons Criar(Transform pai)
        {
            var g = new GameObject("Sons");
            g.transform.SetParent(pai, false);
            var s = g.AddComponent<Sons>();
            s.Montar();
            return s;
        }

        private void Montar()
        {
            for (int i = 0; i < 28; i++)
            {
                var g = new GameObject("fonte" + i);
                g.transform.SetParent(transform, false);
                var a = g.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0.55f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 14;
                a.maxDistance = 55;
                a.dopplerLevel = 0;
                _fontes.Add(a);
            }
            _ui = gameObject.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0;
            var gm = new GameObject("Musica");
            gm.transform.SetParent(transform, false);
            gm.AddComponent<AudioSource>();
            Musica = gm.AddComponent<MotorDeMusica>();
            Gerar();
            Temas();
        }

        // ------------------------------------------------------------------ tocar

        public void Tocar(string nome, Vector3? onde = null, float volume = 1f, float tom = 1f)
        {
            if (string.IsNullOrEmpty(nome)) return;
            if (!_clips.TryGetValue(nome, out AudioClip[] variacoes))
            {
                if (!_clips.TryGetValue(Generico(nome), out variacoes)) return;
            }
            AudioClip c = variacoes[UnityEngine.Random.Range(0, variacoes.Length)];
            if (onde == null)
            {
                _ui.pitch = tom;
                _ui.PlayOneShot(c, volume * VolumeEfeitos);
                return;
            }
            AudioSource a = _fontes[_proxima];
            _proxima = (_proxima + 1) % _fontes.Count;
            a.transform.position = onde.Value;
            a.pitch = tom * UnityEngine.Random.Range(0.94f, 1.06f);
            a.volume = volume * VolumeEfeitos;
            a.clip = c;
            a.Play();
        }

        private static string Generico(string nome)
        {
            if (nome.EndsWith("_alerta")) return "chiado";
            if (nome.EndsWith("_rugido")) return "rugido";
            if (nome.StartsWith("usar_")) return "usar";
            return "corte";
        }

        public void UI(string nome) => Tocar(nome, null, 0.6f);

        public void Passo(Vector3 onde)
        {
            if (Time.time - _ultimoPasso < 0.22f) return;
            _ultimoPasso = Time.time;
            Tocar("passo", onde, 0.35f);
        }

        // ------------------------------------------------------------------ geracao

        private void Adicionar(string nome, Func<int, float[]> receita, int variacoes = 3, float volume = 0.9f)
        {
            var lista = new AudioClip[variacoes];
            for (int i = 0; i < variacoes; i++) lista[i] = S.Clip(nome + i, receita(i), volume);
            _clips[nome] = lista;
        }

        private void Apelido(string nome, string de)
        {
            if (_clips.TryGetValue(de, out AudioClip[] c)) _clips[nome] = c;
        }

        private void Gerar()
        {
            Adicionar("corte", i => Assobio(0.2f, 2800 + i * 300, 700, 0.6f, i));
            Adicionar("corte_duplo", i => Assobio(0.32f, 3200, 800, 0.7f, i, true));
            Adicionar("corte_fundo", i => Assobio(0.28f, 2400, 400, 0.9f, i));
            Adicionar("estocada", i => Assobio(0.16f, 1800, 900, 0.5f, i));
            Adicionar("soco", i => Impacto(0.14f, 0.4f, i));
            Adicionar("pancada", i => Assobio(0.32f, 900, 200, 0.9f, i));
            Adicionar("pancada_forte", i => Assobio(0.4f, 700, 150, 1f, i));
            Adicionar("giro", i => Assobio(0.45f, 1600, 1200, 0.8f, i, true));
            Adicionar("giro_pesado", i => Assobio(0.6f, 800, 600, 1f, i, true));
            Adicionar("rolamento", i => Ruido(0.32f, 500, 0.6f, i));
            Adicionar("disparada", i => Assobio(0.35f, 3500, 900, 0.7f, i));
            Adicionar("investida", i => Assobio(0.4f, 1200, 300, 0.9f, i));
            Adicionar("salto", i => Assobio(0.5f, 600, 1600, 0.6f, i));
            Adicionar("estilingue", i => Elastico(0.18f, i));
            Adicionar("arremesso", i => Assobio(0.22f, 2200, 1000, 0.5f, i));
            Adicionar("dardo", i => Assobio(0.15f, 4500, 2500, 0.4f, i));
            Adicionar("chuva_de_pedras", i => Elastico(0.3f, i));
            Adicionar("choque", i => Estalo(0.25f, i));
            Adicionar("taser", i => Estalo(0.35f, i));
            Adicionar("laser", i => Laser(0.22f, 1900, 300, i));
            Adicionar("laser_aliado", i => Laser(0.16f, 2600, 900, i));
            Adicionar("rajada", i => Laser(0.4f, 1500, 250, i, 3));
            Adicionar("raio", i => Laser(0.6f, 700, 120, i));
            Adicionar("tiro", i => Tiro(i));
            Adicionar("missil", i => Assobio(0.7f, 400, 1800, 0.7f, i));
            Adicionar("bomba", i => Explosao(1.0f, i));
            Adicionar("explosao", i => Explosao(1.4f, i));
            Adicionar("frasco", i => Liquido(0.35f, i));
            Adicionar("cuspe", i => Liquido(0.4f, i + 5));
            Adicionar("teia", i => Ruido(0.4f, 2500, 0.4f, i));
            Adicionar("mola", i => Mola(i));
            Adicionar("curativo", i => Ruido(0.6f, 1800, 0.35f, i));
            Adicionar("sinalizador", i => Ruido(0.9f, 3000, 0.7f, i));
            Adicionar("drone_liga", i => Laser(0.6f, 200, 900, i));
            Adicionar("vento", i => Ruido(0.6f, 1200, 0.5f, i));
            Adicionar("foco", i => Sino(0.8f, 660, i));
            Adicionar("ferro", i => Metal(0.6f, 220, i));
            Adicionar("furia", i => Rosnado(0.6f, 90, i));
            Adicionar("grito", i => Rosnado(0.7f, 140, i));
            Adicionar("terremoto", i => Explosao(1.2f, i + 3));
            Adicionar("mordida", i => Mordida(i));
            Adicionar("ferrao", i => Chiado(0.3f, 2300, i));
            Adicionar("bote", i => Chiado(0.45f, 1700, i));
            Adicionar("chiado", i => Chiado(0.5f, 2000 + i * 250, i));
            Adicionar("ninhada", i => Chiado(1.0f, 1300, i));
            Adicionar("rugido", i => Rosnado(1.3f, 60 + i * 10, i));
            Adicionar("garra", i => Assobio(0.2f, 2000, 600, 0.6f, i));
            Adicionar("grito_cinzento", i => Rosnado(0.6f, 180, i));
            Adicionar("martelo", i => Metal(0.5f, 110, i));
            Adicionar("chamado", i => Laser(0.8f, 600, 1200, i, 2));
            Adicionar("alarme", i => Sirene(1.2f, i));
            Adicionar("impacto_carne", i => Impacto(0.2f, 0.7f, i), 4);
            Adicionar("impacto_metal", i => Metal(0.3f, 400 + i * 80, i), 4);
            Adicionar("morte_bicho", i => Chiado(0.8f, 900, i));
            Adicionar("morte_maquina", i => Explosao(0.8f, i));
            Adicionar("morte_humano", i => Rosnado(0.5f, 120, i));
            Adicionar("passo", i => Ruido(0.07f, 650, 0.9f, i), 5, 0.5f);
            Adicionar("alavanca", i => Metal(0.45f, 160, i));
            Adicionar("bau", i => Ruido(0.4f, 900, 0.5f, i));
            Adicionar("usar", i => Ruido(0.3f, 1500, 0.4f, i));
            Adicionar("equipar", i => Metal(0.18f, 900, i));
            Adicionar("comprar", i => Moedas(i));
            Adicionar("vender", i => Moedas(i + 2));
            Adicionar("fabricar", i => Metal(0.5f, 300, i));
            Adicionar("sucata", i => Moedas(i));
            Adicionar("nivel", i => Arpejo(new[] { 0, 4, 7, 12, 16 }, 0.11f, 523, i), 1);
            Adicionar("missao", i => Arpejo(new[] { 0, 7, 12 }, 0.13f, 440, i), 1);
            Adicionar("missao_concluida", i => Arpejo(new[] { 0, 4, 7, 11, 14, 19 }, 0.1f, 392, i), 1);
            Adicionar("item_raro", i => Arpejo(new[] { 0, 12, 7, 19 }, 0.08f, 880, i), 1);
            Adicionar("ui_clique", i => Blip(0.04f, 1100 + i * 50), 2, 0.4f);
            Adicionar("ui_abrir", i => Blip(0.08f, 700), 1, 0.4f);
            Adicionar("ui_fechar", i => Blip(0.08f, 500), 1, 0.4f);
            Adicionar("fala", i => Blip(0.035f, 380 + i * 40), 5, 0.25f);
            Adicionar("erro", i => Blip(0.12f, 180), 1, 0.4f);
            Adicionar("sirene", i => Sirene(3.5f, i), 1);
            Adicionar("sirene_monta_cargas", i => Sirene(4f, i + 1), 1);
            Adicionar("portao", i => Ronco(3.0f, i), 1);
            Adicionar("muros", i => Ronco(3.5f, i + 2), 2);
            Adicionar("monta_cargas", i => Ronco(4f, i + 5), 1);
            Adicionar("monta_cargas_para", i => Metal(1.2f, 70, i), 1);
            Adicionar("noite", i => Rosnado(2.5f, 45, i), 1);
            Adicionar("amanhecer", i => Arpejo(new[] { 0, 7, 12, 16 }, 0.35f, 392, i), 1);
            Adicionar("coracao", i => Impacto(0.25f, 0.3f, i), 1);
            Apelido("tecelao_alerta", "chiado");
            Apelido("tecelao_rugido", "rugido");
            Apelido("drone_alerta", "chamado");
            Apelido("mech_alerta", "alarme");
            Apelido("guarda_alerta", "alarme");
            Apelido("cinzento_alerta", "grito_cinzento");
        }

        private static float R(System.Random r) => (float)r.NextDouble();

        private static float[] Assobio(float dur, float f0, float f1, float corpo, int sem, bool duplo = false)
        {
            float[] a = S.Buffer(dur);
            var n = new S.Ruido(sem + 11);
            var bp = new S.Filtro(S.Filtro.PassaFaixa, f0, 1.4f);
            var lp = new S.Filtro(S.Filtro.PassaBaixa, 300);
            var o = new S.Osc();
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float u = t / dur;
                float freq = Mathf.Lerp(f0, f1, u);
                if (i % 32 == 0) bp.Ajustar(freq, 1.6f);
                float env = duplo ? Mathf.Max(S.Env(t, 0.03f, 0.05f), S.Env(t - dur * 0.45f, 0.03f, 0.06f)) : Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * Mathf.Exp(-u * 2);
                float v = bp.Processar(n.Branco()) * 1.8f;
                v += lp.Processar(n.Branco()) * corpo * 0.6f;
                v += S.Seno(o.Avancar(freq * 0.08f)) * corpo * 0.15f;
                a[i] = v * env;
            }
            return a;
        }

        private static float[] Impacto(float dur, float brilho, int sem)
        {
            float[] a = S.Buffer(dur + 0.1f);
            var n = new S.Ruido(sem + 3);
            var lp = new S.Filtro(S.Filtro.PassaBaixa, 900 + brilho * 1500);
            var o = new S.Osc();
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float f = 50 + 110 * Mathf.Exp(-t / 0.03f);
                a[i] = S.Seno(o.Avancar(f)) * S.Env(t, 0.002f, dur * 0.5f) + lp.Processar(n.Branco()) * S.Env(t, 0.001f, 0.04f) * 1.2f;
            }
            return a;
        }

        private static float[] Metal(float dur, float f, int sem)
        {
            float[] a = S.Buffer(dur);
            var n = new S.Ruido(sem + 5);
            var c = new S.Osc();
            var m = new S.Osc();
            var c2 = new S.Osc();
            float razao = 2.76f + sem * 0.13f;
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float idx = 5 * Mathf.Exp(-t / 0.05f) + 0.6f;
                float mod = S.Seno(m.Avancar(f * razao)) * idx;
                a[i] = Mathf.Sin((c.Avancar(f) + mod * 0.15f) * 2 * Mathf.PI) * S.Env(t, 0.001f, dur * 0.35f)
                       + S.Seno(c2.Avancar(f * 3.98f)) * S.Env(t, 0.001f, dur * 0.15f) * 0.4f
                       + n.Branco() * S.Env(t, 0.0005f, 0.012f) * 0.8f;
            }
            return a;
        }

        private static float[] Estalo(float dur, int sem)
        {
            float[] a = S.Buffer(dur);
            var n = new S.Ruido(sem + 9);
            var rng = new System.Random(sem);
            var o = new S.Osc();
            var hp = new S.Filtro(S.Filtro.PassaAlta, 1500);
            float f = 300;
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                if (i % 400 == 0) f = 200 + R(rng) * 1600;
                a[i] = (S.Quadrada(o.Avancar(f)) * 0.4f + hp.Processar(n.Branco())) * S.Env(t, 0.001f, dur * 0.4f) * (R(rng) < 0.85f ? 1 : 0.2f);
            }
            return a;
        }

        private static float[] Laser(float dur, float f0, float f1, int sem, int repeticoes = 1)
        {
            float[] a = S.Buffer(dur);
            var o = new S.Osc();
            var v = new S.Osc();
            float cada = dur / repeticoes;
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float tl = t % cada;
                float f = Mathf.Lerp(f0, f1, tl / cada) * (1 + 0.02f * S.Seno(v.Avancar(30)));
                a[i] = (S.Serra(o.Avancar(f)) * 0.5f + S.Quadrada(o.Avancar(0) + 0.25f) * 0.1f) * S.Env(tl, 0.003f, cada * 0.4f);
            }
            return a;
        }

        private static float[] Tiro(int sem)
        {
            float[] a = S.Buffer(0.5f);
            var n = new S.Ruido(sem + 21);
            var lp = new S.Filtro(S.Filtro.PassaBaixa, 2200);
            var o = new S.Osc();
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                a[i] = lp.Processar(n.Branco()) * S.Env(t, 0.001f, 0.06f) * 1.4f + S.Seno(o.Avancar(70 + 200 * Mathf.Exp(-t / 0.02f))) * S.Env(t, 0.001f, 0.12f);
            }
            return a;
        }

        private static float[] Explosao(float dur, int sem)
        {
            float[] a = S.Buffer(dur);
            var n = new S.Ruido(sem + 31);
            var lp = new S.Filtro(S.Filtro.PassaBaixa, 700);
            var o = new S.Osc();
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                if (i % 64 == 0) lp.Ajustar(Mathf.Lerp(1800, 200, Mathf.Clamp01(t / dur)));
                a[i] = lp.Processar(n.Branco()) * S.Env(t, 0.004f, dur * 0.3f) * 1.6f + S.Seno(o.Avancar(40 + 60 * Mathf.Exp(-t / 0.05f))) * S.Env(t, 0.002f, 0.3f);
            }
            return a;
        }

        private static float[] Liquido(float dur, int sem)
        {
            float[] a = S.Buffer(dur);
            var n = new S.Ruido(sem + 41);
            var bp = new S.Filtro(S.Filtro.PassaFaixa, 800, 4);
            var rng = new System.Random(sem);
            var o = new S.Osc();
            float f = 400;
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                if (i % 900 == 0) f = 300 + R(rng) * 900;
                a[i] = (bp.Processar(n.Branco()) * 0.8f + S.Seno(o.Avancar(f * (1 + t * 3))) * 0.3f) * S.Env(t, 0.005f, dur * 0.35f);
            }
            return a;
        }

        private static float[] Ruido(float dur, float corte, float corpo, int sem)
        {
            float[] a = S.Buffer(dur);
            var n = new S.Ruido(sem + 51);
            var lp = new S.Filtro(S.Filtro.PassaBaixa, corte);
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                a[i] = lp.Processar(n.Branco()) * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI) * (0.4f + corpo);
            }
            return a;
        }

        private static float[] Elastico(float dur, int sem)
        {
            float[] a = S.Buffer(dur);
            var o = new S.Osc();
            var n = new S.Ruido(sem);
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float f = 180 + 300 * Mathf.Exp(-t / 0.03f);
                a[i] = S.Triangulo(o.Avancar(f)) * S.Env(t, 0.002f, 0.05f) + n.Branco() * S.Env(t, 0.001f, 0.015f) * 0.5f;
            }
            return a;
        }

        private static float[] Mola(int sem)
        {
            float[] a = S.Buffer(0.5f);
            var o = new S.Osc();
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float f = 260 * (1 + 0.3f * Mathf.Sin(t * 70) * Mathf.Exp(-t / 0.2f));
                a[i] = S.Seno(o.Avancar(f)) * S.Env(t, 0.002f, 0.15f);
            }
            return a;
        }

        private static float[] Sino(float dur, float f, int sem)
        {
            float[] a = S.Buffer(dur);
            var c = new S.Osc();
            var m = new S.Osc();
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                a[i] = Mathf.Sin((c.Avancar(f) + S.Seno(m.Avancar(f * 3.5f)) * 0.4f * Mathf.Exp(-t / 0.2f)) * 2 * Mathf.PI) * S.Env(t, 0.002f, dur * 0.4f);
            }
            return a;
        }

        private static float[] Rosnado(float dur, float f, int sem)
        {
            float[] a = S.Buffer(dur);
            var o1 = new S.Osc();
            var o2 = new S.Osc();
            var n = new S.Ruido(sem + 61);
            var lp = new S.Filtro(S.Filtro.PassaBaixa, 900);
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float am = 0.6f + 0.4f * Mathf.Sin(t * 2 * Mathf.PI * (25 + sem * 3));
                float ff = f * (1 + 0.15f * Mathf.Sin(t * 5));
                float v = S.Serra(o1.Avancar(ff)) + S.Serra(o2.Avancar(ff * 1.51f)) * 0.5f + n.Branco() * 0.4f;
                a[i] = S.Distorcer(lp.Processar(v) * am, 2) * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            }
            return a;
        }

        private static float[] Chiado(float dur, float f, int sem)
        {
            float[] a = S.Buffer(dur);
            var c = new S.Osc();
            var m = new S.Osc();
            var n = new S.Ruido(sem + 71);
            var bp = new S.Filtro(S.Filtro.PassaFaixa, f, 3);
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float vib = 1 + 0.08f * Mathf.Sin(t * 2 * Mathf.PI * 18);
                float v = Mathf.Sin((c.Avancar(f * vib) + S.Seno(m.Avancar(f * 0.5f)) * 0.3f) * 2 * Mathf.PI) * 0.5f + bp.Processar(n.Branco()) * 1.2f;
                float pulsos = 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 22));
                a[i] = v * pulsos * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            }
            return a;
        }

        private static float[] Mordida(int sem)
        {
            float[] a = S.Buffer(0.22f);
            var n = new S.Ruido(sem + 81);
            var bp = new S.Filtro(S.Filtro.PassaFaixa, 1400, 2);
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                a[i] = bp.Processar(n.Branco()) * (S.Env(t, 0.001f, 0.02f) + S.Env(t - 0.08f, 0.001f, 0.03f)) * 1.5f;
            }
            return a;
        }

        private static float[] Moedas(int sem)
        {
            float[] a = S.Buffer(0.35f);
            var rng = new System.Random(sem);
            for (int k = 0; k < 4; k++)
            {
                int ini = (int)(k * 0.06f * S.Taxa);
                float f = 2400 + R(rng) * 1800;
                var o = new S.Osc();
                for (int i = ini; i < a.Length; i++)
                {
                    float t = (i - ini) / (float)S.Taxa;
                    a[i] += S.Seno(o.Avancar(f)) * S.Env(t, 0.001f, 0.05f) * 0.5f;
                }
            }
            return a;
        }

        private static float[] Arpejo(int[] notas, float passo, float basef, int sem)
        {
            float dur = notas.Length * passo + 1.2f;
            float[] a = S.Buffer(dur);
            for (int k = 0; k < notas.Length; k++)
            {
                int ini = (int)(k * passo * S.Taxa);
                float f = basef * Mathf.Pow(2, notas[k] / 12f);
                var c = new S.Osc();
                var m = new S.Osc();
                for (int i = ini; i < a.Length; i++)
                {
                    float t = (i - ini) / (float)S.Taxa;
                    a[i] += Mathf.Sin((c.Avancar(f) + S.Seno(m.Avancar(f * 2)) * 0.2f * Mathf.Exp(-t / 0.3f)) * 2 * Mathf.PI) * S.Env(t, 0.003f, 0.5f) * 0.4f;
                }
            }
            return a;
        }

        private static float[] Blip(float dur, float f)
        {
            float[] a = S.Buffer(dur);
            var o = new S.Osc();
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                a[i] = S.Seno(o.Avancar(f * (1 + t * 2))) * S.Env(t, 0.002f, dur * 0.4f);
            }
            return a;
        }

        private static float[] Sirene(float dur, int sem)
        {
            float[] a = S.Buffer(dur);
            var o1 = new S.Osc();
            var o2 = new S.Osc();
            var lp = new S.Filtro(S.Filtro.PassaBaixa, 1400);
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float sobe = Mathf.Clamp01(t / (dur * 0.3f));
                float desce = Mathf.Clamp01((dur - t) / (dur * 0.35f));
                float f = 180 + 120 * sobe * desce + 8 * Mathf.Sin(t * 6);
                a[i] = lp.Processar(S.Serra(o1.Avancar(f)) + S.Serra(o2.Avancar(f * 1.006f))) * Mathf.Min(sobe * 3, 1) * desce;
            }
            return a;
        }

        private static float[] Ronco(float dur, int sem)
        {
            float[] a = S.Buffer(dur);
            var n = new S.Ruido(sem + 91);
            var lp = new S.Filtro(S.Filtro.PassaBaixa, 180);
            var o = new S.Osc();
            var rng = new System.Random(sem);
            for (int i = 0; i < a.Length; i++)
            {
                float t = i / (float)S.Taxa;
                float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                float tranco = R(rng) < 0.0003f ? 1 : 0;
                a[i] = (lp.Processar(n.Marrom()) * 2 + S.Seno(o.Avancar(38)) * 0.4f) * env + tranco * 0.6f;
            }
            return a;
        }

        // ------------------------------------------------------------------ musica

        private void Temas()
        {
            _temas["silencio"] = new TemaMusical { Nome = "silencio", Drone = 0, Pad = 0, Dedilhado = 0, Sino = 0, Percussao = 0, Vento = 0.15f };
            _temas["menu"] = new TemaMusical { Nome = "menu", Tonica = 45, Escala = new[] { 0, 3, 5, 7, 10 }, Progressao = new[] { 0, 5, 3, 4 }, Bpm = 58, Drone = 0.55f, Pad = 0.6f, Dedilhado = 0.3f, Sino = 0.25f, Percussao = 0, Vento = 0.35f };
            _temas["patio"] = new TemaMusical { Nome = "patio", Tonica = 50, Escala = new[] { 0, 2, 4, 7, 9 }, Progressao = new[] { 0, 3, 4, 2 }, Bpm = 66, Drone = 0.3f, Pad = 0.5f, Dedilhado = 0.45f, Sino = 0.15f, Percussao = 0.2f, Vento = 0.25f, Grilos = 1, Brilho = 1.2f };
            _temas["labirinto_hera"] = new TemaMusical { Nome = "hera", Tonica = 45, Escala = new[] { 0, 3, 5, 7, 10 }, Progressao = new[] { 0, 3, 2, 4 }, Bpm = 74, Drone = 0.55f, Pad = 0.35f, Dedilhado = 0.4f, Sino = 0.12f, Percussao = 0.8f, Vento = 0.45f, Metal = 0.3f };
            _temas["labirinto_ferrugem"] = new TemaMusical { Nome = "ferrugem", Tonica = 43, Escala = new[] { 0, 1, 5, 7, 8 }, Progressao = new[] { 0, 1, 3, 2 }, Bpm = 80, Drone = 0.65f, Pad = 0.3f, Dedilhado = 0.35f, Sino = 0.08f, Percussao = 0.9f, Vento = 0.35f, Metal = 1f, Brilho = 0.85f };
            _temas["labirinto_fosso"] = new TemaMusical { Nome = "fosso", Tonica = 40, Escala = new[] { 0, 1, 3, 6, 7 }, Progressao = new[] { 0, 2, 1, 3 }, Bpm = 62, Drone = 0.8f, Pad = 0.4f, Dedilhado = 0.25f, Sino = 0.3f, Percussao = 0.85f, Vento = 0.5f, Metal = 0.4f, Brilho = 0.7f };
            _temas["labirinto_engrenagens"] = new TemaMusical { Nome = "engrenagens", Tonica = 42, Escala = new[] { 0, 2, 3, 7, 8 }, Progressao = new[] { 0, 4, 3, 1 }, Bpm = 92, Drone = 0.55f, Pad = 0.25f, Dedilhado = 0.5f, Sino = 0.1f, Percussao = 1f, Vento = 0.3f, Metal = 1.4f };
            _temas["nucleo"] = new TemaMusical { Nome = "nucleo", Tonica = 38, Escala = new[] { 0, 2, 5, 7, 9 }, Progressao = new[] { 0, 2, 4, 1 }, Bpm = 56, Drone = 1f, Pad = 0.55f, Dedilhado = 0.2f, Sino = 0.45f, Percussao = 0.8f, Vento = 0.2f, Brilho = 0.75f };
            _temas["laboratorio"] = new TemaMusical { Nome = "laboratorio", Tonica = 47, Escala = new[] { 0, 2, 3, 7, 11 }, Progressao = new[] { 0, 3, 1, 4 }, Bpm = 84, Drone = 0.45f, Pad = 0.4f, Dedilhado = 0.35f, Sino = 0.35f, Percussao = 1f, Vento = 0.05f, Brilho = 1.3f };
        }

        public void TocarTema(string nome)
        {
            if (nome != null && _temas.TryGetValue(nome, out TemaMusical t)) Musica.Tocar(t);
        }

        public void Executar(TocarSom c) => Tocar(c.Som);

        public void Executar(TrocarMusica c) => TocarTema(c.Musica);

        // ------------------------------------------------------------------ notificacoes

        private Principal P => Principal.Instancia;

        private Vector3 Onde(Entidade e) => P.Visuais.De(e)?.PosicaoVisual ?? Util.Mundo(e.Pos);

        public void Executar(NotSom n) => Tocar(n.Som, Util.Mundo(n.Ponto, 1));

        public void Executar(NotAtaqueIniciado n)
        {
            if (n.Poder.Tipo == Nucleo.Regras.TipoPoder.Esquiva) Tocar("rolamento", Onde(n.Entidade), 0.7f);
            else if (n.Poder.Antecipacao > 0.4f && n.Entidade.Faccao == Faccao.Inimigo) Tocar(n.Entidade.Def?.Voz == "tecelao" ? "chiado" : "chamado", Onde(n.Entidade), 0.35f, 1.3f);
        }

        public void Executar(NotImpacto n)
        {
            if (n.Poder.Tipo == Nucleo.Regras.TipoPoder.Esquiva) return;
            Tocar(n.Poder.Som, Onde(n.Entidade), n.Entidade.Faccao == Faccao.Jogador ? 0.8f : 0.6f);
        }

        public void Executar(NotDano n)
        {
            if (n.Esquivou || n.Valor <= 0) return;
            if (n.Elemento == "sangramento" || n.Elemento == "veneno" && n.Fonte == null) return;
            VisualDeEntidadeTipo(n.Alvo, out bool metal);
            Tocar(metal ? "impacto_metal" : "impacto_carne", Onde(n.Alvo), n.Critico ? 1f : 0.7f, n.Critico ? 0.85f : 1f);
        }

        private void VisualDeEntidadeTipo(Entidade e, out bool metal)
        {
            var v = P.Visuais.De(e);
            metal = v is Atores.Drone || v is Atores.Mech;
        }

        public void Executar(NotMorte n)
        {
            VisualDeEntidadeTipo(n.Morto, out bool metal);
            var v = P.Visuais.De(n.Morto);
            string s = metal ? "morte_maquina" : v is Atores.Humanoide ? "morte_humano" : v is Atores.Boneco ? "pancada" : "morte_bicho";
            Tocar(s, Onde(n.Morto), n.Morto.Chefe ? 1.2f : 0.8f, n.Morto.Chefe ? 0.6f : 1);
            if (n.Morto.Chefe) Tocar("explosao", Onde(n.Morto), 1);
        }

        public void Executar(NotProjetilFim n)
        {
            if (n.Acertou) return;
            Tocar(n.Projetil.Poder.Vfx == "acido" ? "frasco" : "soco", Util.Mundo(n.Ponto, 1), 0.3f, 1.4f);
        }

        public void Executar(NotAreaExplodiu n)
        {
            string s = n.Area.Poder.Vfx == "explosao" ? "explosao" : n.Area.Poder.Vfx == "acido" ? "frasco" : n.Area.Poder.Vfx == "armadilha" ? "mola" : null;
            if (s != null) Tocar(s, Util.Mundo(n.Area.Pos, 1), 1);
        }

        public void Executar(NotCiclo n)
        {
            switch (n.Evento)
            {
                case "aviso": Tocar("sirene", null, 0.8f); break;
                case "fecharam": Tocar("portao", null, 0.6f); break;
                case "anoiteceu": Tocar("noite", null, 0.7f); break;
                case "amanheceu": Tocar("amanhecer", null, 0.6f); break;
                case "muros": Tocar("muros", null, 0.8f); break;
            }
        }

        public void Executar(NotNivelSubiu n) => Tocar("nivel", null, 0.8f);

        public void Executar(NotMissao n) => Tocar(n.Concluida ? "missao_concluida" : "missao", null, 0.6f);

        public void Executar(NotItemRecebido n)
        {
            if (n.Item.Raridade >= Nucleo.Itens.Raridade.Raro) Tocar("item_raro", null, 0.6f);
            else Tocar("bau", null, 0.25f, 1.6f);
        }

        public void Executar(NotSucataMudou n)
        {
            if (n.Delta > 0) Tocar("sucata", null, 0.35f);
        }

        public void Executar(NotObjetoMudou n)
        {
            if (n.Objeto.Tipo == TipoObjeto.Porta && n.Objeto.Modelo == "grade") Tocar("portao", Util.Mundo(n.Objeto.Pos, 2), 0.8f, 1.3f);
            else if (n.Objeto.Tipo == TipoObjeto.Porta) Tocar("portao", Util.Mundo(n.Objeto.Pos, 2), 0.7f);
        }
    }
}
