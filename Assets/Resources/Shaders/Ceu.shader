// Ceu procedural: degrade do horizonte ao zenite, sol com halo, lua, estrelas
// de noite e nuvens altas correndo devagar. As cores vem do ciclo do dia.
Shader "Esquecidos/Ceu"
{
    Properties
    {
        _CorZenite ("Zenite", Color) = (0.25,0.45,0.75,1)
        _CorHorizonte ("Horizonte", Color) = (0.75,0.8,0.85,1)
        _CorSol ("Sol", Color) = (1,0.9,0.7,1)
        _DirSol ("Direcao do sol", Vector) = (0,1,0,0)
        _Noite ("Noite", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Esquecidos.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CorZenite;
                half4 _CorHorizonte;
                half4 _CorSol;
                float4 _DirSol;
                half _Noite;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V Vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float alto = saturate(d.y);
                half3 cor = lerp(_CorHorizonte.rgb, _CorZenite.rgb, pow(alto, 0.45));
                // Abaixo do horizonte: neblina escura (o muro tapa mesmo).
                cor = lerp(cor, _CorHorizonte.rgb * 0.5, saturate(-d.y * 4));
                float3 s = normalize(_DirSol.xyz);
                float ds = saturate(dot(d, s));
                cor += _CorSol.rgb * (pow(ds, 900) * 6 + pow(ds, 12) * 0.35) * (1 - _Noite);
                // Lua do lado oposto ao sol.
                float dl = saturate(dot(d, -s));
                cor += half3(0.75, 0.8, 0.95) * (smoothstep(0.9994, 0.9997, dl) * 1.6 + pow(dl, 60) * 0.12) * _Noite;
                // Estrelas: celulas do ruido acendendo, piscando.
                float2 uvc = d.xz / max(d.y + 0.25, 0.05) * 3;
                float4 r = Ruido(uvc);
                float estrela = (1 - smoothstep(0.015, 0.03, r.g)) * smoothstep(0.55, 0.9, r.b);
                estrela *= 0.6 + 0.4 * sin(_Time.y * 3 + r.r * 40);
                cor += estrela * _Noite * alto * 1.4;
                // Nuvens altas.
                float2 uvn = d.xz / max(d.y + 0.15, 0.05) * 0.35 + _Time.y * 0.004;
                float nuvem = smoothstep(0.5, 0.85, Ruido(uvn).r * 0.7 + Ruido(uvn * 3.1).a * 0.3);
                half3 corNuvem = lerp(lerp(_CorHorizonte.rgb, half3(1, 1, 1), 0.5), half3(0.12, 0.13, 0.18), _Noite);
                cor = lerp(cor, corNuvem, nuvem * alto * 0.7);
                return half4(cor, 1);
            }
            ENDHLSL
        }
    }
}
