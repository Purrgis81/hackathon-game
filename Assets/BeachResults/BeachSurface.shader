Shader "BeachResults/Coast"
{
    Properties
    {
        _Sea ("Sea", Range(0,1)) = 0
        _Shore ("Shore at Z", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            float _Sea, _Shore;
            struct Interpolators { float4 pos : SV_POSITION; float3 world : TEXCOORD0; SHADOW_COORDS(1) };
            Interpolators Vert(appdata_base v)
            {
                Interpolators o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                return o;
            }
            float Hash(float2 v)
            {
                return frac(sin(dot(v, float2(127.1, 311.7))) * 43758.54);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            fixed4 Frag(Interpolators i) : SV_Target
            {
                float2 p = i.world.xz;
                float edge = _Shore + .37*sin(.52*p.x) + .21*sin(1.05*p.x+.5);
                float coast = p.y - edge;
                float drift = .13*sin(p.x*1.13+_Time.y*.92) + .10*sin(p.x*2.07-_Time.y*.51);
                float band = coast + drift + .16*sin(_Time.y*1.15);
                float grain = Noise(p*47.0) - .5;
                if (_Sea < .5)
                {
                    // Long wind ripples give way to smoother, darker sand at the tideline.
                    float dry = smoothstep(.10, 2.65, -coast);
                    float sweep = p.x * 2.1 + p.y * 7.6 + 1.55 * sin(p.x * .87 + p.y * .18);
                    float dune = Noise(p * .71);
                    float rib = sin(sweep + dune * 3.0);
                    float ridge = smoothstep(.48, .98, rib) - .46 * smoothstep(-.18, .66, -rib);
                    float detail = saturate(1.0 - 2.0 * fwidth(sweep));
                    float patches = smoothstep(.21, .81, Noise(p * .39 + 4.0));
                    half3 sand = lerp(half3(.68,.66,.61), half3(.91,.885,.825), dry);
                    sand += (dune-.5)*.060 + (Noise(p*2.7)-.5)*.027;
                    sand += ridge * detail * (.015+.035*patches) * (.48+.52*dry) * half3(.86,.92,1.0);
                    sand += grain * .040 * saturate(1.0 - length(fwidth(p*47.0))*.52);
                    float shellGrit = smoothstep(.72, .86, Noise(p*23.2 + 8.0));
                    sand -= shellGrit * .050 * saturate(1.0-length(fwidth(p*23.2))*.58);
                    sand += exp(-abs(band+.17)*9.0) * .027;
                    float2 q=abs(p-float2(0.05,0.49))-float2(3.17,.82);
                    float distanceToStage=length(max(q,0.0))+min(max(q.x,q.y),0.0);
                    float contact = (1-smoothstep(-.06,.37,distanceToStage)) * .16;
                    float light = SHADOW_ATTENUATION(i);
                    sand *= (1-contact) * lerp(half3(.73,.78,.85), half3(1,1,1), light);
                    return half4(sand,1);
                }
                float n = Noise(p*1.6 + float2(0.,_Time.y*.15));
                float n2 = Noise(p*3.2 + float2(_Time.y*-.06,_Time.y*.23));
                half3 sea = lerp(half3(.26,.76,.665),half3(.128,.445,.635),smoothstep(.2,19,coast));
                sea = lerp(sea,half3(.55,.71,.87),smoothstep(25,85,coast));
                float ripples = sin(p.y*5.4+n*4.2-_Time.y*1.7)*.5+.5;
                float crest = smoothstep(.88,.999,ripples)*(.24+.76*n2)*.13;
                float3 eye = normalize(_WorldSpaceCameraPos.xyz - i.world);
                float reflectedSky = pow(1 - saturate(eye.y), 3);
                sea = lerp(sea, half3(.63,.80,.985), reflectedSky * .20);
                sea += crest*half3(.69,.87,.99) + (n-.5)*.075;
                float foam = exp(-abs(band)*11.) * (.58+.42*Noise(p*15));
                float wash = exp(-abs(band-.28)*5.) * .26;
                sea = lerp(sea,half3(.91,.99,.96),saturate(foam+wash));
                return half4(sea,1);
            }
            ENDCG
        }
    }
}
