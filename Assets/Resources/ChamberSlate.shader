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
            float3 stone=lerp(float3(.22,.29,.35),float3(.38,.42,.46),seed*.68+grain*.10);
            stone+=rim*.031+corners*float3(.16,.13,.07)-crack*.085;
            float strata=frac(-p.y*5.3+floor((p.x+p.z)*7)*.031+seed);
            float depth=exp(min(0,p.y)*2.0);
            float3 cliff=float3(.18,.24,.30)*depth*(.7+.4*step(.79,strata));
            float emerge=smoothstep(-8,.0,p.y);
            o.Albedo=lerp(cliff,stone*emerge,top);
            float2 offset=(p.xz-_Pool.xz)*float2(.93,1.04);
            float pool=exp(-dot(offset,offset)*1.65);
            o.Emission=lerp(float3(.005,.008,.016)+cliff*.28,(stone*.16+pool*float3(.24,.185,.105))*emerge,top);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
