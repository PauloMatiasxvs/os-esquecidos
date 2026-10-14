// Personagens, bichos e objetos. A cor vem do vertice (cada modelo e uma
// malha so, montada em codigo); alfa do vertice abaixo de 0.5 marca partes que
// brilham sozinhas (olhos, telas, luzes). Contorno por casco invertido usando a
// normal suavizada guardada no UV3.
Shader "Esquecidos/Toon"
{
    Properties
    {
        _Cor ("Tinta", Color) = (1,1,1,1)
        _Emissao ("Intensidade do brilho", Float) = 2.5
        _Corte ("Corte da luz", Range(0,1)) = 0.52
        _Suavidade ("Suavidade da faixa", Range(0.001,0.5)) = 0.06
        _Borda ("Borda de luz", Range(0,2)) = 0.6
        _Brilho ("Brilho especular", Range(0,2)) = 0.3
        _Detalhe ("Sujeira triplanar", Range(0,1)) = 0.25
        _EscalaDetalhe ("Escala da sujeira", Float) = 0.35
        _Contorno ("Contorno (pixels)", Float) = 1.6
        _CorContorno ("Cor do contorno", Color) = (0.06,0.05,0.05,1)
        _Flash ("Clarao de golpe", Range(0,1)) = 0
        _CorFlash ("Cor do clarao", Color) = (1,1,1,1)
        _Some ("Dissolver", Range(0,1)) = 0
        _CorteJogador ("Raio do corte na frente do jogador", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Esquecidos.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            half _Emissao;
            half _Corte;
            half _Suavidade;
            half _Borda;
            half _Brilho;
            half _Detalhe;
            float _EscalaDetalhe;
            float _Contorno;
            half4 _CorContorno;
            half _Flash;
            half4 _CorFlash;
            half _Some;
            float _CorteJogador;
        CBUFFER_END

        // Dissolve com o ruido: some em manchas, nao de uma vez.
        void Dissolver(float3 posWS)
        {
            if (_Some > 0.001)
            {
                float r = RuidoLod(posWS.xz * 0.9 + posWS.y * 0.37).r;
                clip(r - _Some * 1.05);
            }
        }

        // Copa de arvore e telhado entre a camera e o jogador somem em pontilhado, como o muro.
        void CortarNaFrente(float3 posWS, float4 positionCS)
        {
            if (_CorteJogador <= 0) return;
            float f = CorteNaFrenteDoJogador(posWS, _CorteJogador, 1.0);
            if (f > 0.001) clip(Bayer4(positionCS.xy) - f);
        }
        ENDHLSL

        Pass
        {
            Name "Toon"
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
            #pragma multi_compile_instancing

            struct Atributos
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Atributos i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Dissolver(i.positionWS);
                CortarNaFrente(i.positionWS, i.positionCS);
                half3 n = normalize(i.normalWS);
                half3 albedo = i.color.rgb * _Cor.rgb;
                // Sujeira e variacao: tira a cara de plastico das cores chapadas.
                float4 r = RuidoTriplanar(i.positionWS, n, _EscalaDetalhe);
                albedo *= lerp(1.0, 0.72 + 0.5 * r.r * (0.6 + 0.4 * r.b), _Detalhe);

                DadosDeLuz d;
                d.positionWS = i.positionWS;
                d.normalWS = n;
                d.viewWS = normalize(GetWorldSpaceViewDir(i.positionWS));
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.uvTela = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 cor = IluminarToon(d, albedo, _Corte, _Suavidade, _Borda, _Brilho);

                // Alfa do vertice baixo = parte que brilha (olho, tela, lampada).
                half brilha = saturate(1 - i.color.a * 2);
                cor = lerp(cor, i.color.rgb * _Cor.rgb * _Emissao, brilha);
                cor = lerp(cor, _CorFlash.rgb * 1.6, _Flash);
                // Borda queimando enquanto dissolve.
                if (_Some > 0.001)
                {
                    float rr = RuidoLod(i.positionWS.xz * 0.9 + i.positionWS.y * 0.37).r;
                    cor += half3(1.0, 0.45, 0.15) * 3 * saturate(1 - (rr - _Some * 1.05) * 12);
                }
                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Contorno"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Atributos
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float3 suave : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Atributos i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 nOS = dot(i.suave, i.suave) > 0.01 ? i.suave : i.normalOS;
                float4 cs = TransformObjectToHClip(i.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(nOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float l = length(nCS);
                if (l > 1e-5)
                {
                    // Largura constante em pixels, um pouco mais fina de longe.
                    float2 desloc = nCS / l * _Contorno * 2.0 / _ScreenParams.xy * cs.w;
                    desloc *= saturate(14.0 / max(cs.w, 1.0)) * 0.5 + 0.5;
                    cs.xy += desloc;
                }
                o.positionCS = cs;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.color = i.color;
                o.fog = ComputeFogFactor(cs.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Dissolver(i.positionWS);
                CortarNaFrente(i.positionWS, i.positionCS);
                if (_Contorno <= 0.01) clip(-1);
                half3 cor = lerp(_CorContorno.rgb, i.color.rgb * 0.18, 0.35);
                // Parte que brilha nao ganha contorno escuro.
                if (i.color.a < 0.5) cor = i.color.rgb * _Cor.rgb * _Emissao * 0.6;
                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
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
            #pragma multi_compile_instancing
            #define ESQ_RECORTE_APOIO(v) Dissolver(v.positionWS)
            #include "Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex ProfundidadeVertice
            #pragma fragment VazioFragmento
            #pragma multi_compile_instancing
            #define ESQ_RECORTE_APOIO(v) Dissolver(v.positionWS); CortarNaFrente(v.positionWS, v.positionCS)
            #include "Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex ProfundidadeVertice
            #pragma fragment NormaisFragmento
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #define ESQ_RECORTE_APOIO(v) Dissolver(v.positionWS); CortarNaFrente(v.positionWS, v.positionCS)
            #include "Passes.hlsl"
            ENDHLSL
        }
    }
}
