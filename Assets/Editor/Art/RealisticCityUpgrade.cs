using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

/// <summary>Rendering and scanned prop upgrade. Existing transforms, physics and navigation remain authoritative.</summary>
public static class RealisticCityUpgrade
{
    public const string Root="Assets/Art/Realism";
    private const string Vendor=Root+"/PolyHaven";
    private static readonly System.Random random=new System.Random(74173);
    private static float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    [MenuItem("Tools/Dead city/Apply realistic city graphics")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        if(district.Find("Scanned realism details")!=null)throw new Exception("Already installed; refine existing materials instead.");
        var layout=district.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale));
        var physics=district.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var nav=district.GetComponent<NavMeshSurface>().navMeshData;
        PrepareImports();
        var root=new GameObject("Scanned realism details").transform;root.SetParent(district,false);
        Materials(district);Lighting(district);
        var fern=Foliage("fern_02");var shrub=Foliage("shrub_02");var car=CoveredCarMaterial();
        ReplaceVehicles(district,car);
        // Existing scanned barrels already have the correct collider/root. Hide only obsolete primitive skins.
        foreach(var t in district.GetComponentsInChildren<Transform>(true))if(t.name=="BarrelVisual")foreach(var r in t.GetComponentsInChildren<Renderer>())r.enabled=false;
        PlantEdges(root,fern,shrub);
        foreach(var entry in layout)if(entry.Key==null||Matrix4x4.TRS(entry.Key.localPosition,entry.Key.localRotation,entry.Key.localScale)!=entry.Value)throw new Exception("Existing transform changed: "+entry.Key);
        foreach(var entry in physics)if(entry.Key==null||EditorJsonUtility.ToJson(entry.Key)!=entry.Value)throw new Exception("Existing collider changed.");
        if(district.GetComponentsInChildren<Collider>(true).Length!=physics.Count||district.GetComponent<NavMeshSurface>().navMeshData!=nav)throw new Exception("Physics/navigation changed.");
        var camera=Camera.main;camera.transform.SetPositionAndRotation(new Vector3(.45f,2.3f,-23),Quaternion.LookRotation(new Vector3(0,2,12)-new Vector3(.45f,2.3f,-23)));camera.fieldOfView=58;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/RealismLayout.txt","Preserved "+layout.Count+" existing transforms and "+physics.Count+" collider definitions; unchanged NavMesh data.\nNew scanned foliage renderers="+root.GetComponentsInChildren<Renderer>().Length+"\nNo gameplay scripts, character rig, sockets or movement settings changed.\n");
        Capture();
    }
    private static void PrepareImports()
    {
        Phase2AnimationAssets.Folder(Root+"/Materials");Phase2AnimationAssets.Folder(Root+"/Reflections");
        AssetDatabase.StartAssetEditing();
        try{
            foreach(string guid in AssetDatabase.FindAssets("t:Texture",new[]{Vendor}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                bool normal=path.Contains("nor_gl"),colour=path.Contains("_diff_"),hdr=path.EndsWith(".hdr");
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=colour;importer.mipmapEnabled=true;importer.anisoLevel=8;
                importer.maxTextureSize=path.Contains("_2k")?2048:1024;
                importer.textureCompression=hdr?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
                importer.alphaSource=TextureImporterAlphaSource.None;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
            }
        }finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.Refresh();
    }
    private static Texture2D Map(string id,string channel,string res="1k",string extension="jpg")=>AssetDatabase.LoadAssetAtPath<Texture2D>(Vendor+"/"+id+"/textures/"+id+"_"+channel+"_"+res+"."+extension);
    private static Material NewMaterial(string name,string shader)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(shader)){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,path);}return m;
    }
    private static Material Foliage(string id)
    {
        var m=NewMaterial(id,"Outbreak/Scanned Foliage");m.SetTexture("_MainTex",Map(id,"diff","2k"));m.SetTexture("_AlphaTex",Map(id,"alpha","2k","png"));m.SetTexture("_BumpMap",Map(id,"nor_gl"));m.SetTexture("_RoughnessMap",Map(id,"rough"));m.SetTexture("_OcclusionMap",Map(id,"ao"));
        m.SetColor("_Color",new Color(.89f,.91f,.85f));m.SetFloat("_Cutoff",.43f);m.SetFloat("_WindStrength",id=="fern_02"?.008f:.013f);m.SetFloat("_Translucency",.10f);EditorUtility.SetDirty(m);return m;
    }
    private static Material CoveredCarMaterial()
    {
        var m=NewMaterial("Scanned covered car","Outbreak/Survivor Surface");m.SetTexture("_MainTex",Map("covered_car","diff","2k"));m.SetTexture("_BumpMap",Map("covered_car","nor_gl"));m.SetTexture("_Roughness",Map("covered_car","rough"));m.SetTexture("_OcclusionMap",Map("covered_car","ao"));m.SetColor("_Color",Color.white);m.SetFloat("_Dirt",.12f);m.SetFloat("_BumpScale",1);m.SetFloat("_RoughnessGamma",1);m.SetFloat("_RoughnessAmount",1);EditorUtility.SetDirty(m);return m;
    }
    private static void Materials(Transform district)
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));string n=m.name.ToLowerInvariant();
            if(n.Contains("asphalt")||n.Contains("rainwater")){m.SetColor("_Color",new Color(.72f,.73f,.72f));m.SetFloat("_Moss",.28f);m.SetFloat("_Wetness",n.Contains("rainwater")?.55f:.22f);m.SetFloat("_NormalStrength",n.Contains("rainwater")?.25f:1f);}
            if(n.Contains("cracked_concrete")||n.Contains("brick_wall")){m.SetColor("_Color",new Color(.88f,.87f,.83f));m.SetFloat("_Moss",.3f);m.SetFloat("_NormalStrength",1.1f);m.SetFloat("_Wetness",.08f);}
            if(n.Contains("roadpaint")){m.shader=Shader.Find("Outbreak/Worn Road Paint");m.SetColor("_Color",n.Contains("faded")?new Color(.58f,.56f,.48f):new Color(.65f,.56f,.31f));m.SetFloat("_Cutoff",.40f);m.SetFloat("_SurfaceY",.033f);m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(CityArtAssets.Root+"/PolyHaven/asphalt_02/nor_gl.jpg"));}
            if(n.Contains("glass")){m.shader=Shader.Find("Standard");m.color=new Color(.13f,.17f,.19f);m.SetFloat("_Glossiness",.65f);m.SetFloat("_Metallic",.35f);}
            if(n.StartsWith("survivor_")){m.SetFloat("_Dirt",n.Contains("head")||n.Contains("hands")?.09f:.18f);m.SetFloat("_BumpScale",.85f);m.SetFloat("_RoughnessGamma",1/2.2f);}
            if(n.Contains("infected")){m.SetFloat("_Dirt",.27f);m.SetFloat("_BumpScale",.8f);}
            EditorUtility.SetDirty(m);
        }
        foreach(var r in district.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Outbreak/Worn Road Paint")r.shadowCastingMode=ShadowCastingMode.Off;
    }
    private static void Lighting(Transform district)
    {
        PlayerSettings.colorSpace=ColorSpace.Linear;QualitySettings.antiAliasing=4;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
        QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowCascades=4;QualitySettings.shadowDistance=85;
        var sky=NewMaterial("Photographic overcast sky","Skybox/Panoramic");sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Vendor+"/kloofendal_overcast_puresky/kloofendal_overcast_puresky_2k.hdr"));sky.SetFloat("_Exposure",.75f);sky.SetFloat("_Rotation",120);EditorUtility.SetDirty(sky);
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Skybox;RenderSettings.ambientIntensity=.85f;RenderSettings.reflectionIntensity=.75f;RenderSettings.fog=false;
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional){l.transform.rotation=Quaternion.Euler(44,-38,0);l.color=new Color(.98f,.98f,1);l.intensity=.8f;l.shadows=LightShadows.Soft;l.shadowStrength=.58f;l.shadowBias=.025f;l.shadowNormalBias=.14f;}
        var camera=Camera.main;camera.allowHDR=true;camera.allowMSAA=true;
        var old=camera.GetComponent<CityCameraAtmosphere>();if(old!=null)old.enabled=false;
        var effect=camera.GetComponent<CinematicCityCamera>()??camera.gameObject.AddComponent<CinematicCityCamera>();effect.Configure(Shader.Find("Hidden/Outbreak/Cinematic City"));
        OutbreakEditor.Values(effect,s=>{s.FindProperty("exposureEV").floatValue=.05f;s.FindProperty("hazeDensity").floatValue=.007f;s.FindProperty("maximumHaze").floatValue=.38f;});
        var ao=camera.GetComponent<CityContactShadows>();if(ao!=null)OutbreakEditor.Values(ao,s=>{s.FindProperty("strength").floatValue=.65f;s.FindProperty("radius").floatValue=.45f;});
        DynamicGI.UpdateEnvironment();
    }
    private static void ReplaceVehicles(Transform district,Material material)
    {
        var targets=district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Abandoned vehicle"||t.name=="WreckedSedan").ToArray();
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"/covered_car/covered_car_1k.fbx");
        foreach(var target in targets)
        {
            foreach(var lod in target.GetComponentsInChildren<LODGroup>())lod.enabled=false;
            foreach(var r in target.GetComponentsInChildren<Renderer>())r.enabled=false;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,target);go.name="Scanned vehicle";
            go.transform.localPosition=Vector3.zero;go.transform.localRotation=asset.transform.localRotation;
            var rs=go.GetComponentsInChildren<Renderer>();var initial=LocalBounds(target,rs);
            if(initial.size.x>initial.size.z)go.transform.localRotation=Quaternion.Euler(0,90,0)*go.transform.localRotation;
            // Fit the retained collider footprint using parent-space bounds, independent of the parking yaw.
            var local=LocalBounds(target,rs);float factor=Mathf.Min(1.9f/local.size.x,4.3f/local.size.z);go.transform.localScale*=factor;
            local=LocalBounds(target,rs);go.transform.localPosition+=new Vector3(-local.center.x,.03f-local.min.y,-local.center.z);
            foreach(var r in rs){r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;r.lightProbeUsage=LightProbeUsage.BlendProbes;}
            var group=go.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.025f,rs)});group.RecalculateBounds();
        }
    }
    private static Bounds BoundsOf(Renderer[] rs){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    private static Bounds LocalBounds(Transform parent,Renderer[] rs)
    {
        var b=new Bounds();bool first=true;
        foreach(var r in rs){var f=r.GetComponent<MeshFilter>();if(f==null)continue;var m=f.sharedMesh.bounds;foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f}){var p=parent.InverseTransformPoint(r.transform.TransformPoint(m.center+Vector3.Scale(m.extents,new Vector3(x,y,z))));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}
        return b;
    }
    private static void PlantEdges(Transform root,Material fern,Material shrub)
    {
        Physics.SyncTransforms();
        var fernAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"/fern_02/fern_02_1k.fbx");var shrubAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"/shrub_02/shrub_02_1k.fbx");
        var ferns=fernAsset.GetComponentsInChildren<MeshFilter>().OrderBy(f=>f.sharedMesh.vertexCount).ToArray();var shrubs=shrubAsset.GetComponentsInChildren<MeshFilter>().OrderBy(f=>f.sharedMesh.vertexCount).ToArray();
        for(int i=0;i<126;i++)
        {
            float side=i%2==0?-1:1;Vector3 p=new Vector3(side*Range(5.55f,7.6f),3,Range(-34,34));
            if(!Physics.Raycast(p,Vector3.down,out var hit,4,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.6f)continue;
            var source=ferns[i%ferns.Length];Plant(root,source,fern,hit.point,Range(.3f,.58f),Range(0,360),.014f);
        }
        for(int i=0;i<22;i++)
        {
            float side=i%2==0?-1:1;Vector3 p=new Vector3(side*Range(7.1f,10.4f),4,Range(-34,33));
            if(!Physics.Raycast(p,Vector3.down,out var hit,5,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.6f)continue;
            Plant(root,shrubs[i%2],shrub,hit.point,Range(.65f,1.15f),Range(0,360),.025f);
        }
    }
    private static void Plant(Transform root,MeshFilter source,Material material,Vector3 position,float height,float yaw,float cull)
    {
        var go=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
        // Preserve the source FBX's axis conversion, while discarding its showroom spacing.
        go.transform.rotation=Quaternion.Euler(0,yaw,0)*source.transform.rotation;go.transform.localScale=source.transform.lossyScale;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;var b=renderer.bounds;go.transform.localScale*=height/Mathf.Max(.001f,b.size.y);b=renderer.bounds;go.transform.position=position-new Vector3(b.center.x,b.min.y,b.center.z);
        renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(cull,new Renderer[]{renderer})});lod.RecalculateBounds();
    }
    public static void BakeReflections()
    {
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;int index=0;
        foreach(var probe in district.GetComponentsInChildren<ReflectionProbe>())
        {
            probe.mode=ReflectionProbeMode.Baked;probe.resolution=256;probe.hdr=true;probe.boxProjection=true;probe.intensity=.8f;probe.cullingMask=~(1<<9);probe.clearFlags=ReflectionProbeClearFlags.Skybox;
            string path=Root+"/Reflections/City_"+(index++)+".exr";if(!Lightmapping.BakeReflectionProbe(probe,path))throw new Exception("Reflection capture failed.");
            AssetDatabase.ImportAsset(path);probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);EditorUtility.SetDirty(probe);
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();Capture();
    }
    [MenuItem("Tools/Dead city/Refine realistic city lighting")]
    public static void Refine()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        // Scanned plants replace the triangular stand-in grass. Wetness stays on the
        // asphalt itself rather than on the old disconnected opaque puddle polygons.
        foreach(var r in district.GetComponentsInChildren<Renderer>(true))
            if(r.name.StartsWith("Curb growth ")||r.name=="Batched StandingRainwater")r.enabled=false;
        RenderSettings.ambientIntensity=.45f;RenderSettings.reflectionIntensity=.65f;
        RenderSettings.skybox.SetFloat("_Exposure",.42f);EditorUtility.SetDirty(RenderSettings.skybox);
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional)
        {l.intensity=1.1f;l.shadowStrength=.82f;l.color=new Color(1f,.96f,.90f);}
        OutbreakEditor.Values(Camera.main.GetComponent<CinematicCityCamera>(),s=>{
            s.FindProperty("exposureEV").floatValue=-.15f;
            s.FindProperty("hazeDensity").floatValue=.0028f;
            s.FindProperty("maximumHaze").floatValue=.18f;
            s.FindProperty("contrast").floatValue=1.05f;
            s.FindProperty("saturation").floatValue=.92f;
        });
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(m.shader.name=="Outbreak/Worn Road Paint"){m.SetFloat("_Cutoff",.29f);EditorUtility.SetDirty(m);}
            if(m.name.ToLowerInvariant().Contains("glass"))
            {
                m.shader=Shader.Find("Outbreak/Dirty City Glass");
                m.SetColor("_Color",new Color(.045f,.055f,.055f));
                m.SetFloat("_DustAmount",.38f);m.SetFloat("_Smoothness",.82f);
                m.SetFloat("_DustSmoothness",.24f);m.SetFloat("_StreakStrength",.45f);
                EditorUtility.SetDirty(m);
            }
        }
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
    }
    public static void Capture()
    {
        Directory.CreateDirectory("Previews/ReclaimedCity");
        ReclaimedCityPresentation.Capture("RealismAfter",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        ReclaimedCityPresentation.Capture("RealismStreet",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
    }
}
