using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Admits the approved sculpt textures without rebuilding AI or controllers.</summary>
public static class SculptedEnemyImport
{
    const string Root = CustomBlenderEnemySetup.Root;
    const string Textures = Root + "/Textures/Sculpt4";
    static readonly string[] Kinds = { "Hunter", "Runner", "Ambusher", "Elite" };

    [MenuItem("Tools/Dead city/Apply sculpted enemy revision 4")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before changing enemy assets.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new StringBuilder("Sculpt revision 4 Unity import\n");
        // Complete every model/material import before changing prefab overrides.
        foreach (string kind in Kinds)
        {
            foreach (string channel in new[] { "BaseColor", "Normal", "MetallicSmoothness" })
            {
                string path = TexturePath(kind, channel);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing " + path);
                importer.textureType = channel == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = channel == "BaseColor";
                importer.maxTextureSize = channel == "BaseColor" ? 4096 : 2048;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = channel == "MetallicSmoothness"
                    ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            Material material = MaterialFor(kind);
            string modelPath = Root + "/Models/" + kind + "_Animated.fbx";
            var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (modelImporter == null) throw new InvalidOperationException("Missing " + modelPath);
            // Existing .meta files retain Generic rig, clip IDs and import settings.
            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),
                kind + "_Sculpt4_PBR_Atlas"), material);
            modelImporter.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var animator = model.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid)
                throw new InvalidOperationException(kind + " has no valid imported avatar.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            int expected = kind == "Elite" ? 10 : 9;
            if (clips.Length != expected)
                throw new InvalidOperationException(kind + " expected " + expected + " clips; got " + clips.Length);
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null || skin.sharedMesh == null || skin.sharedMesh.uv.Length != skin.sharedMesh.vertexCount)
                throw new InvalidOperationException(kind + " missing skinned mesh or primary UVs.");
            report.AppendLine(kind + ": " + clips.Length + " clips, " + skin.bones.Length
                + " skin bones, " + skin.sharedMesh.vertexCount + " imported vertices, material=" + material.name);
        }
        AssignMaterial("EnemyBase", "Hunter", report);
        foreach (string kind in Kinds) AssignMaterial(kind, kind, report);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SculptedEnemyImport.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    public static Material MaterialFor(string kind)
    {
        if (!Kinds.Contains(kind)) throw new ArgumentException("Unknown enemy: " + kind);
        string name = kind + "_Sculpt4_PBR_Atlas";
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Project Standard shader missing.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = Color.white;
        material.mainTexture = Load(kind, "BaseColor");
        material.SetTexture("_BumpMap", Load(kind, "Normal"));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_MetallicGlossMap", Load(kind, "MetallicSmoothness"));
        material.SetFloat("_GlossMapScale", 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static string TexturePath(string kind, string channel) => Textures + "/" + kind + "_" + channel + ".png";

    static Texture2D Load(string kind, string channel)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(kind, channel));
        if (texture == null) throw new InvalidOperationException("Missing texture: " + TexturePath(kind, channel));
        return texture;
    }

    static void AssignMaterial(string prefabName, string kind, StringBuilder report)
    {
        string path = "Assets/Prefabs/Enemies/" + prefabName + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var driver = root.GetComponent<EnemyAnimationController>();
            var animator = root.GetComponentInChildren<Animator>(true);
            if (driver == null || animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException(path + " has incomplete existing animation setup.");
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length != 1) throw new InvalidOperationException(path + " unexpected renderer count: " + skins.Length);
            var skin = skins[0];
            if (skin.sharedMesh == null || skin.sharedMesh.subMeshCount != 1)
                throw new InvalidOperationException(path + " did not load the atlas model.");
            skin.sharedMaterials = new[] { MaterialFor(kind) };
            PrefabUtility.SaveAsPrefabAsset(root, path);
            report.AppendLine(prefabName + ": atlas material assigned; existing animator/controller/AI retained.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
