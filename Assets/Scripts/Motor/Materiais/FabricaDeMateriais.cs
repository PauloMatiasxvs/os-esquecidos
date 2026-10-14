using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Esquecidos.Motor.Materiais
{
    /// <summary>
    /// Materiais criados em codigo a partir dos shaders em Resources/Shaders,
    /// guardados por chave: dois objetos com o mesmo acabamento dividem o
    /// mesmo material e o URP agrupa as chamadas de desenho.
    /// </summary>
    public static class FabricaDeMateriais
    {
        private static readonly Dictionary<string, Shader> _shaders = new Dictionary<string, Shader>();
        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public static Shader Shader(string nome)
        {
            if (_shaders.TryGetValue(nome, out Shader s)) return s;
            s = Resources.Load<Shader>("Shaders/" + nome);
            if (s == null)
            {
                Registro.Erro($"shader {nome} nao encontrado em Resources/Shaders");
                s = UnityEngine.Shader.Find("Universal Render Pipeline/Unlit");
            }
            _shaders[nome] = s;
            return s;
        }

        /// <summary>Material guardado pela chave; a configuracao so roda na primeira vez.</summary>
        public static Material Obter(string chave, string shader, Action<Material> configurar = null)
        {
            if (_cache.TryGetValue(chave, out Material m) && m != null) return m;
            m = new Material(Shader(shader)) { name = chave, enableInstancing = true };
            configurar?.Invoke(m);
            _cache[chave] = m;
            return m;
        }

        /// <summary>Particula: aditiva (fogo, faisca, luz) ou misturada (fumaca, sangue).</summary>
        public static Material Particula(string chave, bool aditiva, int forma, float brilho = 1f)
        {
            return Obter("particula:" + chave, "Particula", m =>
            {
                m.SetFloat("_Forma", forma);
                m.SetFloat("_Brilho", brilho);
                m.SetFloat("_Origem", (float)BlendMode.SrcAlpha);
                m.SetFloat("_Destino", aditiva ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                m.renderQueue = (int)RenderQueue.Transparent + (aditiva ? 10 : 0);
            });
        }
    }
}
