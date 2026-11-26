Shader "Stone Chamber/Coastal Sea"
{
    Properties
    {
        _SurfaceNoise ("Wave noise", 2D) = "gray" {}
        _SurfaceDistortion ("Wave distortion", 2D) = "gray" {}
        _ShallowColor ("Shallow water", Color) = (.235, .69, .635, 1)
        _DeepColor ("Open water", Color) = (.098, .387, .577, 1)
        _SkyColor ("Sky reflection", Color) = (.55, .78, .85, 1)
        _SunTint ("Sun reflection", Color) = (1, .925, .735, 1)
        _FlowSpeed ("Flow speed", Range(0, 2)) = .72
        _ReflectionStrength ("Reflection strength", Range(0, 1)) = .68
        _RippleStrength ("Ripple contrast", Range(0, 1)) = .56
        _ShoreShadow ("Shore depth shading", Range(0, 1)) = .62
        _ShoreReflection ("Island reflection", Range(0, 1)) = .76
        _ShoreRipples ("Shore ripples", Range(0, 1)) = .64
        [HideInInspector] _ShoreBounds ("Island footprint", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ShoreHeight ("Island height above water", Float) = 0
        [HideInInspector] _ImpactStart ("Impact time", Float) = -1000
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct V
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            sampler2D _SurfaceNoise, _SurfaceDistortion;
            float4 _ShallowColor, _DeepColor, _SkyColor, _SunTint;
            float _FlowSpeed, _ReflectionStrength, _RippleStrength, _ImpactStart;
            float4 _ShoreBounds;
            float _ShoreShadow, _ShoreReflection, _ShoreRipples, _ShoreHeight;
            int _ImpactCount;
            float4 _ImpactTiles[19]; // Largest falling edge is 10x10 -> 9x9.

            V vert(appdata_base v)
            {
                V o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float ImpactFoam(float2 p, float2 distortion, float elapsed)
            {
                if (elapsed < 0 || elapsed >= 1.15 || _ImpactCount <= 0)
                    return 0;

                float nearestEdge = 1e4;
                for (int k = 0; k < min(_ImpactCount, 19); ++k)
                {
                    float2 q = abs(p - _ImpactTiles[k].xy) - _ImpactTiles[k].zw;
                    nearestEdge = min(nearestEdge, length(max(q, 0)) + min(max(q.x, q.y), 0));
                }

                float progress = elapsed / 1.15;
                float noise = tex2D(_SurfaceNoise, p * .26 + distortion * .14 + elapsed * .026).r;
                float radius = .12 + 1.35 * progress;
                float edge = nearestEdge + (noise - .5) * .27 - radius;
                float rim = 1 - smoothstep(.07, .32, abs(edge));
                float outerWave = 1 - smoothstep(.035, .135, abs(edge - .31));
                float breakup = smoothstep(.16, .54, noise + .22 * (1 - progress));
                return saturate((rim * breakup + outerWave * .22) * (1 - smoothstep(.43, 1, progress)));
            }

            float ShoreDistance(float2 p)
            {
                const float corner = .48;
                float2 q = abs(p - _ShoreBounds.xy) - _ShoreBounds.zw + corner;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - corner;
            }

            float3 ApplyShore(float3 water, float2 p, float3 eye, float2 bend, float2 slope, float time, float detail)
            {
                // Use the visible edge of the elevated board as the start of the shoreline.
                float2 viewSlope = eye.xz / max(.43, eye.y);
                float2 shorePoint = p + viewSlope * _ShoreHeight;
                float distanceToShore = ShoreDistance(shorePoint);
                if (_ShoreBounds.z <= 0 || distanceToShore > 6 || distanceToShore < -.45)
                    return water;

                // The submerged bank falls off farther out than the narrow reflected side.
                float bank = 1 - smoothstep(.18, 4.1, max(0, distanceToShore + bend.x * .18));
                water = lerp(water, water * float3(.73, .84, .83), bank * _ShoreShadow);

                // Project a short coloured silhouette back toward the bank along the reflected view ray.
                float2 reflectedView = -viewSlope;
                float2 warped = shorePoint + bend * .47 + slope * 3.5;
                float shimmer = lerp(.72, 1.12, saturate(.5 + 4 * dot(slope, reflectedView)));
                float nearHit = ShoreDistance(warped + reflectedView * .85);
                float highHit = ShoreDistance(warped + reflectedView * 3.4);
                float side = (1 - smoothstep(-.24, .39, highHit)) * smoothstep(-.4, .25, nearHit);
                float fade = (1 - smoothstep(.43, 3.45, distanceToShore)) * smoothstep(-.14, .32, distanceToShore);
                float broken = lerp(.76, 1, smoothstep(.30, .68, detail));
                float3 reflectedBank = lerp(water * .66, float3(.39, .28, .16), .32);
                water = lerp(water, reflectedBank, side * fade * broken * shimmer * _ShoreReflection);

                // Patchy wavelets have different reach and timing even along a straight tile edge.
                float pocket = dot(shorePoint, float2(.81, .97)) + bend.y * 2.1;
                float patch = smoothstep(.36, .85, .5 + .5 * sin(pocket - time * .48));
                float fragments = smoothstep(.39, .67, detail + .15 * bend.x) * patch;
                float wake = max(0, distanceToShore + .35 * bend.y);
                float phase = wake * 4.7 - time * 3.0 + .95 * sin(pocket) + bend.x * 2.3;
                float bands = smoothstep(.61, .99, .5 + .5 * sin(phase));
                float reach = lerp(1.1, 2.6, patch);
                float contact = smoothstep(.06, .41, distanceToShore) * (1 - smoothstep(.58, reach, wake));
                return lerp(water, _SkyColor.rgb, bands * fragments * contact * _ShoreRipples * .33);
            }

            fixed4 frag(V i) : SV_Target
            {
                float2 p = i.world.xz;
                float time = _Time.y * _FlowSpeed;
                float3 eye = normalize(_WorldSpaceCameraPos.xyz - i.world);

                // Coherent crests travel on one axis. A gentler crossing wave breaks up their spacing.
                const float2 travelAxis = float2(-.58, .815);
                const float2 crossAxis = float2(.815, .58);
                float2 flowPoint = p - travelAxis * time * 1.22;
                float2 bend = tex2D(_SurfaceDistortion, flowPoint * .048).rg * 2 - 1;
                float broad = tex2D(_SurfaceNoise, flowPoint * .064 + bend * .037).r;
                float detail = tex2D(_SurfaceNoise, flowPoint * .17 + bend * .042).r;

                float across = dot(p, crossAxis);
                float along = dot(flowPoint, travelAxis);
                float swell = along * .91 + bend.x * .65;
                float ripple = along * 1.97 + 1.23 * sin(across * .37 + time * .21) + .63 * sin(across * .87 + along * .24) + bend.y * .96;
                float crossing = dot(p, crossAxis) * 1.52 - time * .96 + bend.x * 1.12;
                const float2 smallAxis = float2(.27, .963);
                float wavelet = dot(p, smallAxis) * 3.12 - time * 2.92 + bend.y * 1.65;
                float wavePacket = lerp(.57, 1.14, smoothstep(.27, .73, broad));
                float2 slope = travelAxis * (.038 * cos(swell) + .088 * wavePacket * cos(ripple));
                slope += crossAxis * (.046 * cos(crossing) + .012 * bend.x);
                slope += smallAxis * (.025 * cos(wavelet));
                float3 normal = normalize(float3(-slope.x, 1, -slope.y));

                // A soft reef tint beneath the moving surface adds depth without competing with the tile grid.
                float2 reefPoint = p + eye.xz / max(.48, eye.y) * .68;
                float reef = tex2D(_SurfaceNoise, reefPoint * .027 + float2(.36, .18) + bend * .024).r;
                float openSea = smoothstep(8, 44, length(p - float2(1.15, 1.15)));
                float depth = saturate(.15 + .47 * openSea + (reef - .43) * .35);
                float3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);
                color *= .97 + (broad - .5) * .075 + .022 * sin(swell);

                float fresnel = pow(1 - saturate(dot(normal, eye)), 3);
                float3 reflected = reflect(-eye, normal);
                float sky = .10 + .18 * fresnel + .08 * saturate(reflected.z * .5 + .5);
                float horizon = pow(1 - saturate(reflected.y), 2);
                float3 skyReflection = lerp(_SkyColor.rgb, float3(.71, .72, .92), horizon * .58);
                color = lerp(color, skyReflection, sky * _ReflectionStrength);

                float3 sun = normalize(float3(-.28, .91, -.29));
                float specular = pow(saturate(dot(normal, normalize(sun + eye))), 24);
                float wave = .5 + .5 * sin(ripple);
                float edge = max(.022, fwidth(wave));
                float crest = smoothstep(.75 - edge, .97 + edge, wave);
                float breakup = smoothstep(.39, .65, broad * .61 + detail * .39);
                float meeting = smoothstep(.20, .82, .5 + .5 * sin(crossing));
                float glints = crest * breakup * lerp(.18, .74, meeting);
                glints += .16 * smoothstep(.70, .97, .5 + .5 * sin(wavelet)) * meeting;
                color += _SunTint.rgb * _ReflectionStrength * specular * (.027 + .16 * glints);
                color += lerp(_SkyColor.rgb, _SunTint.rgb, .18) * _RippleStrength * glints * (.104 + .055 * fresnel);

                color = ApplyShore(color, p, eye, bend, slope, time, detail);

                float foam = ImpactFoam(p, bend, _Time.y - _ImpactStart);
                color = lerp(color, float3(.88, .985, .935), foam * .89);
                // Fade distant ripples into the sky haze before the camera's far plane.
                float viewDepth = dot(i.world - _WorldSpaceCameraPos.xyz, -UNITY_MATRIX_V[2].xyz);
                float distanceHaze = smoothstep(95.0, _ProjectionParams.z * .78, viewDepth);
                float3 horizonTint = lerp(_DeepColor.rgb, _SkyColor.rgb, .86);
                return fixed4(lerp(color, horizonTint, distanceHaze), 1);
            }
            ENDCG
        }
    }
}
