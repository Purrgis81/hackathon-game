Shader "Stone Chamber/Pixel Surface"
{
    Properties
    {
        _MainTex ("Base texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelHeight ("Vertical pixel resolution", Range(128,512)) = 256
        _TextureGrid ("Texture grid", Range(64,512)) = 256
        _ShadeSteps ("Light bands", Range(3,12)) = 7
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Pixel fullforwardshadows vertex:SnapVertex exclude_path:deferred
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        float _PixelHeight, _TextureGrid, _ShadeSteps;

        void SnapVertex(inout appdata_full v)
        {
            #if !defined(UNITY_PASS_SHADOWCASTER) && !defined(UNITY_PASS_META)
            float4 clip = UnityObjectToClipPos(v.vertex);
            if (clip.w > 0.001)
            {
                float block = max(1.0, round(_ScreenParams.y / _PixelHeight));
                float2 grid = _ScreenParams.xy / block;
                float2 screen = (clip.xy / clip.w * 0.5 + 0.5) * grid;
                float2 shift = (round(screen) - screen) * (2.0 * clip.w) / grid;
                float3 viewShift = float3(shift / float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11), 0);
                float3 worldShift = mul(viewShift, (float3x3)UNITY_MATRIX_V);
                v.vertex.xyz += mul((float3x3)unity_WorldToObject, worldShift);
            }
            #endif
        }

        struct Input { float2 uv_MainTex; };
        void surf(Input i, inout SurfaceOutput o)
        {
            float2 uv = i.uv_MainTex;
            float2 sampleUV = (floor(uv * _TextureGrid) + 0.5) / _TextureGrid;
            // Derivatives from the original UVs keep the mip level stable inside a pixel.
            fixed3 paint = tex2Dgrad(_MainTex, sampleUV, ddx(uv), ddy(uv)).rgb;
            o.Albedo = paint * _Color.rgb;
            o.Alpha = 1;
        }

        half4 LightingPixel(SurfaceOutput s, half3 lightDir, half atten)
        {
            half diffuse = saturate(dot(s.Normal, lightDir));
            half band = floor(diffuse * _ShadeSteps + 0.5h) / _ShadeSteps;
            return half4(s.Albedo * _LightColor0.rgb * (band * atten), s.Alpha);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
