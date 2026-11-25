Shader "Stone Chamber/Hazard Border"
{
    Properties
    {
        _Progress ("Time to strike (normalized elapsed)", Range(0, 1)) = 0
        _Order ("Eruption order", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+10" "RenderType"="TransparentCutout" }
        Pass
        {
            Cull Off
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Vertex { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Progress, _Order;
            Varying vert(Vertex input)
            {
                Varying output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }
            fixed4 frag(Varying input) : SV_Target
            {
                float2 p = abs(input.uv * 2 - 1);
                float edge = max(p.x, p.y);
                if (_Order < .5)
                {
                    if (_Order < -3.5)
                    {
                        float2 q=input.uv*2-1;
                        float arrow=step(abs(q.y-.15+abs(q.x)*.75),.075)*step(abs(q.x),.48);
                        float diamond=step(abs(abs(q.x)+abs(q.y)-.48),.065);
                        float cross=step(abs(abs(q.x)-abs(q.y)),.06)*step(edge,.45);
                        float safe=step(-4.5,_Order);
                        float returnRisk=1-step(-5.5,_Order);
                        float icon=lerp(cross,diamond,safe);
                        clip(max(step(.92,edge),max(icon,arrow*returnRisk))-.5);
                        float3 color=lerp(float3(1,.44,.07),float3(.27,1,.83),safe);
                        color=lerp(color,float3(1,.36,.92),returnRisk);
                        return fixed4(color,1);
                    }
                    if (_Order < -2.5)
                    {
                        float bars=step(abs(p.y-.4-.32*p.x),.075)*step(p.x,.66);
                        clip(max(step(.92,edge),bars)-.5);
                        return lerp(fixed4(.16,.55,.94,1),fixed4(.64,.94,1,1),_Progress);
                    }
                    if (_Order < -1.5)
                    {
                        float crack=abs(input.uv.x-.48-.13*sin(floor(input.uv.y*5)*2));
                        clip(max(step(crack,.032+.03*_Progress),step(.90,edge))-.5);
                        return lerp(fixed4(1,.62,.12,1),fixed4(1,.16,.04,1),_Progress);
                    }
                    if (_Order < -.5)
                    {
                        float diamond=abs(p.x+p.y-.48);
                        clip(max(step(diamond,.065),step(.94,edge))-.5);
                        return fixed4(.38,.93,.74,1);
                    }
                    float hatch=step(.83,frac((input.uv.x+input.uv.y)*5));
                    float band=step(.87-.11*_Progress,edge);
                    clip(max(step(.96,edge),band*hatch)-.5);
                    return lerp(fixed4(.94,.23,.11,1),fixed4(1,.64,.23,1),_Progress);
                }
                float rim = step(0.93 - 0.05 * _Progress, edge);
                float inner = step(abs(edge - lerp(0.85, 0.34, _Progress)), 0.021);
                clip(max(rim,inner) - .5);
                return lerp(fixed4(1,.29,.08,1),fixed4(1,.86,.38,1),_Progress);
            }
            ENDCG
        }
    }
}
