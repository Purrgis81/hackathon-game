Shader "Stone Chamber/Tidal Water"
{
 Properties { _Fade("Opacity",Range(0,1))=1 }
 SubShader{
 Tags{"Queue"="Transparent" "RenderType"="Transparent"}
 Pass{
 Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct V{float4 pos:POSITION;float2 uv:TEXCOORD0;};
 struct F{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
 float _Fade;
 F vert(V v){
  F f;float pulse=sin(v.uv.x*24+_Time.y*4)*.07;
  v.pos.y+=pulse*saturate(v.pos.y);f.world=mul(unity_ObjectToWorld,v.pos).xyz;
  f.pos=UnityObjectToClipPos(v.pos);f.uv=v.uv;return f;
 }
 fixed4 frag(F f):SV_Target{
  float flow=sin(f.world.x*7+f.world.z*9-_Time.y*8);
  float foam=step(.31,f.uv.y)*step(f.uv.y,.76);
  foam*=step(-.35,flow+sin(f.uv.x*110)*.4);
  float streak=step(.83,sin(f.uv.x*94+f.uv.y*18+_Time.y*6));
  float3 deep=lerp(float3(.015,.18,.32),float3(.05,.68,.78),saturate(f.world.y*.6));
  float3 color=lerp(deep,float3(.79,1,1),saturate(foam+streak*.35));
  return fixed4(color,_Fade*lerp(.83,1,foam));
 }
 ENDCG
 }
 }
}
