Shader "Stone Chamber/Death Focus"
{
    Properties { _Focus ("Contact point", Vector) = (.5,.5,1,0)
                 _Ink ("Hazard colour", Color) = (1,.4,.1,1)
                 _Fade ("Fade to black", Range(0,1)) = 0
                 _Pulse ("Impact ripple", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Cull Off ZTest Always ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Focus, _Ink;
            float _Fade, _Pulse;
            struct Output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            Output vert(appdata_base v)
            {
                Output o; o.pos=float4(v.vertex.xy*2,.5,1);
                o.uv=v.vertex.xy+.5;
                #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x<0) o.pos.y=-o.pos.y;
                #endif
                return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float2 d=(i.uv-_Focus.xy)*float2(_Focus.z,1);
                float r=length(d);
                float ripple=(1-smoothstep(.001,.012,abs(r-lerp(.035,.22,_Pulse))))*(1-_Pulse);
                float rim=max(ripple,(1-smoothstep(.002,.005,abs(r-_Focus.w)))*.80);
                float shade=.64*smoothstep(.075,.34,r);
                float alpha=lerp(_Focus.w<0 ? 0 : max(rim,shade),1,_Fade);
                return fixed4(lerp(lerp(float3(.045,.042,.06),_Ink.rgb,rim),float3(.013,.012,.017),_Fade),alpha);
            }
            ENDCG
        }
    }
}
