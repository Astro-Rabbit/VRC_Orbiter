Shader "Orbiter/HologramStencilVolume"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry-10"
            "RenderType"="Opaque"
        }

        ColorMask 0
        ZWrite Off
        ZTest Always
        Cull Off

        Stencil
        {
            Ref [_StencilRef]
            Comp Always
            Pass Replace
        }

        Pass
        {
        }
    }
}