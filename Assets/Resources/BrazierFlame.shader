Shader "Stone Chamber/Brazier Flame"
{
    Properties { _Seed ("Flicker offset", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Seed;
            struct Varying { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varying vert(appdata_base v)
            {
                Varying o;
                float3 center = UnityObjectToViewPos(float3(0,0,0));
                center.xy += v.vertex.xy * float2(.82,.96);
                o.pos = mul(UNITY_MATRIX_P, float4(center,1));
                o.uv = v.texcoord.xy;
                return o;
            }
            fixed4 frag(Varying i) : SV_Target
            {
                float t = _Time.y + _Seed;
                float h = saturate((i.uv.y-.10) / (.78+.06*sin(10*t)));
                float x = (i.uv.x-.5) - h*h*(.12*sin(6*t+5*h)+.035*sin(15*t));
                float width = (.035+.22*sin(3.14159*pow(h,.64))) * (1-.63*h);
                float ends = smoothstep(.0,.12,h)*(1-smoothstep(.87,1,h));
                float flame = (1-smoothstep(.0,width,abs(x))) * ends;
                float halo = saturate(1-length((i.uv-float2(.5,.36))*float2(2.2,2.5)));
                float3 rgb = float3(.48,.12,.017)*pow(halo,3) + float3(1,.37,.035)*flame;
                rgb += float3(.70,.67,.32)*smoothstep(.39,.79,flame)*(1-h);
                return fixed4(rgb,0);
            }
            ENDCG
        }
    }
}
