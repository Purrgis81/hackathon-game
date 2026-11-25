Shader "Stone Chamber/Drowned Ruins"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct V {float4 pos:SV_POSITION; float2 world:TEXCOORD0;};
            V vert(appdata_base v) { V o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xz; return o; }
            float hash(float2 p) {return frac(sin(dot(p,float2(41.37,287.9)))*43758.5);}
            float masonry(float2 p)
            {
                float ring=length(p-float2(2,4));
                float sector=floor((atan2(p.y-4,p.x-2)+3.142)*13);
                float arc=(1-smoothstep(.22,.30,abs(ring-8.4)))*step(.23,hash(float2(sector,4)));
                float2 grid=abs(frac(p/5.2+.5)-.5)*5.2;
                float2 cell=floor(p/5.2+.5);
                float pier=1-smoothstep(.49,.57,max(grid.x,grid.y));
                float wall=(1-smoothstep(.18,.24,grid.y))*step(.64,hash(cell+9));
                return max(arc,max(wall,pier));
            }
            fixed4 frag(V i):SV_Target
            {
                float2 w=i.world;
                float2 p=float2(w.x*.92+w.y*.39,w.y*.92-w.x*.39);
                float t=_Time.y;
                float swell=sin(w.y*2.5+.54*sin(w.x*1.7+t*.29)+t*.48);
                float mist=.5+.5*sin(w.x*.38+w.y*.24+.95*sin(w.y*.63));
                float gleam=smoothstep(.85,1,swell)*smoothstep(.15,.93,mist);
                float3 water=lerp(float3(.024,.042,.073),float3(.062,.129,.163),mist)+gleam*float3(.022,.052,.052);
                float2 reflected=p+float2(.13*swell,-.67);
                water=lerp(water,float3(.030,.068,.087),masonry(reflected)*.6);
                float base=masonry(p);
                float cap=masonry(p-float2(.04,.26));
                float joint=.77+.23*step(.1,frac((p.x+p.y)*1.68));
                float3 ruins=lerp(float3(.043,.078,.106),float3(.100,.155,.176)*joint,cap);
                return fixed4(lerp(water,ruins,max(base,cap)),1);
            }
            ENDCG
        }
    }
}
