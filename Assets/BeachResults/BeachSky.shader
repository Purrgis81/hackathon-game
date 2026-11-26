Shader "BeachResults/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; };
            struct Varying { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
            Varying Vert(Input v) { Varying o; o.vertex=UnityObjectToClipPos(v.vertex); o.direction=v.vertex.xyz; return o; }
            float H(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.54); }
            float N(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(H(i),H(i+float2(1,0)),f.x),lerp(H(i+float2(0,1)),H(i+1),f.x),f.y);
            }
            fixed4 Frag(Varying v):SV_Target
            {
                float3 d=normalize(v.direction);
                float height=saturate(d.y*2.8);
                half3 color=lerp(half3(.77,.88,.86),half3(.32,.69,.84),height);
                float sun=dot(d,normalize(float3(-.38,.18,.94)));
                color+=half3(.17,.125,.065)*pow(saturate(sun),16);
                color=lerp(color,half3(1,.95,.76),smoothstep(.9991,.99965,sun));
                float2 q=d.xz / max(.05,d.y+.10)*2.5;
                float clouds=smoothstep(.58,.78,.68*N(q)+.23*N(q*2.1)+.09*N(q*5.1));
                color=lerp(color,half3(.96,.98,.92),clouds*smoothstep(.01,.105,d.y)*.69);
                return half4(color,1);
            }
            ENDCG
        }
    }
}
