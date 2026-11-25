Shader "Stone Chamber/Suspended Dust"
{
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(appdata_base v) { V o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(41.3,289.1)))*43174.31); }
            fixed4 frag(V i):SV_Target
            {
                float y=i.uv.y;
                float x=(i.uv.x-.5)/(1-.49*y);
                float edge=1-smoothstep(.08,.49,abs(x));
                float ends=smoothstep(.0,.12,y)*(1-smoothstep(.70,1,y));
                float stripe=.55+.45*cos(x*26+y*.65);
                float2 field=float2(x*18+.17*sin(y*7+_Time.y*.45),y*17+_Time.y*.72);
                float seed=hash(floor(field));
                float mote=(1-smoothstep(.065,.16,length(frac(field)-float2(.18+.63*seed,.5))))*step(.925,seed);
                return fixed4(.94,.86,.66,ends*edge*(.070+.072*stripe+.56*mote));
            }
            ENDCG
        }
    }
}
