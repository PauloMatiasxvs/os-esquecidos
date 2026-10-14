using Esquecidos.Jogo.Visual;
using UnityEngine;

namespace Esquecidos.Jogo.Efeitos
{
    /// <summary>
    /// Sistemas de particula compartilhados: um para faisca, um para gota, um
    /// para poeira, um para fogo... Cada efeito do jogo e um punhado de
    /// Emit() num deles, na posicao certa. Nada de criar e destruir objeto a
    /// cada golpe.
    /// </summary>
    public static class Particulas
    {
        private static Transform _raiz;
        private static ParticleSystem _faiscas, _gotas, _poeira, _fogo, _brilhos, _anel, _mancha, _fumaca, _estilhacos;

        public static void Iniciar(Transform pai)
        {
            _raiz = new GameObject("particulas").transform;
            _raiz.SetParent(pai, false);
            _faiscas = Criar("faiscas", Paleta.Aditiva("faisca", 1, 3f), ParticleSystemRenderMode.Stretch, 1.2f, 600);
            _gotas = Criar("gotas", Paleta.Mistura("gota", 0, 1f), ParticleSystemRenderMode.Billboard, 1.6f, 600);
            _poeira = Criar("poeira", Paleta.Mistura("poeira", 0, 1f), ParticleSystemRenderMode.Billboard, -0.03f, 600);
            _fogo = Criar("fogo", Paleta.Aditiva("fogo", 0, 2.5f), ParticleSystemRenderMode.Billboard, -0.25f, 800);
            _brilhos = Criar("brilhos", Paleta.Aditiva("brilho", 0, 3f), ParticleSystemRenderMode.Billboard, -0.05f, 600);
            _anel = Criar("anel", Paleta.Aditiva("anel", 2, 2f), ParticleSystemRenderMode.HorizontalBillboard, 0, 60);
            _mancha = Criar("mancha", Paleta.Mistura("mancha", 5, 1f), ParticleSystemRenderMode.HorizontalBillboard, 0, 200);
            _fumaca = Criar("fumaca", Paleta.Mistura("fumaca", 0, 1f), ParticleSystemRenderMode.Billboard, -0.08f, 400);
            _estilhacos = Criar("estilhacos", Paleta.Toon, ParticleSystemRenderMode.Mesh, 2f, 200);
            var r = _estilhacos.GetComponent<ParticleSystemRenderer>();
            var c = new Motor.Malhas.ConstrutorDeMalha();
            c.Cor(Color.white).Caixa(Vector3.zero, new Vector3(1f, 0.6f, 0.8f), 0.1f);
            r.mesh = c.Gerar("estilhaco");
            var rot = _estilhacos.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6, 6);
            rot.y = new ParticleSystem.MinMaxCurve(-6, 6);
            rot.z = new ParticleSystem.MinMaxCurve(-6, 6);
            var colisao = _estilhacos.collision;
            colisao.enabled = true;
            colisao.type = ParticleSystemCollisionType.Planes;
            var chao = new GameObject("chao").transform;
            chao.SetParent(_raiz, false);
            colisao.AddPlane(chao);
            colisao.bounce = 0.3f;
            colisao.dampen = 0.5f;
            var colGotas = _gotas.collision;
            colGotas.enabled = true;
            colGotas.type = ParticleSystemCollisionType.Planes;
            colGotas.AddPlane(chao);
            colGotas.bounce = 0f;
            colGotas.dampen = 1f;
            colGotas.lifetimeLoss = 0.6f;
        }

        private static ParticleSystem Criar(string nome, Material mat, ParticleSystemRenderMode modo, float gravidade, int max)
        {
            var g = new GameObject(nome);
            g.transform.SetParent(_raiz, false);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravidade;
            main.startSpeed = 0;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var tam = ps.sizeOverLifetime;
            tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, nome == "anel" || nome == "fumaca" || nome == "poeira" ? 2.2f : 0.3f));
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = modo;
            if (modo == ParticleSystemRenderMode.Stretch)
            {
                r.velocityScale = 0.06f;
                r.lengthScale = 1.5f;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
            ps.Play();
            return ps;
        }

        private static void Emitir(ParticleSystem ps, Vector3 pos, Vector3 vel, Color cor, float tamanho, float vida, float rotacao = 0)
        {
            if (ps == null) return;
            var e = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startColor = cor,
                startSize = tamanho,
                startLifetime = vida,
                rotation = rotacao,
            };
            ps.Emit(e, 1);
        }

        private static Vector3 Esfera(float raio) => Random.insideUnitSphere * raio;

        public static void Faiscas(Vector3 pos, Vector3 dir, int n, Color cor, float forca = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 v = (dir.normalized * Random.Range(2f, 6f) + Esfera(3.5f) + Vector3.up * Random.Range(1f, 3f)) * forca;
                Emitir(_faiscas, pos, v, cor, Random.Range(0.04f, 0.09f), Random.Range(0.15f, 0.45f));
            }
        }

        public static void Gotas(Vector3 pos, Vector3 dir, int n, Color cor)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 v = dir.normalized * Random.Range(1f, 3.5f) + Esfera(1.8f) + Vector3.up * Random.Range(1f, 2.5f);
                Emitir(_gotas, pos, v, cor, Random.Range(0.06f, 0.14f), Random.Range(0.5f, 0.9f));
            }
            if (Random.value < 0.5f) Mancha(new Vector3(pos.x, 0.03f, pos.z) + dir.normalized * 0.6f, Random.Range(0.35f, 0.7f), cor * 0.8f, 7f);
        }

        public static void Poeira(Vector3 pos, int n, float raio = 0.5f, Color? cor = null)
        {
            Color c = cor ?? new Color(0.55f, 0.5f, 0.42f, 0.5f);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = pos + new Vector3(Random.Range(-raio, raio), 0.1f, Random.Range(-raio, raio));
                Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(0.3f, 1.2f), Random.Range(-1f, 1f));
                Emitir(_poeira, p, v, c, Random.Range(0.4f, 0.9f), Random.Range(0.6f, 1.3f));
            }
        }

        public static void Fogo(Vector3 pos, int n, float raio, Color cor)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p = pos + new Vector3(Random.Range(-raio, raio), 0, Random.Range(-raio, raio));
                Emitir(_fogo, p, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1f, 2.5f), Random.Range(-0.3f, 0.3f)), cor, Random.Range(0.25f, 0.6f), Random.Range(0.3f, 0.7f));
            }
        }

        public static void Brilhos(Vector3 pos, int n, Color cor, float raio = 0.5f, float subida = 1f)
        {
            for (int i = 0; i < n; i++)
                Emitir(_brilhos, pos + Esfera(raio), new Vector3(0, Random.Range(0.3f, 1.2f) * subida, 0) + Esfera(0.4f), cor, Random.Range(0.06f, 0.16f), Random.Range(0.4f, 0.9f));
        }

        public static void Anel(Vector3 pos, float raio, Color cor, float vida = 0.45f)
        {
            Emitir(_anel, new Vector3(pos.x, 0.06f, pos.z), Vector3.zero, cor, raio * 1.1f, vida);
        }

        public static void Mancha(Vector3 pos, float raio, Color cor, float vida)
        {
            Emitir(_mancha, new Vector3(pos.x, 0.025f + Random.value * 0.01f, pos.z), Vector3.zero, cor, raio * 2, vida, Random.Range(0, 360));
        }

        public static void Fumaca(Vector3 pos, int n, Color cor, float raio = 0.6f)
        {
            for (int i = 0; i < n; i++)
                Emitir(_fumaca, pos + Esfera(raio), new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.5f, 1.5f), Random.Range(-0.4f, 0.4f)), cor, Random.Range(0.6f, 1.4f), Random.Range(0.8f, 1.8f));
        }

        public static void Estilhacos(Vector3 pos, int n, Color cor, float forca = 1)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 v = (Esfera(3f) + Vector3.up * Random.Range(2f, 5f)) * forca;
                Emitir(_estilhacos, pos + Esfera(0.3f), v, cor, Random.Range(0.06f, 0.16f), Random.Range(1.2f, 2.2f));
            }
        }

        /// <summary>Explosao: clarao, anel no chao, fumaca, faisca e estilhaco.</summary>
        public static void Explosao(Vector3 pos, float raio, Color cor)
        {
            Brilhos(pos + Vector3.up * 0.5f, 10, cor, raio * 0.4f, 2);
            Fogo(pos, 18, raio * 0.4f, cor);
            Faiscas(pos + Vector3.up * 0.3f, Vector3.up, 24, Color.Lerp(cor, Color.white, 0.4f), 1.4f);
            Anel(pos, raio, cor, 0.5f);
            Fumaca(pos + Vector3.up * 0.6f, 10, new Color(0.18f, 0.16f, 0.15f, 0.7f), raio * 0.4f);
            Mancha(pos, raio * 0.6f, new Color(0.05f, 0.04f, 0.04f, 0.8f), 12f);
        }

        // ------------------------------------------------------------------ sistemas presos a objetos

        /// <summary>Poeira continua presa num objeto (portao andando, muro subindo). Toca com Play().</summary>
        public static ParticleSystem Poeira(Transform pai, Vector3 local, Vector3 caixa)
        {
            var g = new GameObject("poeira");
            g.transform.SetParent(pai, false);
            g.transform.localPosition = local;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startColor = new Color(0.55f, 0.52f, 0.46f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = Mathf.Clamp(caixa.x * caixa.z * 3, 8, 70);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = caixa;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var tam = ps.sizeOverLifetime;
            tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2f));
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Paleta.Mistura("poeira", 0, 1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Fogo continuo (fogueira, sinalizador, queimadura). Ja comeca tocando.</summary>
        public static ParticleSystem FogoContinuo(Transform pai, Vector3 local, float raio, Color cor, float taxa = 30)
        {
            var g = new GameObject("fogo");
            g.transform.SetParent(pai, false);
            g.transform.localPosition = local;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 1;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * raio * 2, 0.55f * raio * 2);
            main.startColor = cor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.2f;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = taxa;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = raio;
            shape.rotation = new Vector3(-90, 0, 0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 0.6f), new GradientColorKey(new Color(0.3f, 0.1f, 0.05f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var tam = ps.sizeOverLifetime;
            tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Paleta.Aditiva("fogo", 0, 2.5f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>Particulas continuas de um efeito de estado (veneno subindo, estrelas de atordoado...).</summary>
        public static ParticleSystem Continuo(Transform pai, Vector3 local, Color cor, float taxa, float raio, float subida, float tamanho, bool aditivo, float gravidade = 0)
        {
            var g = new GameObject("efeito");
            g.transform.SetParent(pai, false);
            g.transform.localPosition = local;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 1;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(subida * 0.5f, subida);
            main.startSize = new ParticleSystem.MinMaxCurve(tamanho * 0.6f, tamanho);
            main.startColor = cor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravidade;
            main.maxParticles = 100;
            var em = ps.emission;
            em.rateOverTime = taxa;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = raio;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = aditivo ? Paleta.Aditiva("brilho", 0, 3f) : Paleta.Mistura("gota", 0, 1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        /// <summary>Facho vertical de luz: item raro no chao, coletavel de missao.</summary>
        public static Transform Facho(Transform pai, Color cor, float altura = 3.5f, float largura = 0.5f)
        {
            var c = new Motor.Malhas.ConstrutorDeMalha();
            c.Cor(cor);
            // Dois quads cruzados com UV vertical: a forma 4 do shader apaga para cima e nas bordas.
            for (int i = 0; i < 2; i++)
            {
                Vector3 lado = i == 0 ? Vector3.right * largura : Vector3.forward * largura;
                c.QuadUv(-lado, lado, lado + Vector3.up * altura, -lado + Vector3.up * altura);
            }
            var g = new GameObject("facho");
            g.transform.SetParent(pai, false);
            var mr = Util.ComMalha(g, c.Gerar("facho"), Paleta.Aditiva("facho", 4, 2.2f), false);
            mr.receiveShadows = false;
            return g.transform;
        }
    }
}
