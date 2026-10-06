using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports our Blender skeletons verbatim and replaces only enemy visual children.</summary>
public static class CustomBlenderEnemySetup
{
    public const string Root = "Assets/Art/CustomEnemies";
    static readonly string[] Kinds = { "Hunter", "Runner", "Ambusher", "Elite" };
    static readonly string[] Modes = { "Idle", "Walk", "Chase", "TurnLeft", "TurnRight", "Attack", "Hit", "Stagger", "Death" };
    static readonly float[] WalkSpeeds = { 1.15f, 1.45f, .95f, 1.05f * 1.07f };
    static readonly float[] ChaseSpeeds = { 3.4f, 4.8f, 3.8f, 3.4f * 1.07f };
    static readonly float[] WalkSeconds = { .95f, .85f, 1.1f, 1.1f };
    static readonly float[] ChaseSeconds = { .70f, .56f, .68f, .78f };

    [MenuItem("Tools/Dead Zone/Import custom Blender enemies")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before importing enemy prefabs.");
        var report = new StringBuilder("Custom Blender enemy integration\n");
        string backup = "Backups/CustomEnemyNativeImport-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        foreach (string kind in Kinds.Concat(new[] { "EnemyBase" }))
        {
            string path = "Assets/Prefabs/Enemies/" + kind + ".prefab";
            File.Copy(path, backup + "/" + kind + ".prefab");
            File.Copy(path + ".meta", backup + "/" + kind + ".prefab.meta");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ImportTextures();
        var clips = new Dictionary<string, Dictionary<string, AnimationClip>>();
        var controllers = new Dictionary<string, AnimatorController>();
        // Finish every import/controller before touching any gameplay prefab.
        foreach (string kind in Kinds)
        {
            clips[kind] = ImportModel(kind, report);
            controllers[kind] = Controller(kind, clips[kind]);
        }
        // Base first: variants inherit its replacement before installing their own visual.
        Assign("EnemyBase", "Hunter", controllers["Hunter"], clips["Hunter"], report);
        foreach (string kind in Kinds) Assign(kind, kind, controllers[kind], clips[kind], report);
        AssetDatabase.SaveAssets();
        report.AppendLine("Backup: " + backup);
        report.AppendLine("Scene layout/player/AI unchanged. Turn and stagger takes imported but not bound to AI events.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CustomBlenderEnemyImport.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    static void ImportTextures()
    {
        foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png"))
        {
            var t = (TextureImporter)AssetImporter.GetAtPath(path);
            t.textureType = path.EndsWith("_Normal.png") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = path.EndsWith("_BaseColor.png");
            t.maxTextureSize = 1024;
            t.mipmapEnabled = true;
            t.textureCompression = TextureImporterCompression.Compressed;
            if (path.EndsWith("_MetallicSmoothness.png")) t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.SaveAndReimport();
        }
    }

    static Dictionary<string, AnimationClip> ImportModel(string kind, StringBuilder report)
    {
        string path = Root + "/Models/" + kind + "_Animated.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        // These clips were authored on this exact skeleton. Generic preserves their
        // joint transforms without Humanoid muscle conversion or runtime IK overlays.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.motionNodeName = ""; // static model root; the animated hips stay in the pose
        importer.importAnimation = true;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.optimizeGameObjects = false;
        importer.isReadable = true;
        importer.globalScale = 1;
        importer.SaveAndReimport();
        var takes = importer.defaultClipAnimations;
        foreach (var take in takes)
        {
            string mode = Modes.Concat(new[] { "HeavyAttack" }).FirstOrDefault(m => take.name.EndsWith(kind + "_" + m, StringComparison.Ordinal));
            if (mode == null) throw new InvalidOperationException("Unrecognized FBX take: " + take.name);
            take.name = kind + "_" + mode;
            take.loopTime = mode == "Idle" || mode == "Walk" || mode == "Chase";
            take.loopPose = false; // Blender already supplies identical end poses
            take.keepOriginalOrientation = true;
            take.keepOriginalPositionY = true;
            take.keepOriginalPositionXZ = true;
            take.lockRootRotation = true;
            take.lockRootHeightY = true;
            take.lockRootPositionXZ = true;
        }
        importer.clipAnimations = takes;
        importer.SaveAndReimport();
        var result = new Dictionary<string, AnimationClip>();
        foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
        {
            if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
            result.Add(clip.name.Substring(kind.Length + 1), clip);
            report.AppendLine(kind + "/" + clip.name + ": " + clip.length.ToString("F3") + " s, loop=" + clip.isLooping);
        }
        foreach (string mode in Modes) if (!result.ContainsKey(mode)) throw new Exception(kind + " missing " + mode);
        if (kind == "Elite" && !result.ContainsKey("HeavyAttack")) throw new Exception("Elite missing HeavyAttack");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var avatar = model.GetComponent<Animator>()?.avatar;
        if (avatar == null || !avatar.isValid) throw new Exception(kind + " invalid Generic avatar");
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials) Surface(material.name);
        return result;
    }

    static Material Surface(string name)
    {
        name = name.Replace(" (Instance)", "");
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = Color.white;
        material.enableInstancing = true;
        material.mainTexture = Texture(name, "BaseColor");
        material.SetTexture("_BumpMap", Texture(name, "Normal"));
        material.SetFloat("_BumpScale", .7f);
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", Texture(name, "MetallicSmoothness"));
        material.SetFloat("_GlossMapScale", 1);
        material.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(material);
        return material;
    }
    static Texture2D Texture(string name, string suffix)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + "_" + suffix + ".png");
        return t != null ? t : throw new Exception("Missing texture " + name + "_" + suffix);
    }

    static AnimatorController Controller(string kind, Dictionary<string, AnimationClip> clips)
    {
        string path = Root + "/Controllers/" + kind + ".controller";
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (c == null) c = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = c.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states) sm.RemoveState(s.state);
        foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>()) Object.DestroyImmediate(tree, true);
        c.parameters = Array.Empty<AnimatorControllerParameter>();
        foreach (string p in new[] { "Speed", "GaitScale", "AttackSpeed" })
            c.AddParameter(new AnimatorControllerParameter { name = p, type = AnimatorControllerParameterType.Float, defaultFloat = p == "Speed" ? 0 : 1 });
        foreach (string p in new[] { "Attack", "Heavy", "Hit" }) c.AddParameter(p, AnimatorControllerParameterType.Trigger);
        c.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        var slow = sm.AddState("Locomotion");
        var blend = new BlendTree { name = kind + " Idle Walk", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(blend, c);
        blend.AddChild(clips["Idle"], 0); blend.AddChild(clips["Walk"], .08f);
        slow.motion = blend; slow.speedParameter = "GaitScale"; slow.speedParameterActive = true;
        var run = sm.AddState("Chase"); run.motion = clips["Chase"]; run.speedParameter = "GaitScale"; run.speedParameterActive = true;
        sm.defaultState = slow;
        float threshold = kind == "Elite" ? 3.2f : kind == "Ambusher" ? 3.35f : 2.7f;
        Transition(slow, run, "Speed", AnimatorConditionMode.Greater, threshold);
        Transition(run, slow, "Speed", AnimatorConditionMode.Less, threshold - .25f);
        var attack = sm.AddState("Attack"); attack.motion = clips["Attack"];
        var heavy = sm.AddState("Heavy"); heavy.motion = kind == "Elite" ? clips["HeavyAttack"] : clips["Attack"];
        foreach (var s in new[] { attack, heavy })
        {
            s.speedParameter = "AttackSpeed"; s.speedParameterActive = true;
            var exit = s.AddTransition(slow); exit.hasExitTime = true; exit.exitTime = 1; exit.hasFixedDuration = true; exit.duration = .08f;
        }
        var hit = sm.AddState("Hit"); hit.motion = clips["Hit"];
        var recover = hit.AddTransition(slow); recover.hasExitTime = true; recover.exitTime = .9f; recover.hasFixedDuration = true; recover.duration = .1f;
        // Only locomotion can be interrupted by hit; combat controls attack timing.
        foreach (var s in new[] { slow, run }) Transition(s, hit, "Hit", AnimatorConditionMode.If, 0, .06f);
        var death = sm.AddState("Death"); death.motion = clips["Death"];
        var die = sm.AddAnyStateTransition(death); die.hasExitTime = false; die.hasFixedDuration = true; die.duration = .08f; die.canTransitionToSelf = false;
        die.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        foreach (var s in sm.states) s.state.iKOnFeet = false;
        EditorUtility.SetDirty(c);
        return c;
    }
    static void Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold, float duration = .16f)
    {
        var t = from.AddTransition(to); t.hasExitTime = false; t.hasFixedDuration = true; t.duration = duration;
        t.AddCondition(mode, threshold, parameter);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
    }

    static void Assign(string prefabName, string kind, AnimatorController controller, Dictionary<string, AnimationClip> clips, StringBuilder report)
    {
        string path = "Assets/Prefabs/Enemies/" + prefabName + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var driver = root.GetComponent<EnemyAnimationController>();
            if (driver == null) throw new Exception(path + " missing animation driver");
            foreach (var old in root.GetComponentsInChildren<Animator>(true))
            {
                if (old.gameObject == root) throw new Exception("Unexpected root Animator: " + path);
                Object.DestroyImmediate(old.gameObject);
            }
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + kind + "_Animated.fbx"));
            visual.name = "Custom " + kind + " Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // Keep the FBX's authored scale; Elite's larger skeleton is already baked.
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
            var animator = visual.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Surface(m.name)).ToArray();
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
            int index = Array.IndexOf(Kinds, kind);
            var values = new SerializedObject(driver);
            values.FindProperty("animator").objectReferenceValue = animator;
            values.FindProperty("attackClip").objectReferenceValue = clips["Attack"];
            values.FindProperty("heavyAttackClip").objectReferenceValue = kind == "Elite" ? clips["HeavyAttack"] : clips["Attack"];
            values.FindProperty("walkStrideSpeed").floatValue = WalkSpeeds[index] * WalkSeconds[index] / clips["Walk"].length;
            values.FindProperty("chaseStrideSpeed").floatValue = ChaseSpeeds[index] * ChaseSeconds[index] / clips["Chase"].length;
            values.FindProperty("visualStrideScale").floatValue = 1;
            values.FindProperty("randomizeLocomotionPhase").boolValue = true;
            values.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            report.AppendLine(prefabName + ": new visual/controller assigned; existing gameplay root retained.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
