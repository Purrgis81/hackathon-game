Shader "BeachResults/Hand Painted Props"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows addshadow noforwardadd
        #pragma target 3.0
        struct Input { float4 color : COLOR; };
        void Surf(Input input, inout SurfaceOutputStandard surface)
        {
            surface.Albedo = input.color.rgb;
            surface.Metallic = input.color.a * 0.45;
            surface.Smoothness = lerp(0.21, 0.64, input.color.a);
            surface.Emission = input.color.rgb * 0.055;
            surface.Occlusion = 1;
            surface.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
