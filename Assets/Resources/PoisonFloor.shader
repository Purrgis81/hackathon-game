Shader "Stone Chamber/Poison Floor"
{
    Properties { _Fill ("Time remaining", Range(0,1)) = 1
                 _Preview ("Warning", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Cull Off
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct V { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct F { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Fill, _Preview;
            F vert(V v) { F f; f.pos=UnityObjectToClipPos(v.pos); f.uv=v.uv; return f; }
            fixed4 frag(F f) : SV_Target
            {
                float2 p = f.uv*2-1;
                float edge = max(abs(p.x),abs(p.y));
                float radius = length(p);
                float ring = step(abs(radius-.49),.058);
                float angle = frac(atan2(p.x,p.y)/6.2831853+1);
                float stripes = step(.76, frac((p.x+p.y)*2.8));
                float3 ink = float3(.16,.055,.24);
                float3 lilac = float3(.94,.75,1);
                if (_Preview>.5)
                {
                    clip(max(ring,(1-step(.048,abs(radius-.86)))*stripes)-.5);
                    return fixed4(.57,.18,.85,1);
                }
                float t=_Time.y;
                float flow=sin(9*p.x+5*p.y+t*2.7+2*sin(7*p.y-t));
                float contour=radius+.043*sin(angle*31.416+t*.8)+.03*sin(angle*18.85-t*.66);
                clip(.94-contour);
                float body=1-smoothstep(.74,.94,contour);
                float2 bubble=frac(f.uv*3.7+float2(.13*sin(t+p.y),t*.17))-.5;
                float bubbles=1-smoothstep(.02,.062,abs(length(bubble)-.14));
                float3 pool=lerp(float3(.66,.21,.78),float3(.34,.065,.47),body);
                pool+=body*(smoothstep(.75,.99,flow)*float3(.16,.033,.20)+bubbles*float3(.25,.24,.29));
                float drain=(1-smoothstep(.014,.038,abs(contour-.82)))*step(angle,_Fill);
                pool=lerp(pool,lilac,drain*.72);
                // Darken the liquid at the end, keeping its contact outline visible until expiry.
                pool=lerp(pool*.46+float3(.06,.02,.075),pool,smoothstep(0,.34,_Fill));
                return fixed4(pool,1);
            }
            ENDCG
        }
    }
}
