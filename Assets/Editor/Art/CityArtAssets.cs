using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the documented CC0 surface maps and keeps material assets reusable.</summary>
public static class CityArtAssets
{
    public const string Root = "Assets/Art/AbandonedCity";
    public static void Prepare()
    {
        Phase2AnimationAssets.Folder(Root + "/Materials");
        Phase2AnimationAssets.Folder(Root + "/Meshes");
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/PolyHaven" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool normal = path.Contains("nor_gl"), rough = path.Contains("Rough");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !rough;
            importer.maxTextureSize = normal || rough ? 1024 : 2048;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }

    public static Material Surface(string id, float tile = .5f, float metallic = 0, float wetness = .1f, bool projected = true)
    {
        string path = Root + "/Materials/" + id + (projected ? "_World" : "_UV") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Outbreak/Weathered Surface");
        if (shader == null) throw new InvalidOperationException("Weathered Surface shader has not imported.");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetTexture("_MainTex", Texture(id, "Diffuse"));
        material.SetTexture("_BumpMap", Texture(id, "nor_gl"));
        material.SetTexture("_Roughness", Texture(id, "Rough"));
        material.SetFloat("_Scale", tile);
        material.SetFloat("_Triplanar", projected ? 1 : 0);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Wetness", wetness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D Texture(string id, string channel)
    {
        foreach (string extension in new[] { ".jpg", ".png" })
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/PolyHaven/" + id + "/" + channel + extension);
            if (texture != null) return texture;
        }
        throw new InvalidOperationException("Missing downloaded surface map: " + id + "/" + channel);
    }

    public static Material Solid(string name, Color color, float smoothness = .25f, float metal = 0, Color emission = default)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
        material.color = color;
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metal);
        if (emission.maxColorComponent > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission); }
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static GameObject Model(string id, Transform parent, Vector3 position, float height, float yaw = 0)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/PolyHaven/" + id + "/" + id + ".fbx");
        if (asset == null) throw new InvalidOperationException("Missing model: " + id);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.name = id;
        // These source FBXs contain alternative versions side by side. Use one complete version.
        foreach(var child in go.GetComponentsInChildren<Renderer>())
            if(child.name.Contains("_aged") || child.name.Contains("_rust"))child.gameObject.SetActive(false);
        go.transform.position = position;
        // FBX roots often carry the Blender Z-up to Unity Y-up conversion.
        go.transform.rotation = Quaternion.Euler(0, yaw, 0) * asset.transform.localRotation;
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.localScale *= height / Mathf.Max(.001f, bounds.size.y);
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        go.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
        var material = Surface(id, 1, id == "trashbag" ? 0 : .45f, .05f, false);
        foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
        return go;
    }

}
