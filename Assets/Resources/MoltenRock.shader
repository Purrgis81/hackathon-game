Shader "Stone Chamber/Molten Rock"
{
 SubShader{
 Tags{"RenderType"="Opaque"}
 Pass{
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct F{float4 pos:SV_POSITION;float3 local:TEXCOORD0;float3 normal:TEXCOORD1;};
 F vert(appdata_base v){F f;f.pos=UnityObjectToClipPos(v.vertex);f.local=v.vertex.xyz;f.normal=v.normal;return f;}
 fixed4 frag(F f):SV_Target{
  float cracks=step(.35,sin(f.local.x*24+sin(f.local.y*20)+f.local.z*21));
  float shade=.55+.45*saturate(dot(normalize(f.normal),normalize(float3(-1,2,-1))));
  return fixed4(lerp(float3(.12,.035,.015)*shade,float3(1,.49,.035),cracks),1);
 }
 ENDCG
 }
 }
}
