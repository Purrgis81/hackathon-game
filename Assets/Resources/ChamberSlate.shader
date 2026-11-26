Shader "Stone Chamber/Coastal Turf"
{
    Properties
    {
        _LightGrass ("Light grass", Color) = (.42,.56,.32,1)
        _DarkGrass ("Dark grass", Color) = (.37,.50,.28,1)
        _Pool ("Light on the floor", Vector) = (9999,0,9999,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows vertex:vert
        #pragma target 3.0
        struct Input { float3 worldPos; float3 worldNormal; float depth; float grassTone; };
        fixed4 _LightGrass, _DarkGrass;
        float4 _Pool;
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.depth = -v.vertex.y;
            // Each floor pivot stays at the centre of its grid cell, including during a lift.
            float2 cell = round(unity_ObjectToWorld._m03_m23 / 2.32);
            o.grassTone = frac((cell.x + cell.y) * .5) * 2;
        }
        void surf(Input i, inout SurfaceOutput o)
        {
            float2 p = i.worldPos.xz;
            float3 grass = lerp(_LightGrass.rgb,_DarkGrass.rgb,i.grassTone);
            float depth = i.depth;
            float lip = .17 + .025*sin(p.x*2+p.y*1.7);
            float3 earth = lerp(float3(.44,.245,.13),float3(.30,.155,.092),saturate(depth*.73));
            float3 turf = lerp(grass*.78,grass,saturate(i.worldNormal.y));
            float3 paint = lerp(turf,earth,smoothstep(lip,lip+.018,depth));
            float2 offset = i.worldPos.xz-_Pool.xz;
            float pool = exp(-dot(offset,offset)*1.6);
            o.Albedo = paint;
            o.Emission = paint*.18 + pool*float3(.085,.065,.03)*saturate(i.worldNormal.y);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
