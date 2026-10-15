// Chao. Um material por tipo (grama, terra, pedra, plantacao, concreto,
// chapa, ladrilho, fosso), todos procedurais em espaco de mundo. As zonas
// sobrepostas (caminho de terra por cima da grama) tem alfa no vertice das
// bordas, e a borda some recortada pelo ruido: transicao organica sem
// transparencia nem ordenacao.
Shader "Esquecidos/Chao"
{
    Properties
    {
        _Tipo ("Tipo", Float) = 0
        _Cor ("Tinta", Color) = (1,1,1,1)
        _Escala ("Escala", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" }

        HLSLINCLUDE
        #include "Esquecidos.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Tipo;
            half4 _Cor;
            float _Escala;
        CBUFFER_END

        void RecortarBorda(float3 posWS, half alfa)
        {
            if (alfa < 0.999)
            {
                float r = RuidoLod(posWS.xz * 0.21).r * 0.7 + RuidoLod(posWS.xz * 0.9).b * 0.3;
                clip(alfa - r);
            }
        }
        ENDHLSL

        Pass
        {
            Name "Chao"
            Tags { "LightMode"="UniversalForward" }

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

            half3 Grama(float2 xz, inout half3 n)
            {
                float4 a = Ruido(xz * 0.03);
                float4 b = Ruido(xz * 0.17);
                float4 c = Ruido(xz * 1.3);
                half3 cor = lerp(half3(0.16, 0.27, 0.08), half3(0.33, 0.42, 0.13), a.r);
                cor = lerp(cor, half3(0.42, 0.40, 0.18), smoothstep(0.62, 0.8, b.a) * 0.6);   // grama seca
                cor = lerp(cor, half3(0.30, 0.22, 0.14), smoothstep(0.78, 0.9, a.a) * 0.7);    // terra aparecendo
                cor *= 0.85 + 0.3 * c.b;
                // Florzinhas: celulas pequenas que acendem.
                float flor = 1 - smoothstep(0.05, 0.09, Ruido(xz * 2.1 + 0.5).g);
                cor = lerp(cor, lerp(half3(0.95, 0.9, 0.55), half3(0.85, 0.55, 0.75), c.r), flor * smoothstep(0.55, 0.7, b.r) * 0.85);
                return cor;
            }

            half3 Terra(float2 xz)
            {
                float4 a = Ruido(xz * 0.08);
                float4 b = Ruido(xz * 0.9);
                half3 cor = lerp(half3(0.27, 0.2, 0.13), half3(0.4, 0.31, 0.2), a.r) * (0.85 + 0.3 * b.b);
                float pedrinha = 1 - smoothstep(0.08, 0.12, Ruido(xz * 1.6).g);
                cor = lerp(cor, half3(0.5, 0.47, 0.42), pedrinha * 0.7);
                // Marcas de pegada e de roda: listras suaves.
                cor *= 1 - smoothstep(0.7, 0.95, Ruido(float2(xz.x * 0.4, xz.y * 0.05)).a) * 0.15;
                return cor;
            }

            half3 Pedra(float2 xz, inout half3 n)
            {
                // Lajes irregulares: celulas Worley em duas escalas.
                float4 c1 = Ruido(xz * 0.22);
                float junta = smoothstep(0.08, 0.16, c1.g);
                float4 a = Ruido(xz * 0.05);
                float4 b = Ruido(xz * 1.1);
                half3 laje = lerp(half3(0.36, 0.35, 0.33), half3(0.52, 0.5, 0.46), c1.b) * (0.85 + 0.25 * b.b);
                laje = lerp(laje, half3(0.3, 0.33, 0.24), smoothstep(0.6, 0.85, a.r) * 0.55);   // limo
                half3 vao = lerp(half3(0.12, 0.13, 0.09), half3(0.16, 0.24, 0.08), a.a);       // musgo no vao
                half3 cor = lerp(vao, laje, junta);
                // Folha caida.
                float folha = 1 - smoothstep(0.06, 0.1, Ruido(xz * 1.9 + 0.17).g);
                cor = lerp(cor, lerp(half3(0.45, 0.3, 0.12), half3(0.35, 0.36, 0.12), b.r), folha * smoothstep(0.5, 0.75, a.a));
                n = normalize(n + half3(c1.b - 0.5, 0, c1.r - 0.5) * 0.15 * junta);
                return cor;
            }

            half3 Plantacao(float2 xz)
            {
                float sulco = sin(xz.y * 3.0) * 0.5 + 0.5;
                float4 b = Ruido(xz * 0.8);
                half3 cor = lerp(half3(0.18, 0.12, 0.07), half3(0.32, 0.22, 0.13), sulco) * (0.85 + 0.3 * b.b);
                return cor;
            }

            half3 Concreto(float2 xz)
            {
                float2 laje = xz / 3.0;
                float2 f = frac(laje);
                float junta = smoothstep(0.0, 0.015, min(min(f.x, 1 - f.x), min(f.y, 1 - f.y)));
                float4 a = Ruido(xz * 0.07);
                float4 b = Ruido(xz * 1.2);
                half3 cor = lerp(half3(0.4, 0.4, 0.39), half3(0.55, 0.54, 0.52), a.r) * (0.9 + 0.2 * b.b);
                cor *= lerp(0.55, 1, junta);
                cor = lerp(cor, half3(0.2, 0.2, 0.18), smoothstep(0.75, 0.95, a.a) * 0.5);   // mancha de oleo
                float crista = 1 - abs(Ruido(xz * 0.25).a * 2 - 1);
                cor *= 1 - smoothstep(0.95, 0.99, crista) * 0.6;
                return cor;
            }

            half3 Chapa(float2 xz)
            {
                // Chapa xadrez de piso industrial.
                float2 g = xz * 2.2;
                float2 r1 = frac(float2(g.x + g.y, g.x - g.y) * 0.7071) - 0.5;
                float losango = 1 - smoothstep(0.08, 0.14, abs(r1.x) + abs(r1.y) * 0.3);
                float2 placa = frac(xz / 2.5);
                float junta = smoothstep(0.0, 0.01, min(min(placa.x, 1 - placa.x), min(placa.y, 1 - placa.y)));
                float4 a = Ruido(xz * 0.09);
                half3 cor = half3(0.32, 0.32, 0.33) * (0.85 + 0.25 * a.b);
                cor += losango * 0.07;
                float ferrugem = smoothstep(0.55, 0.8, a.r + Ruido(xz * 0.7).b * 0.25);
                cor = lerp(cor, half3(0.36, 0.17, 0.07), ferrugem * 0.8);
                cor *= lerp(0.5, 1, junta);
                return cor;
            }

            half3 Ladrilho(float2 xz)
            {
                float2 f = frac(xz / 1.0);
                float junta = smoothstep(0.0, 0.025, min(min(f.x, 1 - f.x), min(f.y, 1 - f.y)));
                float4 a = Ruido(xz * 0.13);
                half3 cor = half3(0.3, 0.33, 0.37) * (0.93 + 0.07 * a.b);
                cor = lerp(cor, half3(0.55, 0.5, 0.45), smoothstep(0.78, 0.95, a.r) * 0.4);
                cor *= lerp(0.6, 1, junta);
                return cor;
            }

            half3 Fosso(float2 xz)
            {
                float4 a = Ruido(xz * 0.12);
                float4 b = Ruido(xz * 0.9);
                half3 cor = lerp(half3(0.13, 0.12, 0.13), half3(0.25, 0.23, 0.24), a.r) * (0.8 + 0.4 * b.b);
                float risco = 1 - smoothstep(0.02, 0.05, abs(Ruido(xz * 0.4).a - 0.5));
                cor = lerp(cor, half3(0.06, 0.05, 0.06), risco * 0.6);
                return cor;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                RecortarBorda(i.positionWS, i.color.a);
                float2 xz = i.positionWS.xz * _Escala;
                half3 n = normalize(i.normalWS);
                half3 cor;
                int tipo = (int)round(_Tipo);
                if (tipo == 0) cor = Grama(xz, n);
                else if (tipo == 1) cor = Terra(xz);
                else if (tipo == 2) cor = Pedra(xz, n);
                else if (tipo == 3) cor = Plantacao(xz);
                else if (tipo == 4) cor = Concreto(xz);
                else if (tipo == 5) cor = Chapa(xz);
                else if (tipo == 6) cor = Ladrilho(xz);
                else cor = Fosso(xz);
                cor *= _Cor.rgb * i.color.rgb;

                DadosDeLuz d;
                d.positionWS = i.positionWS;
                d.normalWS = n;
                d.viewWS = normalize(GetWorldSpaceViewDir(i.positionWS));
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.uvTela = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 luz = IluminarToon(d, cor, 0.45, 0.3, 0, tipo == 5 || tipo == 6 ? 0.25 : 0);
                luz = MixFog(luz, i.fog);
                return half4(luz, 1);
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
            struct A { float4 positionOS : POSITION; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half a : TEXCOORD1; };
            V Vert(A i) { V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.a = i.color.a; return o; }
            half4 Frag(V i) : SV_Target { RecortarBorda(i.positionWS, i.a); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half a : TEXCOORD1; float3 n : TEXCOORD2; };
            V Vert(A i) { V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.a = i.color.a; o.n = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 Frag(V i) : SV_Target
            {
                RecortarBorda(i.positionWS, i.a);
                float3 n = normalize(i.n);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
