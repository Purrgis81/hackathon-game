Shader "Stone Chamber/Bolt Streak"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(appdata_base v){V o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
            fixed4 frag(V i):SV_Target
            {
                float core=1-abs(2*i.uv.y-1);
                return fixed4(lerp(float3(.08,.54,.65),float3(.69,.98,.94),core*i.uv.x),i.uv.x*core*.84);
            }
            ENDCG
        }
    }
}
