Shader "Stone Chamber/Pastel Coast Sky"
{
    Properties
    {
        _Zenith ("Lavender sky", Color) = (.49,.46,.76,1)
        _Horizon ("Horizon haze", Color) = (.73,.87,.98,1)
        _Cloud ("Cloud color", Color) = (1,.98,.94,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float4 _Zenith, _Horizon, _Cloud;
            struct Input { float4 vertex : POSITION; };
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Input input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.54); }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 blend = frac(p);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), blend.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), blend.x), blend.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float height = direction.y;
                half3 color = lerp(_Horizon.rgb, _Zenith.rgb, smoothstep(-.014,.38,height));
                float sunlight = saturate(dot(direction, normalize(float3(-.38,.32,.87))));
                color += half3(.085,.066,.035) * pow(sunlight,12);

                float2 cloudPoint = direction.xz * 2.6 / max(.13,height + .19);
                float clouds = smoothstep(.58,.75,.73*Noise(cloudPoint)+.27*Noise(cloudPoint*2.4));
                color = lerp(color,_Cloud.rgb,clouds*smoothstep(.018,.12,height)*.54);

                // Two distant silhouettes stay low in the haze.
                float angle = atan2(direction.x,direction.z);
                float ridge = .053 + .028*sin(angle*5.0+.63) + .016*sin(angle*9.0-.7) + .008*sin(angle*21.0);
                float foothills = .020 + .012*sin(angle*7.0-.7) + .005*sin(angle*19.0);
                color = lerp(color,_Horizon.rgb*half3(.77,.84,.96),(1-smoothstep(ridge-.002,ridge+.002,height))*.55);
                color = lerp(color,_Horizon.rgb*half3(.69,.84,.92),(1-smoothstep(foothills-.002,foothills+.002,height))*.58);
                return half4(color,1);
            }
            ENDCG
        }
    }
}