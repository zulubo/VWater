// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Water/VWater View Overlay"
{
    Properties
    {
        _AbsorptionColor("Absorption Color", Color) = (0.2, 0.3, 0.9, 1.0)
        _ScatteringColor("Scattering Color", Color) = (0.2, 0.3, 0.9, 1.0)
        _Density("Water Density Multiplier", Range(0.01,1)) = 1.0

		_ScatteringTable("Scattering Lookup Table", 2D) = "white" {}
		_TableHDR("Scattering Table Multiplier", Float) = 1
		_MaxDepth("Max LUT Scattering Depth", Float) = 10

        _GlobalWaterHeight("Water Level", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Overlay" "Queue" = "Overlay-1"}
        LOD 100

        ZTest Off
        ZWrite Off

        GrabPass
        {
            "_WaterViewGrab"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "ForwardBase"
                "PassFlags" = "OnlyDirectional"
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase 
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
			#include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _AbsorptionColor;
            fixed4 _ScatteringColor;
            half _Density;

			sampler2D _ScatteringTable;
			half _TableHDR;

            half _GlobalWaterHeight;
			half _MaxDepth;


            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_WaterViewGrab);

			fixed4 _LightingColor;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.uv = v.uv;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.screenPos = ComputeScreenPos (o.vertex);
                COMPUTE_EYEDEPTH(o.screenPos.z);

                return o;
            }


#define SCATTER_FUDGE 2

#if !defined(POINT) && !defined(SPOT) && !defined(DIRECTIONAL) && !defined(POINT_COOKIE) && !defined(DIRECTIONAL_COOKIE)
//#define DIRECTIONAL
#endif

			fixed4 scattering(v2f i)
			{
#ifdef DIRECTIONAL
                fixed4 direct = 0;
                //fixed4 direct = _LightColor0;
#else
                fixed4 direct = 0;
#endif
				//fixed4 indirect = float4(length(unity_SHAr), length(unity_SHAg), length(unity_SHAb), 1); // average color of light probe
				fixed4 indirect = _LightingColor;
				half4 c = _ScatteringColor * (direct + indirect);
				return c;
			}

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
#if UNITY_SINGLE_PASS_STEREO
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#endif
                half fudgeAmount = 0.01;
                half heightFudge = saturate((_WorldSpaceCameraPos.y - _GlobalWaterHeight)/fudgeAmount)*fudgeAmount;
                clip(-i.worldPos.y + _GlobalWaterHeight + heightFudge);

                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));

                float2 screenUV = i.screenPos.xy/i.screenPos.w;
                float4 grab = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_WaterViewGrab, screenUV);

                // light absorption
                grab *= pow(_AbsorptionColor, clamp(depth * _Density, 1e-10, 1e+10));


				float3 viewDir = normalize(i.worldPos - _WorldSpaceCameraPos);
				float latitude = asin(viewDir.y) / 1.57079;
				float viewDepth = _GlobalWaterHeight - _WorldSpaceCameraPos.y;

                // light scattering
                fixed4 scatter = scattering(i);
				float lutScatter = tex2Dlod(_ScatteringTable, float4(viewDepth / _MaxDepth, latitude / 2 + 0.5, 0, 0)) * SCATTER_FUDGE;
				scatter *= lutScatter * _TableHDR;
                float scatterAmount = 1 - pow(2.71828, -clamp(depth * _Density * _ScatteringColor.a, 1e-10, 1e+10));
                grab *= 1 - scatterAmount;
                grab += scatter * scatterAmount;

                return grab;

            }
            ENDCG
        }
    }
}
