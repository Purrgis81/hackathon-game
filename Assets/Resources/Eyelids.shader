Shader "Stone Chamber/Opening Eyes"
{
    Properties { _Open ("Eye opening", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Open;
            struct Output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            Output vert(appdata_base v)
            {
                Output o;
                o.pos=float4(v.vertex.xy*2,0.5,1);
                o.uv=v.texcoord.xy*2-1;
                return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float opening = _Open * 1.35 - .24*i.uv.x*i.uv.x;
                float mask = smoothstep(opening-.09,opening,abs(i.uv.y));
                return fixed4(.014,.012,.018,mask);
            }
            ENDCG
        }
    }
}
