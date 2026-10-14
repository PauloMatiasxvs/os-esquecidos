// Os muros do labirinto. Tudo procedural em espaco de mundo, sem UV:
// concreto com marcas de forma e furos de amarracao, escorrido de sujeira,
// rachadura, hera que cresce mais no alto, musgo no pe e no topo, chapa
// enferrujada com rebite, ou o painel branco do laboratorio.
// O pedaco que fica entre a camera e o jogador some em pontilhado.
Shader "Esquecidos/Muro"
{
    Properties
    {
        _CorBase ("Cor do concreto", Color) = (0.55,0.53,0.5,1)
        _CorEscura ("Cor da sujeira", Color) = (0.22,0.2,0.18,1)
        _Hera ("Hera", Range(0,1)) = 0.5
        _CorHera ("Cor da hera", Color) = (0.18,0.32,0.12,1)
        _Musgo ("Musgo", Range(0,1)) = 0.4
        _Ferrugem ("Chapa enferrujada", Range(0,1)) = 0
        _Lab ("Painel de laboratorio", Range(0,1)) = 0
        _Forma ("Marcas de forma", Range(0,1)) = 1
        _Corte ("Raio do corte", Float) = 3.4
        _Emissao ("Brilho das luzes", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Esquecidos.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CorBase;
            half4 _CorEscura;
            half _Hera;
            half4 _CorHera;
            half _Musgo;
            half _Ferrugem;
            half _Lab;
            half _Forma;
            float _Corte;
            half _Emissao;
        CBUFFER_END

        void CortarNaFrente(float3 posWS, float4 positionCS)
        {
            float f = CorteNaFrenteDoJogador(posWS, _Corte);
            if (f > 0.001) clip(Bayer4(positionCS.xy) - f);
        }
        ENDHLSL

        Pass
        {
            Name "Muro"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            struct Atributos
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Atributos i)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Linha fina repetida a cada 'periodo' metros.
            float Linha(float x, float periodo, float largura)
            {
                float f = frac(x / periodo);
                float d = min(f, 1 - f) * periodo;
                return 1 - smoothstep(largura * 0.5, largura, d);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                CortarNaFrente(i.positionWS, i.positionCS);
                float3 p = i.positionWS;
                half3 n = normalize(i.normalWS);
                bool topo = n.y > 0.7;
                // Coordenada horizontal ao longo da face (x ou z conforme a normal).
                float h = abs(n.x) > abs(n.z) ? p.z : p.x;
                float altura = p.y;
                // Cada trecho de muro tem altura propria no vertice (cor.a = topo relativo).
                float alturaTotal = max(i.color.a * 30.0, 1.0);

                float4 rLarga = Ruido(float2(h, altura) * 0.045 + p.xz * 0.01);
                float4 rMedia = Ruido(float2(h, altura) * 0.23);
                float4 rFina = Ruido(float2(h, altura) * 1.7);

                // ---------------- concreto
                half3 cor = _CorBase.rgb * (0.82 + 0.3 * rLarga.r) * (0.92 + 0.16 * rFina.b);
                cor *= i.color.rgb;
                // Marcas de forma: tabuas horizontais e emendas verticais.
                float forma = Linha(altura, 1.2, 0.035) * 0.6 + Linha(h, 2.4, 0.03) * 0.5;
                // Furos de amarracao nas emendas.
                float2 furo = float2(frac(h / 1.2 + 0.5), frac(altura / 1.2 + 0.5)) - 0.5;
                forma += (1 - smoothstep(0.03, 0.05, length(furo * 1.2))) * 0.7;
                cor *= 1 - forma * 0.35 * _Forma * (1 - _Lab);
                // Escorrido: manchas verticais descendo do topo e de cada linha de forma.
                float escorrido = Ruido(float2(h * 0.35, altura * 0.02)).a;
                escorrido = smoothstep(0.45, 0.9, escorrido) * (0.4 + 0.6 * saturate(1 - (altura / alturaTotal)));
                cor = lerp(cor, _CorEscura.rgb, escorrido * 0.55);
                // Rachadura: crista do ruido vira linha escura fina.
                float crista = 1 - abs(rMedia.a * 2 - 1);
                cor *= 1 - smoothstep(0.93, 0.985, crista) * 0.7 * (1 - _Lab);
                // Sujeira no pe do muro.
                cor = lerp(cor, _CorEscura.rgb * 0.8, saturate(1 - altura / 1.4) * 0.6);

                // ---------------- chapa enferrujada
                if (_Ferrugem > 0.01)
                {
                    float2 placa = float2(h, altura) / float2(2.0, 1.6);
                    float2 fp = frac(placa);
                    float junta = 1 - smoothstep(0.0, 0.025, min(min(fp.x, 1 - fp.x), min(fp.y, 1 - fp.y)));
                    float2 rebite = min(fp, 1 - fp) * float2(2.0, 1.6);
                    float temRebite = 1 - smoothstep(0.05, 0.07, length(rebite - 0.12));
                    float tom = Ruido(floor(placa) * 0.173).b;
                    half3 metal = lerp(half3(0.36, 0.37, 0.38), half3(0.25, 0.26, 0.28), tom);
                    float ferrugem = smoothstep(0.35, 0.75, rMedia.r * 0.7 + rFina.r * 0.3 + (1 - fp.y) * 0.25);
                    metal = lerp(metal, half3(0.42, 0.2, 0.08) * (0.7 + 0.5 * rFina.b), ferrugem);
                    // Ferrugem escorrendo do rebite para baixo.
                    float escorreRebite = smoothstep(0.88, 1.0, Ruido(float2(h * 2.0, altura * 0.08)).b) * (1 - fp.y);
                    metal = lerp(metal, half3(0.35, 0.14, 0.05), escorreRebite * 0.8);
                    metal *= 1 - junta * 0.5;
                    metal += temRebite * 0.12;
                    cor = lerp(cor, metal, _Ferrugem);
                }

                // ---------------- laboratorio
                half3 emissao = 0;
                if (_Lab > 0.01)
                {
                    float junta = Linha(h, 1.6, 0.02) + Linha(altura, 1.25, 0.02);
                    half3 painel = half3(0.86, 0.88, 0.9) * (0.95 + 0.05 * rFina.b) * (1 - saturate(junta) * 0.35);
                    float faixa = 1 - smoothstep(0.03, 0.06, abs(altura - 1.05));
                    painel = lerp(painel, half3(0.4, 0.42, 0.45), saturate(1 - altura / 0.25));
                    cor = lerp(cor, painel, _Lab);
                    emissao += half3(0.25, 0.75, 1.0) * faixa * _Lab * _Emissao * (1 - topo);
                }

                // ---------------- hera
                float hera = 0;
                if (_Hera > 0.01)
                {
                    float subida = saturate(altura / alturaTotal);
                    // A hera desce do alto e sobe do chao; no meio fica mais rala.
                    float onde = rLarga.a + 0.35 * subida * subida + 0.2 * saturate(1 - altura / 2.5);
                    float limite = 1.15 - _Hera * 0.8;
                    float mascara = smoothstep(limite - 0.08, limite + 0.04, onde + rMedia.g * 0.25);
                    // Folhas: celulas do ruido Worley viram manchas de folha.
                    float4 rf = Ruido(float2(h, altura) * 0.9 + 0.31);
                    float folha = 1 - smoothstep(0.32, 0.5, rf.g);
                    hera = mascara * saturate(folha + mascara * 0.35);
                    half3 corFolha = _CorHera.rgb * (0.65 + 0.7 * rf.b) * (0.75 + 0.35 * subida);
                    corFolha *= 1 - (1 - folha) * 0.45;
                    if (topo) hera = saturate(rLarga.r * 1.6 - 0.2) * _Hera;
                    cor = lerp(cor, corFolha, hera);
                }

                // ---------------- musgo
                float musgo = topo ? 0.75 : saturate(1 - altura / 0.9) * 0.6;
                musgo *= smoothstep(0.35, 0.65, rMedia.r) * _Musgo;
                cor = lerp(cor, half3(0.17, 0.24, 0.09) * (0.8 + 0.4 * rFina.b), musgo);

                DadosDeLuz d;
                d.positionWS = p;
                // A hera e mais "fofa": normal levemente perturbada.
                d.normalWS = normalize(n + (rFina.rgb - 0.5) * hera * 0.6);
                d.viewWS = normalize(GetWorldSpaceViewDir(p));
                d.shadowCoord = TransformWorldToShadowCoord(p);
                d.uvTela = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 luz = IluminarToon(d, cor, 0.47, 0.22, 0.0, _Ferrugem * 0.4 + _Lab * 0.2);
                luz += emissao;
                luz = MixFog(luz, i.fog);
                return half4(luz, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex SombraVertice
            #pragma fragment VazioFragmento
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex ProfundidadeVertice
            #pragma fragment VazioFragmento
            #define ESQ_RECORTE_APOIO(v) CortarNaFrente(v.positionWS, v.positionCS)
            #include "Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex ProfundidadeVertice
            #pragma fragment NormaisFragmento
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define ESQ_RECORTE_APOIO(v) CortarNaFrente(v.positionWS, v.positionCS)
            #include "Passes.hlsl"
            ENDHLSL
        }
    }
}
