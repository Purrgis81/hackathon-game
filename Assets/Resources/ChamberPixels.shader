Shader "Stone Chamber/Pixel Grade"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Grid;
            float bayer2(float2 p)
            {
                p=fmod(floor(p),2);
                return 2*p.x+3*p.y-4*p.x*p.y;
            }
            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 color=tex2D(_MainTex,i.uv).rgb;
                float2 px=floor(i.uv*_Grid.xy);
                float d=(4*bayer2(px)+bayer2(px*.5)+.5)/16;
                float3 levels=float3(30,35,39);
                float3 poster=floor(color*levels+d)/levels;
                // Keep a deep ink floor without a checkerboard in empty space.
                return fixed4(lerp(color,poster,smoothstep(.035,.16,max(color.r,max(color.g,color.b)))*.86),1);
            }
            ENDCG
        }
    }
}
