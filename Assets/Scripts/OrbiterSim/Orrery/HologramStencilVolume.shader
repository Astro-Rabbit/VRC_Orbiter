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
        Cull Back

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