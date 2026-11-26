Shader "Stone Chamber/Wooden Stake"
{
    Properties { _GroundY ("Grass surface height", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        float _GroundY;
        struct Input { float3 worldPos; float3 woodPosition; };
        void vert(inout appdata_full vertex, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            data.woodPosition=vertex.vertex.xyz;
        }
        void surf(Input data, inout SurfaceOutput surface)
        {
            clip(data.worldPos.y-_GroundY-.001);
            float3 p=data.woodPosition;
            float cut=step(.90+.35*p.x-.22*p.z,p.y);
            float3 wood=lerp(float3(.36,.19,.085),float3(.89,.68,.41),cut);
            surface.Albedo=wood;
            surface.Emission=wood*.12;
            surface.Alpha=1;
        }
        ENDCG
    }
}
