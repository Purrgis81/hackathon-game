Shader "Stone Chamber/Impact Dust"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Varying { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Varying vert(Input i)
            {
                Varying o; o.pos=UnityObjectToClipPos(i.vertex); o.uv=i.uv*2-1; o.color=i.color; return o;
            }
            fixed4 frag(Varying i):SV_Target
            {
                float r=length(i.uv)+.055*sin(9*i.uv.x+2*i.uv.y)*cos(8*i.uv.y);
                return fixed4(i.color.rgb,i.color.a*(1-smoothstep(.36,1,r)));
            }
            ENDCG
        }
    }
}
