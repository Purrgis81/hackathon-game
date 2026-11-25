Shader "Stone Chamber/Lava Target"
{
 Properties{_Progress("Warning",Range(0,1))=0 _ImpactAge("Time since impact",Float)=-1 _Hot("Danger duration",Float)=.3}
 SubShader{
 Tags{"Queue"="AlphaTest+12" "RenderType"="TransparentCutout"}
 Pass{
 Cull Off ZWrite Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct F{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 float _Progress,_ImpactAge,_Hot;
 F vert(appdata_base v){F f;f.pos=UnityObjectToClipPos(v.vertex);f.uv=v.texcoord.xy;return f;}
 fixed4 frag(F f):SV_Target{
  float2 p=f.uv*2-1;float edge=max(abs(p.x),abs(p.y));
  if(_ImpactAge<0){
   float ring=step(abs(edge-.92),.035);
   float closing=step(abs(edge-lerp(.82,.17,_Progress)),.035);
   float cross=step(abs(abs(p.x)-abs(p.y)),.045)*step(edge,.30);
   clip(max(ring,max(closing,cross))-.5);
   return fixed4(lerp(float3(1,.27,.035),float3(1,.89,.28),_Progress),1);
  }
  clip(.96-edge);
  float cracks=step(.55,sin(p.x*14+sin(p.y*13)*2))*step(.2,sin(p.y*19+p.x*7));
  float hot=step(_ImpactAge,_Hot);
  float splash=1-smoothstep(.0,.11,abs(length(p)-_ImpactAge*4));
  float3 molten=lerp(float3(1,.24,.01),float3(1,.88,.18),cracks*.55+splash*.4);
  float3 cold=lerp(float3(.06,.025,.018),float3(.28,.065,.01),cracks)*saturate(1-(_ImpactAge-_Hot)/.42);
  return fixed4(lerp(cold,molten,hot),1);
 }
 ENDCG
 }
 }
}
