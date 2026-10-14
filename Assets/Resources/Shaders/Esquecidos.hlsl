// Biblioteca comum dos shaders de Os Esquecidos.
// Luz toon (faixas suaves), ambiente em hemisferio controlado pelo ciclo do dia,
// ruido triplanar a partir de uma textura de ruido gerada em codigo, e o corte
// por pontilhado dos muros que ficam entre a camera e o jogador.

#ifndef ESQUECIDOS_INCLUDED
#define ESQUECIDOS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

// Globais definidas pelo jogo a cada quadro (Ambiente.cs).
TEXTURE2D(_Esq_Ruido);
SAMPLER(sampler_Esq_Ruido);
float4 _Esq_PosJogador;        // xyz = jogador, w = 1 se o corte esta ligado
float4 _Esq_AmbienteCeu;       // cor do ambiente vinda de cima
float4 _Esq_AmbienteChao;      // cor do ambiente vinda de baixo
float4 _Esq_CorSombra;         // tom das areas de sombra (fica azulado a noite)
float  _Esq_Noite;             // 0 de dia, 1 de noite
float  _Esq_Vento;             // forca do vento na grama e nas folhas

// ---------------------------------------------------------------- ruido

// Canais da textura: R = fbm suave, G = celulas (Worley), B = fino, A = outro fbm.
float4 Ruido(float2 uv)
{
    return SAMPLE_TEXTURE2D(_Esq_Ruido, sampler_Esq_Ruido, uv);
}

float4 RuidoLod(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_Esq_Ruido, sampler_Esq_Ruido, uv, 0);
}

// Ruido projetado nos tres eixos e misturado pela normal: nada de costura em
// muro, chao ou caixote, sem precisar de UV.
float4 RuidoTriplanar(float3 posWS, float3 normalWS, float escala)
{
    float3 peso = pow(abs(normalWS), 4.0);
    peso /= max(peso.x + peso.y + peso.z, 1e-4);
    float4 x = Ruido(posWS.zy * escala);
    float4 y = Ruido(posWS.xz * escala);
    float4 z = Ruido(posWS.xy * escala);
    return x * peso.x + y * peso.y + z * peso.z;
}

// ---------------------------------------------------------------- luz

half3 Ambiente(half3 normalWS)
{
    half t = saturate(normalWS.y * 0.5 + 0.5);
    return lerp(_Esq_AmbienteChao.rgb, _Esq_AmbienteCeu.rgb, t);
}

// Faixa toon: a luz principal cai em degrau suave em vez de gradiente.
half Faixa(half nl, half corte, half suavidade)
{
    return smoothstep(corte - suavidade, corte + suavidade, nl);
}

struct DadosDeLuz
{
    float3 positionWS;
    half3 normalWS;
    half3 viewWS;
    float4 shadowCoord;
    float2 uvTela;
};

// Ilumina um albedo com a luz principal em faixas, as luzes adicionais em
// faixa mais larga, ambiente em hemisferio e borda de luz no contorno.
half3 IluminarToon(DadosDeLuz d, half3 albedo, half corte, half suavidade, half borda, half brilho)
{
    half4 shadowMask = half4(1, 1, 1, 1);
    Light principal = GetMainLight(d.shadowCoord, d.positionWS, shadowMask);

    half aoDireto = 1, aoIndireto = 1;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(d.uvTela);
        aoDireto = ao.directAmbientOcclusion;
        aoIndireto = ao.indirectAmbientOcclusion;
    #endif

    half nl = dot(d.normalWS, principal.direction) * 0.5 + 0.5;
    half sombra = principal.shadowAttenuation * principal.distanceAttenuation;
    half luz = Faixa(nl, corte, suavidade) * sombra * aoDireto;

    half3 ambiente = Ambiente(d.normalWS) * aoIndireto;
    // A sombra nao e cinza: puxa para o tom do ceu, que e o que preenche ela.
    half3 cor = albedo * (ambiente + _Esq_CorSombra.rgb * (1 - luz) * 0.35 + principal.color * luz);

    // Brilho especular em degrau (metal, olho, pele molhada).
    if (brilho > 0)
    {
        half3 h = normalize(principal.direction + d.viewWS);
        half nh = saturate(dot(d.normalWS, h));
        cor += principal.color * smoothstep(0.92, 0.96, nh) * brilho * luz;
    }

    // Luzes adicionais: tochas, lanterna, postes.
    InputData inputData = (InputData)0;
    inputData.positionWS = d.positionWS;
    inputData.normalizedScreenSpaceUV = d.uvTela;
    uint quantas = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        Light l = GetAdditionalLight(lightIndex, d.positionWS, shadowMask);
        half a = l.distanceAttenuation * l.shadowAttenuation;
        cor += albedo * l.color * a * Faixa(dot(d.normalWS, l.direction) * 0.5 + 0.5, 0.45, 0.2);
    }
    #endif
    LIGHT_LOOP_BEGIN(quantas)
        Light l = GetAdditionalLight(lightIndex, d.positionWS, shadowMask);
        half a = l.distanceAttenuation * l.shadowAttenuation;
        half f = Faixa(dot(d.normalWS, l.direction) * 0.5 + 0.5, 0.4, 0.25);
        cor += albedo * l.color * a * f;
    LIGHT_LOOP_END

    // Borda de luz: separa o personagem do chao escuro.
    half fresnel = pow(1 - saturate(dot(d.normalWS, d.viewWS)), 4);
    cor += borda * fresnel * lerp(principal.color * 0.6, half3(0.35, 0.45, 0.7), _Esq_Noite) * (0.35 + luz * 0.65);
    return cor;
}

// ---------------------------------------------------------------- corte

// Matriz de Bayer 4x4 para o pontilhado: some sem precisar de transparencia.
static const float ESQ_BAYER[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

float Bayer4(float2 pixel)
{
    uint2 p = uint2(pixel) % 4;
    return (ESQ_BAYER[p.y * 4 + p.x] + 0.5) / 16.0;
}

// Quanto o fragmento deve sumir por estar entre a camera e o jogador (0..1).
float CorteNaFrenteDoJogador(float3 posWS, float raioBase, float margem)
{
    if (_Esq_PosJogador.w < 0.5) return 0;
    float3 cam = _WorldSpaceCameraPos;
    float3 alvo = _Esq_PosJogador.xyz + float3(0, 1.1, 0);
    float3 eixo = alvo - cam;
    float comp = length(eixo);
    float3 dir = eixo / comp;
    float t = dot(posWS - cam, dir);
    if (t <= 0 || t >= comp - margem) return 0;
    float3 maisPerto = cam + dir * t;
    float dist = length(posWS - maisPerto);
    float raio = raioBase * lerp(1.35, 0.85, t / comp);
    // O que esta abaixo da cintura do jogador fica: so some o muro alto.
    float altura = smoothstep(_Esq_PosJogador.y + 0.6, _Esq_PosJogador.y + 2.2, posWS.y);
    return smoothstep(raio, raio * 0.55, dist) * altura;
}

float CorteNaFrenteDoJogador(float3 posWS, float raioBase)
{
    return CorteNaFrenteDoJogador(posWS, raioBase, 0.6);
}

#endif
