Shader "Stone Chamber/Fractured Slate"
{
    Properties { _Pool ("Light on the floor", Vector) = (0,0,0,0) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.0
        struct Input { float3 worldPos; float3 worldNormal; };
        float4 _Pool;
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        void surf(Input i,inout SurfaceOutput o)
        {
            float3 p=i.worldPos;
            float2 tile=floor(p.xz/2.32+.5);
            float2 local=p.xz-tile*2.32;
            float seed=hash(tile);
            float top=smoothstep(.5,.85,i.worldNormal.y);
            float2 texel=floor(p.xz*25)/25;
            float grain=hash(texel);
            float edge=max(abs(local.x),abs(local.y));
            float rim=(1-smoothstep(.021,.041,abs(edge-.938)));
            float corners=rim*step(.65,min(abs(local.x),abs(local.y)));
            float seam=abs(local.x+local.y*.56+floor(texel.y*6)*.047-.99*(seed-.5));
            float crack=(1-smoothstep(.009,.029,seam))*smoothstep(.45,.91,edge);
            float3 stone=lerp(float3(.53,.392,.267),float3(.72,.588,.409),seed*.68+grain*.08);
            stone+=rim*float3(.05,.045,.032)+corners*float3(.108,.086,.034)-crack*.062;
            float strata=frac(-p.y*5.3+floor((p.x+p.z)*7)*.031+seed);
            float depth=exp(min(0,p.y)*2.0);
            float3 cliff=float3(.30,.223,.175)*depth*(.7+.4*step(.79,strata));
            float emerge=smoothstep(-8,.0,p.y);
            o.Albedo=lerp(cliff,stone*emerge,top);
            float2 offset=(p.xz-_Pool.xz)*float2(.93,1.04);
            float pool=exp(-dot(offset,offset)*1.65);
            o.Emission=lerp(float3(.021,.015,.012)+cliff*.30,(stone*.26+pool*float3(.145,.108,.061))*emerge,top);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
