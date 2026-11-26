using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public sealed class AbyssLighting : MonoBehaviour
{
    Transform feet, shaft;
    Light overhead;
    Material slate, lightInk, water, lakeBedMaterial;
    Mesh lakeBedMesh;
    const float BackgroundWaterDepth = 3.6f;
    const float BackgroundWaterWidth = 240f;


    public void Prepare(Transform explorer, SpikeTile[] tiles, Mesh quad)
    {
        feet = explorer;
        slate = new Material(Resources.Load<Shader>("ChamberSlate"));
        lightInk = new Material(Resources.Load<Shader>("ChamberShaft"));
        var lake = Resources.Load<Material>("Environment/Water/Lake/MI_Water_MeadowsLake");
        var lakeModel = Resources.Load<GameObject>("Environment/Water/Lake/SM_WaterPlane_Medium");
        var lakeFilter = lakeModel ? lakeModel.GetComponentInChildren<MeshFilter>() : null;
        var lakeMesh = lakeFilter ? lakeFilter.sharedMesh : null;
        var bedShader = Resources.Load<Shader>("Environment/Water/Shaders/ChamberLakeBed");
        bool useLake = lake && lake.shader && lake.shader.isSupported && lakeMesh &&
            lakeMesh.bounds.size.x > 0 && lakeMesh.bounds.size.z > 0 &&
            bedShader && bedShader.isSupported;

        var deep = new GameObject("Flooded ruins far below").transform;
        deep.SetParent(explorer.parent, false);
        var surfacePosition = Vector3.down * BackgroundWaterDepth;
        deep.localPosition = surfacePosition;
        if (useLake)
        {
            water = new Material(lake);
            GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
            var bounds = lakeMesh.bounds;
            deep.localScale = new Vector3(BackgroundWaterWidth / bounds.size.x, 1,
                BackgroundWaterWidth / bounds.size.z);
            deep.localPosition -= Vector3.Scale(bounds.center, deep.localScale);
            CreateLakeBed(explorer.parent, surfacePosition, bedShader);
        }
        else
        {
            water = new Material(Resources.Load<Shader>("Environment/Water/Shaders/DrownedRuins"));
            deep.localRotation = Quaternion.Euler(90, 0, 0);
            deep.localScale = new Vector3(BackgroundWaterWidth, BackgroundWaterWidth, 1);
        }

        deep.gameObject.AddComponent<MeshFilter>().sharedMesh = useLake ? lakeMesh : quad;
        var depths = deep.gameObject.AddComponent<MeshRenderer>();
        depths.sharedMaterial=water;
        depths.shadowCastingMode=ShadowCastingMode.Off;
        depths.receiveShadows=false;

        for (int i = 0; i < tiles.Length; i++)
        {
            var cell = tiles[i].transform;
            var floor = cell.Find("floor");
            foreach (var surface in floor.GetComponentsInChildren<MeshRenderer>(true))
                surface.sharedMaterial = slate;
        }

        var lamp = new GameObject("Overhead tile light");
        lamp.transform.SetParent(explorer.parent,false);
        overhead = lamp.AddComponent<Light>();
        overhead.type = LightType.Spot;
        overhead.renderMode = LightRenderMode.ForcePixel;
        overhead.color = new Color(1,.91f,.75f);
        overhead.range = 9;
        overhead.spotAngle = 45;
        overhead.innerSpotAngle = 24;
        overhead.intensity = 1.8f;
        shaft = new GameObject("Dust in the shaft").transform;
        shaft.SetParent(lamp.transform.parent,false);
        shaft.gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
        var beam = shaft.gameObject.AddComponent<MeshRenderer>();
        beam.sharedMaterial = lightInk;
        beam.shadowCastingMode = ShadowCastingMode.Off;
        beam.receiveShadows = false;
        PositionLight();
    }

    void CreateLakeBed(Transform parent, Vector3 surfacePosition, Shader shader)
    {
        const int segments = 48;
        const int stride = segments + 1;
        const float width = BackgroundWaterWidth + 40;
        var vertices = new Vector3[stride * stride];
        var colors = new Color[vertices.Length];
        var triangles = new int[segments * segments * 6];
        int next = 0;
        for (int y = 0; y <= segments; y++)
        {
            for (int x = 0; x <= segments; x++)
            {
                int cell = y * stride + x;
                float px = (x / (float)segments - .5f) * width;
                float pz = (y / (float)segments - .5f) * width;
                float shelf = Mathf.PerlinNoise(px * .037f + 17, pz * .037f + 28);
                float fine = Mathf.PerlinNoise(px * .092f + 5, pz * .092f + 11);
                float depth = Mathf.Lerp(.85f, 8.5f, Mathf.SmoothStep(0, 1, shelf));
                vertices[cell] = new Vector3(px, -depth - fine * 1.5f, pz);
                colors[cell] = new Color((1 - shelf) * .7f + fine * .3f, 0, 0, 1);
                if (x == segments || y == segments) continue;
                triangles[next++] = cell;
                triangles[next++] = cell + stride;
                triangles[next++] = cell + 1;
                triangles[next++] = cell + 1;
                triangles[next++] = cell + stride;
                triangles[next++] = cell + stride + 1;
            }
        }

        lakeBedMesh = new Mesh { name = "Lake bed", vertices = vertices, colors = colors, triangles = triangles };
        lakeBedMesh.RecalculateNormals();
        lakeBedMaterial = new Material(shader);
        var bed = new GameObject("Submerged lake bed");
        bed.transform.SetParent(parent, false);
        bed.transform.localPosition = surfacePosition;
        bed.AddComponent<MeshFilter>().sharedMesh = lakeBedMesh;
        bed.AddComponent<MeshRenderer>().sharedMaterial = lakeBedMaterial;
    }

    public void Follow(Transform target){
        feet=target;overhead.enabled=target!=null;shaft.gameObject.SetActive(target!=null);
        if(!target)slate.SetVector("_Pool",new Vector4(9999,0,9999,0));
    }
    void OnPreCull() { if (feet) PositionLight(); }

    void PositionLight()
    {
        var target = feet.position+Vector3.up*.04f;
        var axis = new Vector3(-1.47f,5.15f,.55f);
        slate.SetVector("_Pool",target);
        overhead.transform.SetPositionAndRotation(target+axis,Quaternion.LookRotation(-axis));
        // Keep the ribbon edge-on to neither the opening camera nor the overhead camera.
        var side = Vector3.Cross(axis,transform.position-target).normalized;
        shaft.SetPositionAndRotation(target+axis*.5f,Quaternion.LookRotation(Vector3.Cross(side,axis),axis));
        shaft.localScale = new Vector3(2.1f,axis.magnitude,1);
    }

    void OnDestroy()
    {
        if (slate) Destroy(slate);
        if (lightInk) Destroy(lightInk);
        if (water) Destroy(water);
        if (lakeBedMaterial) Destroy(lakeBedMaterial);
        if (lakeBedMesh) Destroy(lakeBedMesh);

    }
}
