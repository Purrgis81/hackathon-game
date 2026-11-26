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
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float _Sea, _Shore;
            struct Interpolators { float4 clip : SV_POSITION; float3 world : TEXCOORD0; };
            Interpolators Vert(appdata_base v)
            {
                Interpolators o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
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
                    half3 sand = lerp(half3(.96,.82,.58), half3(.97,.90,.74), saturate(-coast/4.3));
                    sand += grain*.055 + (Noise(p*2.7)-.5)*.05;
                    float2 q=abs(p-float2(0.05,0.49))-float2(3.17,.82);
                    float distanceToStage=length(max(q,0.0))+min(max(q.x,q.y),0.0);
                    float shadow=(1-smoothstep(-.1,.51,distanceToStage))*.22;
                    float palm=exp(-length((p-float2(-4.8,.26))*float2(.78,1.03))*2.1)*.22
                              +exp(-length((p-float2(6.05,2.60))*float2(.85,1.08))*2.3)*.19;
                    sand *= 1-shadow-palm-.09*exp(-abs(band+.02)*3);
                    return half4(sand,1);
                }
                float n = Noise(p*1.6 + float2(0.,_Time.y*.15));
                float n2 = Noise(p*3.2 + float2(_Time.y*-.06,_Time.y*.23));
                half3 sea = lerp(half3(.27,.75,.66),half3(.09,.51,.72),smoothstep(.2,19,coast));
                sea = lerp(sea,half3(.43,.70,.80),smoothstep(25,85,coast));
                float ripples = sin(p.y*5.4+n*4.2-_Time.y*1.7)*.5+.5;
                float crest = smoothstep(.88,.999,ripples)*(.24+.76*n2)*.13;
                sea += crest*half3(.68,.87,.96) + (n-.5)*.09;
                float foam = exp(-abs(band)*11.) * (.58+.42*Noise(p*15));
                float wash = exp(-abs(band-.28)*5.) * .26;
                sea = lerp(sea,half3(.91,.99,.96),saturate(foam+wash));
                return half4(sea,1);
            }
            ENDCG
        }
    }
}
