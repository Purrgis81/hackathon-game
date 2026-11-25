Shader "Stone Chamber/Tidal Guide"
{
 Properties{_Span("Width in tiles",Float)=1 _Length("Length in tiles",Float)=4}
 SubShader{
 Tags{"Queue"="AlphaTest+8" "RenderType"="TransparentCutout"}
 Pass{
 Cull Off ZWrite Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct F{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 float _Span,_Length;
 F vert(appdata_base v){F f;f.pos=UnityObjectToClipPos(v.vertex);f.uv=v.texcoord.xy;return f;}
 fixed4 frag(F f):SV_Target{
  float edge=step(.475,abs(f.uv.x-.5));
  float2 cell=float2(frac(f.uv.x*_Span)-.5,frac(f.uv.y*_Length-_Time.y*.65)-.5);
  float arrow=step(abs(cell.y-.12+abs(cell.x)*.72),.045)*step(abs(cell.x),.29);
  clip(max(edge,arrow)-.5);return fixed4(.18,.88,1,1);
 }
 ENDCG
 }
 }
}
