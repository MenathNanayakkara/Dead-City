using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Renders authored enemy motion in Edit Mode without running the game.</summary>
public static class InfectedMotionPreview
{
    private const int Width = 1400;
    private const int Height = 750;
    private const int FrameCount = 24;
    private const int PreviewLayer = 31;
    private const string OutputDirectory = "Previews/InfectedMotion";

    private sealed class Subject
    {
        public string Name;
        public Animator Animator;
        public SkinnedMeshRenderer[] Skins;
        public Mesh[] Baked;
    }

    [MenuItem("Zombie Outbreak/Art/Capture infected motion preview")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before capturing the authored motion preview.");

        Directory.CreateDirectory(OutputDirectory);
        var existingLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var lightFlags = existingLights.Select(light => light.enabled).ToArray();
        bool oldFog = RenderSettings.fog;
        AmbientMode oldAmbientMode = RenderSettings.ambientMode;
        Color oldAmbient = RenderSettings.ambientLight;
        float oldReflectionIntensity = RenderSettings.reflectionIntensity;
        RenderTexture oldActive = RenderTexture.active;
        var objects = new List<GameObject>();
        var meshes = new List<Mesh>();
        var vertices = new List<Vector3>();
        var bounds = new StringBuilder("Authored pose geometry reference; not a gameplay test.\n" +
            "Lowest baked skin vertex relative to the studio floor (metres). No contact assertions.\n" +
            "Mode,Frame,Enemy,LowestSkinYAboveFloor\n");
        Material floorMaterial = null;
        RenderTexture target = null;
        Texture2D pixels = null;
        Camera camera = null;
        try
        {
            foreach (var light in existingLights)
                light.enabled = false;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.40f, .42f, .44f);
            RenderSettings.reflectionIntensity = 0;

            // The dedicated layer keeps the gameplay map out of this studio render.
            var stage = new GameObject("Temporary infected motion studio");
            objects.Add(stage);
            stage.hideFlags = HideFlags.HideAndDontSave;
            stage.transform.position = new Vector3(10000, 0, 10000);
            AddLight(stage.transform, "Studio key", new Vector3(38, 150, 0), 1.1f, true);
            AddLight(stage.transform, "Studio fill", new Vector3(28, -65, 0), .42f, false);

            floorMaterial = new Material(Shader.Find("Standard"));
            floorMaterial.hideFlags = HideFlags.HideAndDontSave;
            floorMaterial.color = new Color(.16f, .18f, .19f);
            floorMaterial.SetFloat("_Glossiness", .08f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Studio floor";
            floor.transform.SetParent(stage.transform, false);
            floor.transform.localPosition = new Vector3(0, -.04f, 0);
            floor.transform.localScale = new Vector3(12, .08f, 8);
            floor.layer = PreviewLayer;
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            camera = new GameObject("Studio camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 2.05f, 9.5f);
            camera.transform.LookAt(stage.transform.position + new Vector3(0, 1.0f, 0));
            camera.enabled = false;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.105f, .12f, .13f);
            camera.orthographic = true;
            camera.orthographicSize = 1.9f;
            camera.aspect = Width / (float)Height;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            string[] kinds = { "Hunter", "Runner", "Ambusher", "Elite" };
            var subjects = new Subject[kinds.Length];
            for (int index = 0; index < kinds.Length; index++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + kinds[index] + ".prefab");
                if (prefab == null)
                    throw new InvalidOperationException("Missing enemy prefab: " + kinds[index]);
                var instance = Object.Instantiate(prefab, stage.transform);
                instance.name = kinds[index] + " preview";
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;
                foreach (var agent in instance.GetComponentsInChildren<NavMeshAgent>(true))
                    agent.enabled = false;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                    node.gameObject.layer = PreviewLayer;

                // Camera faces -Z: decreasing world X reads left-to-right in the image.
                instance.transform.localPosition = new Vector3((1.5f - index) * 1.65f, 0, 0);
                instance.transform.localRotation = Quaternion.Euler(0, -28, 0);
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Enemy needs a configured Animator: " + kinds[index]);
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.SetBool("Dead", false);
                animator.SetFloat("GaitScale", 1);
                var subject = new Subject { Name = kinds[index], Animator = animator, Skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true) };
                subject.Baked = new Mesh[subject.Skins.Length];
                for (int skinIndex = 0; skinIndex < subject.Skins.Length; skinIndex++)
                {
                    var skin = subject.Skins[skinIndex];
                    var mesh = new Mesh { name = kinds[index] + " baked preview", hideFlags = HideFlags.HideAndDontSave };
                    meshes.Add(mesh);
                    subject.Baked[skinIndex] = mesh;
                    var rendered = new GameObject("Baked preview skin", typeof(MeshFilter), typeof(MeshRenderer));
                    rendered.transform.SetParent(skin.transform, false);
                    rendered.layer = PreviewLayer;
                    rendered.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = rendered.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    skin.enabled = false;
                }
                subjects[index] = subject;
            }

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            foreach (string mode in new[] { "Walk", "Chase" })
            {
                int state = Animator.StringToHash("Base Layer." + (mode == "Walk" ? "Locomotion" : "Chase"));
                foreach (var subject in subjects)
                {
                    if (!subject.Animator.HasState(0, state))
                        throw new InvalidOperationException("Missing preview state: " + mode);
                    subject.Animator.SetFloat("Speed", mode == "Walk" ? 1 : 5);
                    subject.Animator.SetFloat("GaitScale", 1);
                }
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    foreach (var subject in subjects)
                    {
                        subject.Animator.Play(state, 0, frame / (float)FrameCount);
                        subject.Animator.Update(0);
                        float lowest = float.PositiveInfinity;
                        for (int skinIndex = 0; skinIndex < subject.Skins.Length; skinIndex++)
                        {
                            var skin = subject.Skins[skinIndex];
                            var mesh = subject.Baked[skinIndex];
                            skin.BakeMesh(mesh);
                            mesh.RecalculateBounds();
                            mesh.GetVertices(vertices);
                            foreach (var vertex in vertices)
                                lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y - stage.transform.position.y);
                        }
                        bounds.Append(mode).Append(',').Append(frame.ToString("00")).Append(',').Append(subject.Name).Append(',')
                            .AppendLine(float.IsPositiveInfinity(lowest) ? "no skin vertices" : lowest.ToString("F6", CultureInfo.InvariantCulture));
                    }
                    // Warm newly uploaded baked meshes and shadows before recording each cycle.
                    if (frame == 0)
                        camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(OutputDirectory + "/" + mode + "_" + frame.ToString("00") + ".png", pixels.EncodeToPNG());
                }
            }
            File.WriteAllText(OutputDirectory + "/README.txt",
                "Edit Mode animation preview; no gameplay simulation.\n" +
                "Left to right: Hunter, Runner, Ambusher, Elite.\n" +
                "Walk_00..23 and Chase_00..23 each sample one normalized animation cycle.\n" +
                "These sheets show posture and motion; they do not demonstrate runtime travel speed or NavMesh foot contact.\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/InfectedPreviewBounds.txt", bounds.ToString());
        }
        finally
        {
            if (camera != null)
                camera.targetTexture = null;
            RenderTexture.active = oldActive;
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            if (pixels != null)
                Object.DestroyImmediate(pixels);
            foreach (var instance in objects)
                if (instance != null)
                    Object.DestroyImmediate(instance);
            foreach (var mesh in meshes)
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
            if (floorMaterial != null)
                Object.DestroyImmediate(floorMaterial);
            for (int index = 0; index < existingLights.Length; index++)
                if (existingLights[index] != null)
                    existingLights[index].enabled = lightFlags[index];
            RenderSettings.fog = oldFog;
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientLight = oldAmbient;
            RenderSettings.reflectionIntensity = oldReflectionIntensity;
        }
    }

    private static void AddLight(Transform parent, string name, Vector3 rotation, float intensity, bool shadows)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.rotation = Quaternion.Euler(rotation);
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = Color.white;
        light.cullingMask = 1 << PreviewLayer;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = .65f;
        light.shadowBias = .025f;
        light.shadowNormalBias = .08f;
    }
}
