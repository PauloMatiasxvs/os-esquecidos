// Particulas, rastros, facho de loot, poca de acido, numero flutuante nao.
// Sem luz: cor do vertice vezes um brilho HDR (o bloom faz o resto).
// _Forma: 0 bola suave, 1 faisca esticada, 2 anel, 3 quadrado, 4 facho vertical,
// 5 mancha no chao (borda recortada pelo ruido), 6 cone (some ao longo do comprimento).
Shader "Esquecidos/Particula"
{
    Properties
    {
        _Cor ("Cor", Color) = (1,1,1,1)
        _Brilho ("Brilho", Float) = 1
        _Forma ("Forma", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _Origem ("Blend origem", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Destino ("Blend destino", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_Origem] [_Destino]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Particula"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Esquecidos.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Cor;
                half _Brilho;
                float _Forma;
                float _Origem;
                float _Destino;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half fog : TEXCOORD2; };

            V Vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = i.color * _Cor;
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float2 c = i.uv * 2 - 1;
                float a = 1;
                int forma = (int)round(_Forma);
                if (forma == 0) a = saturate(1 - dot(c, c));
                else if (forma == 1) a = saturate(1 - abs(c.y)) * saturate(1 - c.x * c.x);
                else if (forma == 2) { float r = length(c); a = saturate(1 - abs(r - 0.75) * 6) ; }
                else if (forma == 3) a = 1;
                else if (forma == 4) a = saturate(1 - abs(c.x)) * pow(saturate(1 - i.uv.y), 1.5);
                else if (forma == 6) a = pow(saturate(1 - i.uv.y), 1.2);
                else if (forma == 5)
                {
                    float r = length(c);
                    float borda = RuidoLod(i.positionWS.xz * 0.8).r;
                    a = saturate((1 - r) * 3 - borda * 0.8);
                }
                a *= a;
                half4 cor = half4(i.color.rgb * _Brilho, i.color.a * a);
                cor.rgb = MixFogColor(cor.rgb, half3(0, 0, 0), i.fog);
                return cor;
            }
            ENDHLSL
        }
    }
}
