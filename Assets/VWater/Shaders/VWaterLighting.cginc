// Upgrade NOTE: replaced 'defined UNITY_LIGHT_ATTENUATION' with 'defined (UNITY_LIGHT_ATTENUATION)'




//#include "UnityCG.cginc"
#include "AutoLight.cginc"
//#include "Lighting.cginc"
#include "UnityPBSLighting.cginc"

struct v2f
{
    UNITY_POSITION(pos);
    UNITY_FOG_COORDS(1)
    float3 worldPos : TEXCOORD2;
    float3 worldNormal : TEXCOORD3;

    float3 worldUV : TEXCOORD5; // same as worldPos but offset for flowing water

    UNITY_LIGHTING_COORDS(6, 7)

    float4 screenPos : TEXCOORD8;
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

fixed4 _AbsorptionColor;
fixed4 _ScatteringColor;
half _Density;
sampler2D _MainTex;
sampler2D _Normal;
sampler2D _FoamMap;
half _FoamDepth;
sampler2D _WindMap;
half4 _FoamMap_ST;
half4 _WindMap_ST;
half4 _WaveScale;
half4 _WaveAmplitude;
half4 _WaveSpeed;
half _Smoothness;
half _Transition;
half _Refraction;


#ifdef DYNAMICS_ENABLED

sampler2D _WaveBuffer;
float _WaveBufferWorldSize;
float2 _WaveBufferWorldPos;

#endif

UNITY_INSTANCING_BUFFER_START(Props)
UNITY_DEFINE_INSTANCED_PROP(fixed4, _LightingColor)
UNITY_DEFINE_INSTANCED_PROP(half2, _Flow)
UNITY_INSTANCING_BUFFER_END(Props)


v2f vert(appdata_full v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex);
    float2 flow = UNITY_ACCESS_INSTANCED_PROP(Props, _Flow) * _Time.g;
    o.worldUV = o.worldPos - float3(flow.x, 0, flow.y);
    UNITY_TRANSFER_FOG(o, o.pos);
    UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
    float3 worldNormal = UnityObjectToWorldNormal(v.normal);
    o.worldNormal = worldNormal;

    o.screenPos = ComputeScreenPos(o.pos);
    COMPUTE_EYEDEPTH(o.screenPos.z);

    return o;
}

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

#if defined(FORWARD_BASE_PASS)
UNITY_DECLARE_SCREENSPACE_TEXTURE(_WaterGrab);
#endif


struct SurfaceOutputWater
{
    fixed3 Albedo;      // base (diffuse or specular) color
    fixed3 Normal;      // tangent space normal, if written
    half Smoothness;    // 0=rough, 1=smooth
    half Specular; 
    fixed3 Scattering;   // light scattering color
};

UnityLight CreateLight(v2f i) {
    UnityLight light;

#if defined(POINT) || defined(SPOT) || defined(POINT_COOKIE)
    light.dir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
#else
    light.dir = _WorldSpaceLightPos0.xyz;
#endif
    UNITY_LIGHT_ATTENUATION(attenuation, 0, i.worldPos);
    light.color = _LightColor0.rgb * attenuation;
    light.ndotl = DotClamped(i.worldNormal, light.dir);
    return light;
}

float3 BoxProjection(
    float3 direction, float3 position,
    float4 cubemapPosition, float3 boxMin, float3 boxMax
) 
{
    UNITY_BRANCH
    if (cubemapPosition.w > 0) 
    {
        float3 factors = ((direction > 0 ? boxMax : boxMin) - position) / direction;
        float scalar = min(min(factors.x, factors.y), factors.z);
        direction = direction * scalar + (position - cubemapPosition);
    }
    return direction;
}

UnityIndirect CreateIndirectLight(v2f i, float3 viewDir, float3 normal, float smoothness) {
    UnityIndirect indirectLight;
    indirectLight.diffuse = 0;
    indirectLight.specular = 0;

//#if defined(VERTEXLIGHT_ON)
    //indirectLight.diffuse = i.vertexLightColor;
//#endif

#if defined(FORWARD_BASE_PASS)
    indirectLight.diffuse += max(0, ShadeSH9(float4(normal, 1)));
    float3 reflectionDir = reflect(-viewDir, normal);
    Unity_GlossyEnvironmentData envData;
    envData.roughness = 1 - smoothness;
    envData.reflUVW = BoxProjection(
        reflectionDir, i.worldPos,
        unity_SpecCube0_ProbePosition,
        unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
    );

    indirectLight.specular = Unity_GlossyEnvironment(
        UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, envData
    );
#endif

    return indirectLight;
}


fixed4 lighting(SurfaceOutputWater s, v2f i)
{
    float3 specularTint;
    float oneMinusReflectivity;
    s.Albedo = DiffuseAndSpecularFromMetallic(
        s.Albedo, 0, specularTint, oneMinusReflectivity
    );

    specularTint *= s.Specular;
    oneMinusReflectivity = 1 - s.Specular;

    float3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));

    UnityLight light = CreateLight(i);
    UnityIndirect indirect = CreateIndirectLight(i, worldViewDir, s.Normal, s.Smoothness);

#if defined(FORWARD_BASE_PASS)
    float3 extraLighting = UNITY_ACCESS_INSTANCED_PROP(Props, _LightingColor);
#else
    float3 extraLighting = 0;
#endif
    fixed3 scattering = (light.color /*+ indirect.diffuse*/ + extraLighting) * s.Scattering;

    return UNITY_BRDF_PBS(
        s.Albedo, specularTint,
        oneMinusReflectivity, s.Smoothness,
        s.Normal, worldViewDir,
        light, indirect
    ) + float4(scattering, 0);
}

float GetDepth(v2f i, float2 uv)
{
    float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
    float partZ = i.screenPos.z;

    return sceneZ - partZ;
}


float2 GetWaves(float2 coords, half scale, half amplitude, half speed, half2 dir)
{
    return (UnpackNormal(tex2D(_Normal, (coords + dir * _Time.g * speed) / scale)) * amplitude).xy;
}

#define e 2.71828

float alerp(float a, float b, float t)
{
    return (t - a) / (b - a);
}

fixed4 ApplyFogColor(v2f i, fixed4 col, fixed4 fogCol)
{
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
    UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fogCol);
#endif
    return col;
}

#define SCATTER_FUDGE 0.3
#define WAVE_FALLOFF_SLOPE 5

fixed4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
#if UNITY_SINGLE_PASS_STEREO
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#endif

    float foam = 0;
    float waveHeight = 0;
    float2 waveNormals = 0;

    #ifdef DYNAMICS_ENABLED
        float2 waveUV = ((i.worldPos.xz - _WaveBufferWorldPos) / _WaveBufferWorldSize) + 0.5;
        fixed4 waveBuffer = tex2D(_WaveBuffer, waveUV);
        //return waveBuffer;

        half waveFalloff = saturate(WAVE_FALLOFF_SLOPE - 2 * WAVE_FALLOFF_SLOPE * abs(0.5 - waveUV.x))
            * saturate(WAVE_FALLOFF_SLOPE - 2 * WAVE_FALLOFF_SLOPE * abs(0.5 - waveUV.y));

        waveBuffer *= waveFalloff;

        waveHeight = waveBuffer.r;
        foam = (1 - (1 - waveBuffer.g) * (1 - waveBuffer.g)) * 1;
        waveNormals = waveBuffer.ba;

    #endif

    float wind = 0;
    #ifdef WIND_MAP
        wind = tex2D(_WindMap, TRANSFORM_TEX(((i.worldPos.xz + _Time.g) / 100), _WindMap)).r;
        wind = wind * wind;
    #endif

        // multiple overlapping waves
        float2 nrm = GetWaves(i.worldUV.xz, _WaveScale.x, _WaveAmplitude.x, _WaveSpeed.x, half2(1,0.5));
        nrm += GetWaves(i.worldUV.xz, _WaveScale.y, _WaveAmplitude.y, _WaveSpeed.y, half2(-1,-0.5));
        nrm += GetWaves(i.worldUV.xz, _WaveScale.z, _WaveAmplitude.z, _WaveSpeed.z, half2(0.5,-1));

        nrm /= 3;

        #ifdef WIND_MAP
            nrm *= lerp(0.5, 1.3, wind);
        #endif

        half2 oldNrm = nrm; // pre-wavebuffer normals

        nrm += waveNormals;

        // near fade for falloff at clipping plane
        float nearFade = smoothstep(0,1,saturate(alerp(_ProjectionParams.y, _ProjectionParams.y * 3,  i.screenPos.z)));

        float2 uvOffset = nrm * unity_CameraProjection._m11 * _Refraction * nearFade;

        float2 distUV = i.screenPos.xy;
        distUV.xy += uvOffset;
        distUV /= i.screenPos.w;

        float depth = GetDepth(i, distUV);
        // fade for intersections with terrain
        float transitionFade = smoothstep(0, 1, saturate(depth / _Transition));

    #if REFRACTION_DEPTH_CORRECTION
        // GO BACK TO UNDISTORTED UVS IF NEGATIVE DEPTH
        distUV = lerp(i.screenPos.xy / i.screenPos.w, distUV, transitionFade);
        depth = GetDepth(i, distUV);
        // recalc transition
        transitionFade = smoothstep(0, 1, saturate(depth / _Transition));
    #endif


#if defined(FORWARD_BASE_PASS)

    fixed4 col = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_WaterGrab, distUV);

    // light absorption
    // fade to white with fog to fade in distance properly
    float exp = clamp(depth * _Density, 1e-10, 1e+10);
    fixed4 absorb = pow(_AbsorptionColor, exp);
    col *= absorb;
    //col *= pow(ApplyFogColor(i, _AbsorptionColor, 1), clamp(depth * _Density, 1e-10, 1e+10));
#else
    fixed4 col = 0;
#endif

    fixed3 scattering = _ScatteringColor.rgb;
    float scatterAmount = (1 - pow(2.71828, -clamp(depth * _Density * _ScatteringColor.a, 1e-10, 1e+10)));

    scattering *= scatterAmount;
    col *= 1 - scatterAmount;

    // foam Intersection
    foam += saturate(alerp(_FoamDepth, 0, depth)) * saturate(alerp(0, _FoamDepth / 10, depth));

    float2 foamDistort = GetWaves(i.worldUV.xz, 1 / _FoamMap_ST.x * 20, 0.1, 0.13, half2(1,0.5));
    float2 fuv = TRANSFORM_TEX(((i.worldUV.xz - float2(0.05, 0.03) * _Time.g + foamDistort) / 10), _FoamMap);
    half foamMask = tex2D(_FoamMap, fuv);
    foamMask = saturate(foamMask);
    foam = pow(foam, 0.5);
    foam = saturate(foam - (1 - foamMask));


    SurfaceOutputWater o;
    o.Albedo = nearFade * foam;
    o.Smoothness = lerp(lerp(_Smoothness, 0, foam), 0, wind * 0.4);
    o.Normal = i.worldNormal.xyz;
    o.Normal.xz += nrm;
    o.Normal = normalize(o.Normal);
    o.Scattering = scattering * SCATTER_FUDGE;
    o.Specular = 1;
    o.Specular = nearFade * transitionFade; // fade out reflections close to eyes

    fixed4 light = lighting(o, i) * transitionFade;

#if FOG_ENABLED
    col += light;

    #if defined(FORWARD_BASE_PASS)
        UNITY_APPLY_FOG(i.fogCoord, col);
    #else
        UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
    #endif
#else
    // only apply fog to light
    UNITY_APPLY_FOG_COLOR(i.fogCoord, light, fixed4(0, 0, 0, 0));
    col += light;
#endif


    return col;
}