Shader "Water/VWater Bottom"
{
    Properties
    {
		_ReflectionColor("Internal Reflection Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.95
        
        //_MainTex ("Texture", 2D) = "white" {}
        [NoScaleOffset]_Normal ("Water Normals", 2D) = "bump" {}

        _WaveScale("Waves Scale", Vector) = (10, 13, 6, 3)
        _WaveAmplitude("Waves Intensity", Vector) = (0.5, 0.4, 0.3, 0.2)
        _WaveSpeed("Waves Speed", Vector) = (0.5, 0.4, 0.3, 0.2)

        _Refraction ("Refraction Strength", Float) = 0.5
        

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+20"}
        LOD 100

        Cull Front

        GrabPass
        {
            "_WaterGrab"
        }

        Pass {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #pragma target 4.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                UNITY_POSITION(pos);
                UNITY_FOG_COORDS(1)
                float4 worldPos : TEXCOORD2;
                float4 worldUV : TEXCOORD4;
                float3 worldNormal : TEXCOORD3;
                UNITY_LIGHTING_COORDS(5,6)
                float4 screenPos : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _ReflectionColor;
            sampler2D _Normal;
            half4 _WaveScale;
            half4 _WaveAmplitude;
            half4 _WaveSpeed;
            half _Smoothness;
            half _Refraction;

            UNITY_INSTANCING_BUFFER_START(Props)
				UNITY_DEFINE_INSTANCED_PROP(half2, _Flow)
			UNITY_INSTANCING_BUFFER_END(Props)


            v2f vert (appdata_full v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                float2 flow = UNITY_ACCESS_INSTANCED_PROP(Props, _Flow) * _Time.g;
				o.worldUV = o.worldPos - float4(flow.x, 0, flow.y, 0);
                UNITY_TRANSFER_FOG(o,o.pos);
                UNITY_TRANSFER_LIGHTING(o,v.texcoord1.xy);
                float3 worldNormal = -UnityObjectToWorldNormal(v.normal); // inverted normals for water bottom
                o.worldNormal = worldNormal;


                o.screenPos = ComputeScreenPos (o.pos);
                COMPUTE_EYEDEPTH(o.screenPos.z);

                return o;
            }

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_WaterGrab);


            fixed4 lighting(SurfaceOutputStandard s, v2f i)
            {
                #ifndef USING_DIRECTIONAL_LIGHT
                fixed3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                #else
                fixed3 lightDir = _WorldSpaceLightPos0.xyz;
                #endif
                float3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));

                // compute lighting & shadowing factor
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos)
                fixed4 c = 0;

                // Setup lighting environment
                UnityGI gi;
                UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
                gi.indirect.diffuse = 0;
                gi.indirect.specular = 0;
                gi.light.color = _LightColor0.rgb;
                gi.light.dir = lightDir;
                // Call GI (lightmaps/SH/reflections) lighting function
                UnityGIInput giInput;
                UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
                giInput.light = gi.light;
                giInput.worldPos = i.worldPos;
                giInput.worldViewDir = worldViewDir;
                giInput.atten = atten;
                
                giInput.lightmapUV = 0.0;

                
                giInput.ambient.rgb = 0.0;
                
                giInput.probeHDR[0] = unity_SpecCube0_HDR;
                giInput.probeHDR[1] = unity_SpecCube1_HDR;
                #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
                giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
                #endif
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                giInput.boxMax[0] = unity_SpecCube0_BoxMax;
                giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
                giInput.boxMax[1] = unity_SpecCube1_BoxMax;
                giInput.boxMin[1] = unity_SpecCube1_BoxMin;
                giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                LightingStandard_GI(s, giInput, gi);

                // realtime lighting: call lighting function
                c += LightingStandard (s, worldViewDir, gi);

                UNITY_OPAQUE_ALPHA(c.a);
                return c;
            }

            float2 GetWaves(float2 coords, half scale, half amplitude, half speed, half2 dir)
            {
                return (UnpackNormal(tex2D(_Normal, (coords + dir * _Time.g * speed) / scale)) * amplitude).xy;
            }

            #define e 2.71828

            float alerp(float a, float b, float t)
            {
                return (t - a)/(b - a);
            }


            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
#if UNITY_SINGLE_PASS_STEREO
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#endif

                float2 nrm = GetWaves(i.worldUV.xz, _WaveScale.x, _WaveAmplitude.x, _WaveSpeed.x, half2(1,0.5));
                nrm += GetWaves(i.worldUV.xz, _WaveScale.y, _WaveAmplitude.y, _WaveSpeed.y, half2(-1,-0.5));
                nrm += GetWaves(i.worldUV.xz, _WaveScale.z, _WaveAmplitude.z, _WaveSpeed.z, half2(0.5,-1));

                nrm/=3;

                // near fade for falloff at clipping plane
                float nearFade = saturate(alerp(_ProjectionParams.y, _ProjectionParams.y * 3,  i.screenPos.z));
                nearFade *= nearFade;

                float2 uvOffset = nrm * unity_CameraProjection._m11 * _Refraction * nearFade;

                float2 distUV = i.screenPos.xy;
                distUV.xy += uvOffset;
                distUV/=i.screenPos.w;

                float4 col = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_WaterGrab, distUV);
                
                SurfaceOutputStandard o;
                o.Albedo = 1;
                o.Emission = 0.0;
                o.Metallic = 1;
                o.Smoothness = _Smoothness;
                o.Alpha = 1.0;
                o.Occlusion = 1;
                o.Normal = i.worldNormal.xyz;
                o.Normal.xz += nrm;

                o.Normal = normalize(o.Normal);

                fixed4 light = lighting(o, i) * _ReflectionColor; // tint reflection
                

                float snellCutoff = 0.5;
                float angle = dot(o.Normal, normalize(UnityWorldSpaceViewDir(i.worldPos)));
                float snell = saturate(alerp(snellCutoff+0.001, snellCutoff-0.001, angle)) * (nearFade);

                col = lerp(col, light, snell);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        Pass
        { 
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
 
            Fog {Mode Off}
            ZWrite On ZTest Less Cull Front
            Offset 1, 1
 
            CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it does not contain a surface program or both vertex and fragment programs.
#pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
             #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert( appdata v )
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
 
              return o;
            }
 
            float4 frag( v2f i ) : COLOR
            {
                return 1;
            }
            ENDCG
        } 
 
     }
     
}
