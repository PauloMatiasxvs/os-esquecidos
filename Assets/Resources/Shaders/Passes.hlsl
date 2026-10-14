// Passes de apoio comuns: sombra, profundidade e normais (para o SSAO).
// Cada shader inclui este arquivo depois de declarar seu CBUFFER.

#ifndef ESQUECIDOS_PASSES_INCLUDED
#define ESQUECIDOS_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct AtributosApoio
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsApoio
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

#ifndef ESQ_DESLOCAMENTO_VERTICE
#define ESQ_DESLOCAMENTO_VERTICE(p, n) p
#endif

VaryingsApoio SombraVertice(AtributosApoio i)
{
    VaryingsApoio o = (VaryingsApoio)0;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
    posWS = ESQ_DESLOCAMENTO_VERTICE(posWS, i.normalOS);
    float3 nWS = TransformObjectToWorldNormal(i.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 dirLuz = normalize(_LightPosition - posWS);
    #else
        float3 dirLuz = _LightDirection;
    #endif
    float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, dirLuz));
    o.positionCS = ApplyShadowClamping(cs);
    o.positionWS = posWS;
    return o;
}

VaryingsApoio ProfundidadeVertice(AtributosApoio i)
{
    VaryingsApoio o = (VaryingsApoio)0;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
    posWS = ESQ_DESLOCAMENTO_VERTICE(posWS, i.normalOS);
    o.positionCS = TransformWorldToHClip(posWS);
    o.normalWS = TransformObjectToWorldNormal(i.normalOS);
    o.positionWS = posWS;
    return o;
}

#ifndef ESQ_RECORTE_APOIO
#define ESQ_RECORTE_APOIO(v)
#endif

half4 VazioFragmento(VaryingsApoio i) : SV_TARGET
{
    ESQ_RECORTE_APOIO(i);
    return 0;
}

half4 NormaisFragmento(VaryingsApoio i) : SV_TARGET
{
    ESQ_RECORTE_APOIO(i);
    float3 n = normalize(i.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 oct = PackNormalOctQuadEncode(n);
        return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
    #else
        return half4(n, 0.0);
    #endif
}

#endif
