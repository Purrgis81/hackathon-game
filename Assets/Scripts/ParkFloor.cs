using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public sealed class ParkFloor : MonoBehaviour
{
    // Pretty Park's nine slices, in source order A through I.
    [SerializeField] Mesh[] slices;

    public void Fit(int x, int z, int last)
    {
        int column = x == 0 ? 0 : x == last ? 2 : 1;
        int row = z == 0 ? 0 : z == last ? 2 : 1;
        GetComponent<MeshFilter>().sharedMesh = slices[row * 3 + column];
    }
}
