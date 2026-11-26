Shader "Stone Chamber/Coastal Sea"
{
    Properties
    {
        _SurfaceNoise ("Roystan wave noise", 2D) = "gray" {}
        _SurfaceDistortion ("Roystan wave distortion", 2D) = "gray" {}
        [HideInInspector] _ImpactStart ("Impact time", Float) = -1000
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct V {float4 pos:SV_POSITION; float2 world:TEXCOORD0;};
            sampler2D _SurfaceNoise, _SurfaceDistortion;
            float _ImpactStart;
            int _ImpactCount;
            float4 _ImpactTiles[19]; // Largest falling edge is 10x10 -> 9x9.
            V vert(appdata_base v) { V o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xz; return o; }
            fixed4 frag(V i):SV_Target
            {
                float2 p=i.world;
                float t=_Time.y;
                // Roystan Toon Water: scroll a noise texture through an RG distortion field.
                float2 bend=tex2D(_SurfaceDistortion,p*.032+t*float2(.006,-.003)).rg*2-1;
                float2 uv=p*float2(.034,.072)+t*float2(.006,-.009)+bend*.15;
                float wave=tex2D(_SurfaceNoise,uv).r;
                float swell=sin(p.x*.22+p.y*.15-t*.36+bend.x*.8);
                float3 color=lerp(float3(.045,.31,.35),float3(.12,.56,.54),
                                  saturate(.30+.19*swell+.30*wave));
                color+=float3(.029,.060,.052)*smoothstep(.65,.83,wave);

                float age=t-_ImpactStart;
                if(age>=0 && age<1.15 && _ImpactCount>0)
                {
                    float distanceToEdge=1e4;
                    for(int k=0;k<_ImpactCount;k++)
                    {
                        float2 q=abs(p-_ImpactTiles[k].xy)-_ImpactTiles[k].zw;
                        distanceToEdge=min(distanceToEdge,length(max(q,0))+min(max(q.x,q.y),0));
                    }
                    float travel=age/1.15;
                    float radius=.12+1.35*travel;
                    float noise=tex2D(_SurfaceNoise,p*.26+bend*.14+t*.026).r;
                    float edge=distanceToEdge+(noise-.5)*.34;
                    float rim=1-smoothstep(.09,.34,abs(edge-radius));
                    float foam=rim*smoothstep(.16,.54,noise+.22*(1-travel))*(1-smoothstep(.45,1,travel));
                    color=lerp(color,float3(.78,.94,.86),foam*.94);
                }
                return fixed4(color,1);
            }
            ENDCG
        }
    }
}
