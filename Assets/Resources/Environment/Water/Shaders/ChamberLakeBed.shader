Shader "Stone Chamber/Lake Bed"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Lambert addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        void surf(Input input, inout SurfaceOutput result)
        {
            half3 sand = lerp(half3(0.27, 0.46, 0.46), half3(0.58, 0.65, 0.50), input.color.r);
            result.Albedo = sand;
            result.Emission = sand * 0.31h;
        }
        ENDCG
    }
    Fallback "Diffuse"
}

