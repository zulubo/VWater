Shader "Water/VWater Surface"
{
    Properties
    {
        _AbsorptionColor("Absorption Color", Color) = (0.2, 0.3, 0.9, 1.0)
        _ScatteringColor("Scattering Color", Color) = (0.2, 0.3, 0.9, 1.0)
        _Density("Water Density Multiplier", Float) = 1.0

        _Smoothness("Smoothness", Range(0,1)) = 0.95
        _Transition ("Surface Transition Distance", Float) = 0.25

        [Space()]

        [NoScaleOffset] _Normal ("Water Normals", 2D) = "bump" {}

        _WaveScale("Waves Scale", Vector) = (10, 13, 6, 3)
        _WaveAmplitude("Waves Intensity", Vector) = (0.5, 0.4, 0.3, 0.2)
        _WaveSpeed("Waves Speed", Vector) = (0.5, 0.4, 0.3, 0.2)

        [Space()]

        _FoamMap("Foam Texture", 2D) = "grey" {}
        _FoamDepth ("Foam Intersection Depth", Float) = 0.4


        [Space()]

        _Refraction ("Refraction Strength", Float) = 0.5
        [Toggle(REFRACTION_DEPTH_CORRECTION)]
        _RefractionDepthCorrection ("Refraction Depth Correction", Float) = 1

		[Toggle(WIND_MAP)]
        _WindEnabled ("Wind Map Enabled", Float) = 0
		_WindMap ("Wind Map", 2D) = "white" {}
        
        [NoScaleOffset] _WaveBuffer("Water Buffer", 2D) = "black" {}

        [Toggle(FOG_ENABLED)]
        _FogEnabled("Fog Enabled", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-1"}
        LOD 100

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
            //#pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma shader_feature_local REFRACTION_DEPTH_CORRECTION
            #pragma shader_feature_local WIND_MAP
            #pragma shader_feature_local DYNAMICS_ENABLED
            #pragma shader_feature_local FOG_ENABLED
            #pragma target 4.0

            #define FORWARD_BASE_PASS
            
            #include "VWaterLighting.cginc"

            ENDCG
        }

        Pass {
            Name "FORWARDADD"
            Tags { "LightMode" = "ForwardAdd"}
            
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #pragma shader_feature_local REFRACTION_DEPTH_CORRECTION
            #pragma shader_feature_local WIND_MAP
            #pragma shader_feature_local DYNAMICS_ENABLED
            #pragma target 4.0

            #include "VWaterLighting.cginc"

            ENDCG
        }
        
     }

}
