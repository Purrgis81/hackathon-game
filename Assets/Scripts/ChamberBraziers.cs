using UnityEngine;
using UnityEngine.Rendering;

public sealed class ChamberBraziers : MonoBehaviour
{
    readonly Light[] fires = new Light[2];
    Material flame, bowl;
    Mesh bowlMesh;

    static Mesh MakeBowl()
    {
        const int sides = 12;
        // Cross section from the foot up the outer wall and down into the coals.
        var profile = new[] { new Vector2(.20f,0), new Vector2(.43f,.15f),
            new Vector2(.38f,.18f), new Vector2(.27f,.09f), new Vector2(0,.085f) };
        var vertices = new Vector3[sides * profile.Length];
        var triangles = new int[sides * (profile.Length-1) * 6];
        int index = 0;
        for (int j = 0; j < profile.Length; j++)
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2 / sides;
            int a = j * sides + i;
            vertices[a] = new Vector3(Mathf.Cos(angle)*profile[j].x,profile[j].y,Mathf.Sin(angle)*profile[j].x);
            if (j == profile.Length-1) continue;
            int b = j * sides + (i+1)%sides;
            triangles[index++] = a; triangles[index++] = a+sides; triangles[index++] = b;
            triangles[index++] = b; triangles[index++] = a+sides; triangles[index++] = b+sides;
        }
        var mesh = new Mesh { name = "Iron brazier", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        return mesh;
    }

    public void Prepare(Mesh quad)
    {
        flame = new Material(Resources.Load<Shader>("BrazierFlame"));
        bowl = new Material(Shader.Find("Standard")) { color = new Color(.065f,.042f,.032f) };
        bowl.SetFloat("_Glossiness", .18f);
        bowlMesh = MakeBowl();
        for (int i = 0; i < fires.Length; i++)
        {
            var lamp = new GameObject("Pillar flame");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(i == 0 ? -3.7f : 3.7f, 1.89f, 3.6f);
            var dish = new GameObject("Iron fire bowl");
            dish.transform.SetParent(lamp.transform,false);
            dish.transform.localPosition = Vector3.down * .45f;
            dish.AddComponent<MeshFilter>().sharedMesh = bowlMesh;
            dish.AddComponent<MeshRenderer>().sharedMaterial = bowl;
            lamp.AddComponent<MeshFilter>().sharedMesh = quad;
            var surface = lamp.AddComponent<MeshRenderer>();
            surface.sharedMaterial = flame;
            surface.shadowCastingMode = ShadowCastingMode.Off;
            surface.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_Seed", i * 2.1f);
            surface.SetPropertyBlock(properties);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .53f, .21f);
            light.range = 6.8f;
            light.intensity = 2.25f;
            fires[i] = light;
        }
    }

    void Update()
    {
        for (int i = 0; i < fires.Length; i++)
            fires[i].intensity = 2f + .6f * Mathf.PerlinNoise(Time.time * 3.6f, i * 3.1f);
    }

    void OnDestroy()
    {
        if (flame) Destroy(flame);
        if (bowl) Destroy(bowl);
        if (bowlMesh) Destroy(bowlMesh);
    }
}
