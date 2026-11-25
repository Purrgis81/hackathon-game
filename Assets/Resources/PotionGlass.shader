Shader "Stone Chamber/Apothecary Glass"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct V {float4 pos:SV_POSITION;float3 n:TEXCOORD0;float3 eye:TEXCOORD1;};
            V vert(appdata_base v) {V o;o.pos=UnityObjectToClipPos(v.vertex);o.n=UnityObjectToWorldNormal(v.normal);o.eye=WorldSpaceViewDir(v.vertex);return o;}
            fixed4 frag(V i):SV_Target
            {
                float3 n=normalize(i.n);
                float rim=pow(1-abs(dot(n,normalize(i.eye))),2);
                float streak=smoothstep(.74,.95,dot(n,normalize(float3(-.62,.55,-.55))));
                return fixed4(lerp(float3(.4,.73,.83),float3(.9,1,1),streak),.12+.59*rim+.49*streak);
            }
            ENDCG
        }
    }
}
