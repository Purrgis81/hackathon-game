using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public sealed class AbyssLighting : MonoBehaviour
{
    Transform feet, shaft;
    Light overhead;
    Material slate, lightInk;


    public void Prepare(Transform explorer, SpikeTile[] tiles, Mesh quad)
    {
        feet = explorer;
        slate = new Material(tiles[0].transform.Find("floor").GetComponent<MeshRenderer>().sharedMaterial);
        lightInk = new Material(Resources.Load<Shader>("ChamberShaft"));
        foreach (var tile in tiles)
            tile.transform.Find("floor").GetComponent<MeshRenderer>().sharedMaterial = slate;

        var lamp = new GameObject("Overhead tile light");
        lamp.transform.SetParent(explorer.parent,false);
        overhead = lamp.AddComponent<Light>();
        overhead.type = LightType.Spot;
        overhead.renderMode = LightRenderMode.ForcePixel;
        overhead.color = new Color(1,.89f,.69f);
        overhead.range = 9;
        overhead.spotAngle = 37;
        overhead.innerSpotAngle = 17;
        overhead.intensity = 2.3f;
        shaft = new GameObject("Dust in the shaft").transform;
        shaft.SetParent(lamp.transform.parent,false);
        shaft.gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
        var beam = shaft.gameObject.AddComponent<MeshRenderer>();
        beam.sharedMaterial = lightInk;
        beam.shadowCastingMode = ShadowCastingMode.Off;
        beam.receiveShadows = false;
        PositionLight();
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

    }
}
