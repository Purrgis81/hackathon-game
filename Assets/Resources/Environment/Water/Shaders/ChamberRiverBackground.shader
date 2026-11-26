Shader "Stone Chamber/River Background"
{
    Properties
    {
        _FlowHeight ("Dreamscape ripple pattern", 2D) = "gray" {}
        [Normal] _FlowNormal ("Dreamscape wave normal", 2D) = "bump" {}
        _DeepColor ("Deep water", Color) = (0.120, 0.411, 0.550, 1)
        _SurfaceColor ("Blue water", Color) = (0.245, 0.622, 0.694, 1)
        _RippleColor ("Ripple highlight", Color) = (0.411, 0.748, 0.762, 1)
        _FlowSpeed ("Flow speed (world units per second)", Range(0, 3)) = 0.72
        _RippleTiling ("Ripples per world unit", Range(0.05, 0.5)) = 0.21
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FlowHeight, _FlowNormal;
            half4 _DeepColor, _SurfaceColor, _RippleColor;
            float _FlowSpeed, _RippleTiling;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
            };

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            float RippleHash(float2 cell)
            {
                float3 p = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float SoftNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 blend = frac(position);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float bottom = lerp(RippleHash(cell), RippleHash(cell + float2(1, 0)), blend.x);
                float top = lerp(RippleHash(cell + float2(0, 1)), RippleHash(cell + 1.0), blend.x);
                return lerp(bottom, top, blend.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 world = input.worldPosition.xz;
                const float2 downstream = float2(0.8944, 0.4472);
                const float2 across = float2(-0.4472, 0.8944);
                float travel = _Time.y * _FlowSpeed;

                // Fixed bends give the current a path, while the two ripple scales drift at different speeds.
                float bend = (SoftNoise(world * 0.075) - 0.5) * 3.2;
                float2 current = world + across * bend;
                float2 swellUV = (current - downstream * travel) * _RippleTiling;
                float2 rippleWorld = current - (downstream * 0.56 + across * 0.21) * travel;
                float2 rippleUV = float2(dot(rippleWorld, float2(0.82, 0.57)),
                    dot(rippleWorld, float2(-0.57, 0.82))) * (_RippleTiling * 1.76) + float2(0.31, 0.57);

                half swell = tex2D(_FlowHeight, swellUV).r;
                half ripple = tex2D(_FlowHeight, rippleUV).r;
                half2 swellSlope = UnpackNormal(tex2D(_FlowNormal, swellUV + float2(0.19, 0.43))).xy;
                half2 rippleSlope = UnpackNormal(tex2D(_FlowNormal, rippleUV)).xy;
                rippleSlope = half2(0.82h * rippleSlope.x - 0.57h * rippleSlope.y,
                    0.57h * rippleSlope.x + 0.82h * rippleSlope.y);
                half2 slope = swellSlope * 0.62h + rippleSlope * 0.32h;

                // The broad reflections stay in world space and soften or sharpen as crests pass through them.
                half blue = SoftNoise(world * 0.044 + float2(4.2, 9.7));
                half reflectionPatch = SoftNoise((world + slope * 3.6) * 0.067 + float2(8.3, 2.5));
                half3 color = lerp(_DeepColor.rgb, _SurfaceColor.rgb, 0.40h + blue * 0.42h);
                color += (swell - 0.42h) * half3(0.05h, 0.078h, 0.087h);

                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                half3 waterNormal = normalize(half3(slope.x, 1.0h, slope.y));
                half facing = 1.0h - saturate(dot(waterNormal, viewDirection));
                half crest = smoothstep(0.32h, 0.57h, swell * 0.54h + ripple * 0.46h - slope.y * 0.20h);
                half reflection = smoothstep(0.28h, 0.82h, reflectionPatch);
                half sheen = crest * (0.075h + reflection * 0.23h + facing * facing * 0.09h);
                return half4(lerp(color, _RippleColor.rgb, sheen), 1);
            }
            ENDCG
        }
    }
}

