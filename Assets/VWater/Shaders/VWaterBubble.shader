Shader "Water/VWater Bubble"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (1,1,1,1)
        _MainTex ("Mask", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normals", 2D) = "bump" {}
        _RefractiveIndex("Index of Refraction", Float) = 1.5
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        LOD 100

        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"


            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                half3 tspace0 : TEXCOORD2;
                half3 tspace1 : TEXCOORD3;
                half3 tspace2 : TEXCOORD4;
                float3 worldPos : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            half _RefractiveIndex;
            half _Cutoff;
            fixed4 _Color;


            half fresnel(float3 viewDir, float3 normal, float ior) 
            { 
                half cosi = clamp(-1, 1, dot(viewDir, normal)); 
                half etai = 1;
                half etat = ior; 
                if (cosi > 0)
                { // swap
                    etai = ior;
                    etat = 1;
                }

                // Compute sini using Snell's law
                half sint = etai / etat * sqrt(max(0, 1 - cosi * cosi)); 
                // Total internal reflection
                if (sint >= 1) { 
                    return 1;
                } 
                else { 
                    half cost = sqrt(max(0, 1 - sint * sint)); 
                    cosi = abs(cosi); 
                    half Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost)); 
                    half Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost)); 
                    return(Rs * Rs + Rp * Rp) / 2; 
                } 
                // As a consequence of the conservation of energy, transmittance is given by:
                // kt = 1 - kr;
            } 

            v2f vert (appdata_full v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                UNITY_TRANSFER_FOG(o,o.vertex);

                half3 wNormal = UnityObjectToWorldNormal(v.normal);
                half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
                o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
                o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
                o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a-_Cutoff);

                half3 tnormal = UnpackNormal(tex2D(_NormalMap, i.uv));
                half3 worldNormal;
                worldNormal.x = dot(i.tspace0, tnormal);
                worldNormal.y = dot(i.tspace1, tnormal);
                worldNormal.z = dot(i.tspace2, tnormal);

                worldNormal = normalize(worldNormal);

                half3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float3 refractedDirection = refract(worldViewDir, worldNormal, -1.0 / _RefractiveIndex);
                half4 refractData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, refractedDirection, 0);
                half3 refractColor = DecodeHDR (refractData, unity_SpecCube0_HDR);

                float3 reflectedDirection = -reflect(worldViewDir, worldNormal);
                half4 reflectData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectedDirection, 0);
                half3 reflectColor = DecodeHDR (reflectData, unity_SpecCube0_HDR);

                half fres = fresnel(worldViewDir, worldNormal, 1.0 / _RefractiveIndex);
                fixed4 c = 0;
                c.rgb = lerp(refractColor, reflectColor, fres) * _Color;
                c.a = tex.a;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
