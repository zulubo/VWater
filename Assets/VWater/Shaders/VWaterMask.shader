Shader "Water/VWater Mask"
{
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+10"}
        //Stencil {
        //    Ref 2
        //    Comp Never
        //    Fail replace
        //}
        ZWrite On
        ColorMask 0
        Pass {}
    } 
}
