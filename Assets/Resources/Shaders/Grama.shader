// Laminas de grama do Patio. Cada lamina e um triangulo fino; o UV.y diz a
// altura ao longo dela (0 na raiz, 1 na ponta) e o vento empurra mais a ponta.
// A cor do vertice traz a tonalidade da touceira.
Shader "Esquecidos/Grama"
{
    Properties
    {
        _CorRaiz ("Cor da raiz", Color) = (0.08,0.14,0.04,1)
        _CorPonta ("Cor da ponta", Color) = (0.45,0.55,0.18,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Esquecidos.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _CorRaiz;
            half4 _CorPonta;
        CBUFFER_END

        float3 Vento(float3 posWS, float alturaRel)
        {
            float t = _Time.y;
            float onda = sin(t * 1.7 + posWS.x * 0.35 + posWS.z * 0.21) + 0.5 * sin(t * 3.1 + posWS.x * 1.3);
            float rajada = RuidoLod(posWS.xz * 0.02 + t * 0.03).r;
            float forca = (0.15 + rajada * 0.5) * _Esq_Vento * alturaRel * alturaRel;
            float3 d = float3(onda, 0, onda * 0.6) * forca;
            // O jogador amassa a grama por onde passa.
            float2 afasta = posWS.xz - _Esq_PosJogador.xz;
            float dist = length(afasta);
            float amassa = saturate(1 - dist / 0.9) * alturaRel;
            d.xz += (dist > 1e-3 ? afasta / dist : 0) * amassa * 0.35;
            d.y -= amassa * 0.15;
            return d;
        }
        ENDHLSL

        Pass
        {
            Name "Grama"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            struct Atributos
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : TEXCOORD1;
                float altura : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Atributos i)
            {
                Varyings o = (Varyings)0;
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                posWS += Vento(posWS, i.uv.y);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.color = i.color;
                o.altura = i.uv.y;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo = lerp(_CorRaiz.rgb, _CorPonta.rgb, i.altura) * i.color.rgb;
                DadosDeLuz d;
                d.positionWS = i.positionWS;
                d.normalWS = half3(0, 1, 0);
                d.viewWS = normalize(GetWorldSpaceViewDir(i.positionWS));
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.uvTela = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 cor = IluminarToon(d, albedo, 0.4, 0.3, 0, 0);
                // Contraluz na ponta: o sol atravessa a folha.
                Light l = GetMainLight();
                cor += albedo * l.color * pow(saturate(dot(-d.viewWS, l.direction)), 4) * i.altura * 0.6;
                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; };
            V Vert(A i) { V o; float3 p = TransformObjectToWorld(i.positionOS.xyz); p += Vento(p, i.uv.y); o.positionCS = TransformWorldToHClip(p); return o; }
            half4 Frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
