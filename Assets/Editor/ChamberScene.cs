using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ChamberScene
{
    const string ScenePath = "Assets/Scenes/StoneChamber.unity";
    const string Materials = "Assets/Art/Materials/";
    const string Coast = "Assets/Art/Coast/";

    public static void OpenAndPlay()
    {
        if (!File.Exists(ScenePath)) Create();
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }

    static Material Surface(string name, Color color, float smoothness = 0.18f)
    {
        var mat = new Material(Shader.Find("Standard")) { name = name, color = color };
        mat.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(mat, Materials + name + ".mat");
        return mat;
    }

    static GameObject Node(string name, Transform parent, Vector3 localPosition)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        return obj;
    }

    static Mesh SpikeMesh()
    {
        var mesh = new Mesh { name = "Forged spike" };
        var a = new Vector3(-.15f, 0, -.15f);
        var b = new Vector3(.15f, 0, -.15f);
        var c = new Vector3(.15f, 0, .15f);
        var d = new Vector3(-.15f, 0, .15f);
        var tip = new Vector3(0, .92f, 0);
        mesh.vertices = new[] { a,tip,b, b,tip,c, c,tip,d, d,tip,a };
        mesh.triangles = new[] { 0,1,2, 3,4,5, 6,7,8, 9,10,11 };
        mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, "Assets/Art/Spike.asset");
        return mesh;
    }

    static void Create()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory(Materials);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var room = Node("Buried chamber", null, Vector3.zero).transform;
        var bronze = Surface("Bronze trim", new Color(.67f,.47f,.22f), .42f);
        var steel = Surface("Spike steel", new Color(.89f,.21f,.075f), .67f);
        steel.SetFloat("_Metallic", .28f);
        var floor = AssetDatabase.LoadAssetAtPath<GameObject>(Coast + "floor.prefab");
        var border = new Material(Shader.Find("Stone Chamber/Hazard Border"));
        AssetDatabase.CreateAsset(border, Materials + "Trap warning.mat");
        var spikeMesh = SpikeMesh();
        var game = room.gameObject.AddComponent<ChamberGame>();
        game.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/CountdownText/Font/AllIn1Text_Font_LilitaOne.ttf");
        game.arrowheadMesh = spikeMesh;
        game.trapMetal = steel;
        game.tiles = new SpikeTile[9];
        for (int i = 0; i < 9; i++)
        {
            var cell = Node("Tile " + (i + 1), room, new Vector3(i%3-1, 0, i/3-1)*RisingBoard.Spacing).transform;
            var tile = cell.gameObject.AddComponent<SpikeTile>();
            game.tiles[i] = tile;
            PrefabUtility.InstantiatePrefab(floor,cell);
            RisingBoard.DressEdge(cell,new Vector2Int(i%3-1,i/3-1),3);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Countdown inlay";
            quad.transform.SetParent(cell, false);
            quad.transform.localPosition = Vector3.up * .035f;
            quad.transform.localRotation = Quaternion.Euler(90,0,0);
            quad.transform.localScale = new Vector3(1.98f,1.98f,1);
            tile.warning = quad.GetComponent<Renderer>();
            tile.warning.sharedMaterial = border;
            tile.warning.shadowCastingMode = ShadowCastingMode.Off;
            tile.warning.enabled = false;
            tile.stakeMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Coast/Wooden Stake.asset");
            tile.stakeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Coast/Wooden Stake.mat");
            var dust = Node("Stone chips", cell, Vector3.up*.12f);
            dust.transform.localRotation = Quaternion.Euler(-90,0,0);
            tile.dust = dust.AddComponent<ParticleSystem>();
            var main = tile.dust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f,.32f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f,.10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f,2.8f);
            main.gravityModifier = .9f;
            var emission = tile.dust.emission; emission.rateOverTime = 0;
            var shape = tile.dust.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = .52f; shape.angle = 32;
            var dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            dustRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            dustRenderer.mesh = spikeMesh;
            dustRenderer.sharedMaterial = bronze;
        }
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Coast + "Coastal Sea.prefab"),room);
        game.explorer = Node("Explorer", room, Vector3.zero).transform;
        PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("ChamberAnimals/Bear"), game.explorer);
        game.explorer.rotation = Quaternion.Euler(0,180,0);
        var cameraObj = Node("Main Camera", null, new Vector3(0,11.8f,-5.2f));
        cameraObj.tag = "MainCamera";
        cameraObj.transform.LookAt(new Vector3(0,.15f,0));
        var camera = cameraObj.AddComponent<Camera>(); game.chamberCamera = camera;
        camera.fieldOfView = 38;
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Coast + "Pastel Coast Skybox.mat");
        camera.clearFlags = CameraClearFlags.Skybox;
        var sun = Node("Warm shaft of light", null, Vector3.up*8).AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50,-33,0);
        sun.intensity = .72f; sun.color = new Color(.84f,.91f,1); sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.32f,.41f,.48f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        const string results = "Assets/BeachResults/BeachResults.unity";
        buildScenes.RemoveAll(scene => scene.path == ScenePath || scene.path == results);
        buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        buildScenes.Add(new EditorBuildSettingsScene(results, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
    }
}
