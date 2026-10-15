using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

/// <summary>A presentation pass on the existing level. Geometry, collision and navigation are locked.</summary>
public static class ReclaimedCityPresentation
{
    public const string Root="Assets/Art/ReclaimedCity";
    private static readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
    private static readonly System.Random random=new System.Random(17042);
    private static readonly StringBuilder report=new StringBuilder();
    private static float Rand(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)random.NextDouble());

    [MenuItem("Tools/Dead city/Apply reclaimed city presentation")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        if(district.Find("Reclaimed vegetation")!=null)throw new InvalidOperationException("Presentation is already installed. Edit its existing materials and vegetation groups.");
        foreach(string folder in new[]{Root,Root+"/Materials",Root+"/Meshes","Previews/ReclaimedCity"})Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();materials.Clear();report.Clear();
        var transforms=district.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale));
        var collisions=district.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var surface=district.GetComponent<NavMeshSurface>();var nav=surface.navMeshData;
        Capture("Before",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        foreach(var r in district.GetComponentsInChildren<MeshRenderer>(true))r.sharedMaterials=r.sharedMaterials.Select(EnvironmentMaterial).ToArray();
        Dress(district);
        Lighting();
        string[] prefabs={"Assets/Prefabs/Player/PlayerPhase3.prefab","Assets/Prefabs/Enemies/EnemyBase.prefab","Assets/Prefabs/Enemies/Hunter.prefab","Assets/Prefabs/Enemies/Runner.prefab","Assets/Prefabs/Enemies/Ambusher.prefab","Assets/Prefabs/Enemies/Elite.prefab"};
        foreach(string path in prefabs)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Character(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Character(Object.FindAnyObjectByType<ThirdPersonPlayerController>().gameObject);
        foreach(var actor in Object.FindObjectsByType<EnemyActor>())Character(actor.gameObject);
        foreach(var item in transforms)
            if(item.Key==null||Matrix4x4.TRS(item.Key.localPosition,item.Key.localRotation,item.Key.localScale)!=item.Value)
                throw new InvalidOperationException("Existing layout transform changed: "+(item.Key==null?"missing":item.Key.name));
        foreach(var item in collisions)
            if(item.Key==null||EditorJsonUtility.ToJson(item.Key)!=item.Value)throw new InvalidOperationException("Existing collision changed.");
        if(surface.navMeshData!=nav)throw new InvalidOperationException("Navigation data changed.");
        if(district.GetComponentsInChildren<Collider>(true).Length!=collisions.Count)throw new InvalidOperationException("Added decoration has collision.");
        report.AppendLine("PASS: "+transforms.Count+" existing environment transforms unchanged; "+collisions.Count+" collider definitions unchanged; navigation data unchanged.");
        ExistingCityEnhancer.ValidateNavigation();Phase18Setup.Validate();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        Capture("After",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        Capture("Street",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
        CharacterPreview();
        var renderers=district.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
        report.AppendLine("Enabled environment renderers: "+renderers.Length);
        report.AppendLine("Vegetation triangles: "+district.Find("Reclaimed vegetation").GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3));
        report.AppendLine("Retained current grounded controllers, all meshes/Avatars, weapon sockets, enemy stats and AI.");
        File.WriteAllText("Logs/ReclaimedCityPresentation.txt",report.ToString());
        Debug.Log("RECLAIMED_CITY_PASS\n"+report);
    }

    private static Material EnvironmentMaterial(Material source)
    {
        if(source==null)return null;
        if(materials.TryGetValue(source,out var found))return found;
        string shader=source.shader.name;
        if(shader!="Standard"&&shader!="Outbreak/Weathered Surface")return source;
        var copy=new Material(source){name=source.name+" Reclaimed",enableInstancing=true};
        if(shader=="Outbreak/Weathered Surface")
        {
            copy.shader=Shader.Find("Outbreak/Reclaimed Surface");
            string lower=source.name.ToLowerInvariant();
            bool metal=lower.Contains("iron")||lower.Contains("rust")||lower.Contains("bin")||lower.Contains("barrel");
            copy.SetFloat("_Moss",metal?.14f:lower.Contains("asphalt")?.5f:.65f);
            copy.SetFloat("_Wetness",lower.Contains("asphalt")?.34f:.12f);
            copy.SetFloat("_NormalStrength",.85f);
            copy.SetColor("_MossColor",new Color(.16f,.205f,.07f));
            copy.SetColor("_Color",Color.Lerp(source.GetColor("_Color"),Color.white,.16f));
        }
        else if(source.name.Contains("vehicle paint")||source.name.Contains("CarPaint"))
        {
            var rust=AssetDatabase.LoadAssetAtPath<Material>(CityArtAssets.Root+"/Materials/rusty_corrugated_iron_World.mat");
            if(rust!=null){copy.CopyPropertiesFromMaterial(rust);copy.shader=Shader.Find("Outbreak/Reclaimed Surface");copy.SetFloat("_Scale",2);copy.SetFloat("_Moss",.15f);copy.SetColor("_Color",new Color(.5f,.55f,.43f));}
        }
        else if(copy.HasProperty("_Glossiness"))copy.SetFloat("_Glossiness",Mathf.Min(.4f,copy.GetFloat("_Glossiness")));
        AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath(Root+"/Materials/"+Safe(source.name)+".mat"));
        materials[source]=copy;return copy;
    }
    private static string Safe(string name)=>string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));

    private static void Character(GameObject root)
    {
        bool player=root.GetComponent<ThirdPersonPlayerController>()!=null;
        string category=player?"Survivor":root.name.Replace("(Clone)","");
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.sharedMaterials=skin.sharedMaterials.Select(m=>CharacterMaterial(m,category,player)).ToArray();
        var animator=root.GetComponentInChildren<Animator>(true);
        if(animator==null||!animator.isHuman)return;
        if(animator.GetComponent<CharacterMotionPolish>()==null)animator.gameObject.AddComponent<CharacterMotionPolish>();
        OutbreakEditor.Values(animator.GetComponent<CharacterMotionPolish>(),s=>{
            s.FindProperty("amount").floatValue=player?.8f:category.Contains("Runner")?.65f:.45f;
            s.FindProperty("breathing").floatValue=player?.35f:.6f;
        });
        report.AppendLine(category+": preserved humanoid Avatar and "+animator.runtimeAnimatorController.name+", added torso inertia.");
    }

    private static Material CharacterMaterial(Material source,string category,bool player)
    {
        if(source==null||source.name.Contains("Eyes"))return source;
        if(source.shader.name=="Outbreak/Survivor Surface")return source;
        string name=category+"_"+Safe(source.name),path=Root+"/Materials/"+name+".mat";
        var result=AssetDatabase.LoadAssetAtPath<Material>(path);if(result!=null)return result;
        result=new Material(Shader.Find("Outbreak/Survivor Surface")){name=name};
        result.SetTexture("_MainTex",source.mainTexture);
        result.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));
        result.SetTexture("_OcclusionMap",source.GetTexture("_OcclusionMap"));
        result.SetFloat("_BumpScale",player?.85f:.65f);result.SetFloat("_Dirt",player?.38f:.5f);
        result.SetFloat("_Smoothness",.22f);result.SetFloat("_RoughnessAmount",0);
        Color tint=Color.white;
        if(player)
        {
            string part=source.name.Split('_')[0].ToLowerInvariant();if(part=="cap1")part="caps1";
            string texture=AssetDatabase.FindAssets("t:Texture2D",new[]{SurvivalistSetup.Root+"/Vendor/Texture"}).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p).ToLowerInvariant()=="t_military_"+part+"_rough");
            if(texture!=null){result.SetTexture("_Roughness",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));result.SetFloat("_RoughnessAmount",1);result.SetFloat("_RoughnessGamma",((TextureImporter)AssetImporter.GetAtPath(texture)).sRGBTexture?1/2.2f:1);}
            if(part.Contains("pants"))
            {
                result.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(SurvivalistSetup.Root+"/Vendor/Texture/Bottom/T_Military_Pants2_color_skin3.png"));
                tint=new Color(.76f,.79f,.7f);
            }
            if(part.Contains("shirt"))tint=new Color(.81f,.86f,.74f);
            if(part.Contains("head")||part.Contains("hands")){result.SetFloat("_Dirt",.14f);result.SetFloat("_BumpScale",.6f);}
        }
        else
        {
            bool runner=category.Contains("Runner"),elite=category.Contains("Elite"),ambusher=category.Contains("Ambusher");
            tint=runner?new Color(.93f,.83f,.77f):elite?new Color(.76f,.8f,.67f):ambusher?new Color(.76f,.8f,.79f):new Color(.89f,.88f,.8f);
            result.SetFloat("_Infection",runner?.22f:elite?.75f:ambusher?.6f:.4f);
            result.SetColor("_InfectionColor",elite?new Color(.36f,.28f,.13f):ambusher?new Color(.20f,.24f,.19f):new Color(.28f,.14f,.11f));
            result.SetFloat("_Smoothness",.16f);
        }
        result.SetColor("_Color",tint);AssetDatabase.CreateAsset(result,path);return result;
    }

    private static void Lighting()
    {
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.49f,.55f,.55f);RenderSettings.fogDensity=.0085f;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.47f,.53f,.59f);
        RenderSettings.ambientEquatorColor=new Color(.29f,.32f,.30f);
        RenderSettings.ambientGroundColor=new Color(.16f,.18f,.13f);
        var sky=new Material(RenderSettings.skybox){name="Broken cloud daylight"};
        sky.SetColor("_SkyColor",new Color(.77f,.82f,.86f));sky.SetColor("_Horizon",new Color(.64f,.69f,.7f));sky.SetColor("_CloudDark",new Color(.42f,.48f,.53f));
        AssetDatabase.CreateAsset(sky,Root+"/Materials/Daylight sky.mat");RenderSettings.skybox=sky;
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional)
        {l.transform.rotation=Quaternion.Euler(43,-38,0);l.color=new Color(1,.91f,.77f);l.intensity=1.15f;l.shadows=LightShadows.Soft;l.shadowStrength=.8f;l.shadowBias=.035f;l.shadowNormalBias=.22f;}
        var camera=Camera.main;
        var ao=camera.GetComponent<CityContactShadows>()??camera.gameObject.AddComponent<CityContactShadows>();ao.Configure(Shader.Find("Hidden/Outbreak/Contact Shadows"));
        var grade=camera.GetComponent<CityCameraAtmosphere>();
        if(grade!=null)OutbreakEditor.Values(grade,s=>{s.FindProperty("exposure").floatValue=1.03f;s.FindProperty("saturation").floatValue=.91f;s.FindProperty("contrast").floatValue=1.06f;s.FindProperty("vignette").floatValue=.08f;s.FindProperty("bloom").floatValue=.045f;});
        // Closer shoulder framing retains all targets and the existing collision solver.
        foreach(var mode in Object.FindObjectsByType<ThirdPersonCameraMode>())if(mode.name.Contains("Exploration"))
            OutbreakEditor.Values(mode,s=>{s.FindProperty("distance").floatValue=3.5f;s.FindProperty("shoulderOffset").vector2Value=new Vector2(.46f,.05f);s.FindProperty("fieldOfView").floatValue=58;});
    }

    internal sealed class Leaves
    {
        public readonly List<Vector3> v=new List<Vector3>();public readonly List<int> t=new List<int>();
        public readonly List<Color> colors=new List<Color>();public readonly List<Vector2> uv=new List<Vector2>();
        public void Leaf(Vector3 origin,Vector3 direction,Vector3 side,float length,float width,Color color)
        {
            direction.Normalize();side.Normalize();Vector3 normal=Vector3.Cross(side,direction).normalized;
            int start=v.Count;
            v.Add(origin);v.Add(origin+direction*length*.42f-side*width);v.Add(origin+direction*length*.48f+normal*width*.22f);
            v.Add(origin+direction*length*.42f+side*width);v.Add(origin+direction*length);
            uv.AddRange(new[]{new Vector2(.5f,0),new Vector2(0,.45f),new Vector2(.5f,.5f),new Vector2(1,.45f),new Vector2(.5f,1)});
            for(int i=0;i<5;i++){var c=color*(i==0?.58f:i==2?1.13f:1);c.a=i==0?0:i==4?1:.5f;colors.Add(c);}
            foreach(int index in new[]{0,1,2,0,2,3,1,4,2,2,4,3})t.Add(start+index);
        }
        public void Save(Transform parent,string name,Material material)
        {
            if(v.Count==0)return;
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
            mesh.bounds=new Bounds(mesh.bounds.center,mesh.bounds.size+Vector3.one*.1f);
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Root+"/Meshes/"+Safe(name)+".asset"));
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;
            var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.009f,new[]{go.GetComponent<Renderer>()})});lod.RecalculateBounds();
        }
    }
    private static Color Green()=>Color.Lerp(new Color(.12f,.19f,.045f),new Color(.34f,.38f,.105f),Rand(0,1));
    private static void Dress(Transform district)
    {
        var root=new GameObject("Reclaimed vegetation").transform;root.SetParent(district,false);
        var leaves=new Material(Shader.Find("Outbreak/Living Foliage")){name="Weathered green foliage",enableInstancing=true};AssetDatabase.CreateAsset(leaves,Root+"/Materials/Foliage.mat");
        Physics.SyncTransforms();
        // Edge growth is visual-only and does not alter path width, collision or tactical markers.
        for(int zone=0;zone<10;zone++)
        {
            var grass=new Leaves();float startZ=-35+zone*7;
            for(int patch=0;patch<20;patch++)
            {
                float x=(patch%2==0?-1:1)*Rand(4.8f,7.6f),z=startZ+Rand(0,7);
                if(!Physics.Raycast(new Vector3(x,3,z),Vector3.down,out var hit,3.4f,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.5f)continue;
                for(int blade=0;blade<24;blade++)
                {
                    Vector3 p=hit.point+new Vector3(Rand(-.38f,.38f),.005f,Rand(-.38f,.38f));
                    float angle=Rand(0,Mathf.PI*2);Vector3 side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    grass.Leaf(p,Vector3.up+Vector3.Cross(side,Vector3.up)*Rand(.05f,.65f),side,Rand(.13f,.52f),Rand(.008f,.025f),Green());
                }
                for(int stem=0;stem<5;stem++)
                {
                    Vector3 p=hit.point+new Vector3(Rand(-.25f,.25f),Rand(.05f,.17f),Rand(-.25f,.25f));
                    for(int leaf=0;leaf<5;leaf++){float a=leaf*1.256f;var d=new Vector3(Mathf.Cos(a),.45f,Mathf.Sin(a));grass.Leaf(p,d,Vector3.Cross(d,Vector3.up),Rand(.12f,.24f),.045f,Green());}
                }
            }
            grass.Save(root,"Curb growth "+zone,leaves);
        }
        int faceId=0;
        foreach(var face in district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Weathered facade").ToArray())
        {
            var ivy=new Leaves();
            float width=face.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Floor ledge").Select(t=>t.localScale.x).DefaultIfEmpty(12).Max();
            for(int vine=0;vine<9;vine++)
            {
                float centre=(vine%2==0?-1:1)*width*Rand(.25f,.46f);float height=Rand(5,12),baseY=Rand(.4f,2.8f);
                for(float y=baseY;y<height;y+=.17f)
                {
                    float x=centre+Mathf.Sin(y*.9f+vine)*.3f;
                    for(int side=-1;side<=1;side+=2)
                    {
                        Vector3 local=new Vector3(x+side*Rand(.03f,.25f),y,-Rand(.20f,.38f));
                        Vector3 dir=face.TransformDirection(new Vector3(side*Rand(.3f,.9f),Rand(.2f,.75f),-.15f));
                        Vector3 lateral=face.TransformDirection(new Vector3(-dir.y,side*.7f,0));
                        ivy.Leaf(face.TransformPoint(local),dir,lateral,Rand(.15f,.3f),Rand(.065f,.13f),Green());
                    }
                }
            }
            ivy.Save(root,"Facade ivy "+faceId++,leaves);
        }
        // Sparse crowns on the existing dead trees preserve their footprint and silhouette anchors.
        int treeId=0;
        foreach(var tree in district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Dead street tree").ToArray())
        {
            var crown=new Leaves();
            for(int i=0;i<420;i++)
            {
                float angle=Rand(0,Mathf.PI*2),distance=Rand(.1f,1.35f);
                var p=tree.position+new Vector3(Mathf.Cos(angle)*distance,Rand(2.8f,4.4f),Mathf.Sin(angle)*distance*.7f);
                var d=new Vector3(Rand(-1,1),Rand(-.15f,.7f),Rand(-1,1));
                crown.Leaf(p,d,Vector3.Cross(d,Vector3.up),Rand(.12f,.24f),Rand(.04f,.08f),Green());
            }
            crown.Save(root,"Sparse tree regrowth "+treeId++,leaves);
        }
    }

    public static void Capture(string name,Vector3 position,Vector3 lookAt,float fov)
    {
        var camera=Camera.main;var p=camera.transform.position;var q=camera.transform.rotation;float angle=camera.fieldOfView,aspect=camera.aspect;
        var player=Object.FindAnyObjectByType<ThirdPersonPlayerController>();var a=player.GetComponentInChildren<Animator>();
        var bones=a.GetComponentsInChildren<Transform>();var poses=bones.Select(t=>(t.localPosition,t.localRotation)).ToArray();
        var rt=new RenderTexture(1600,1000,24){antiAliasing=4};var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);var active=RenderTexture.active;
        try
        {
            a.Rebind();a.SetBool("IsGrounded",true);a.SetLayerWeight(a.GetLayerIndex("Weapon Carry"),1);a.Update(.01f);
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(lookAt-position));camera.fieldOfView=fov;camera.aspect=1.6f;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
            SavePPM(tex,"Previews/ReclaimedCity/"+name+".ppm");
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
            camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=angle;camera.aspect=aspect;
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=poses[i].Item1;bones[i].localRotation=poses[i].Item2;}
        }
    }
    private static void SavePPM(Texture2D tex,string path)
    {
        var pixels=tex.GetPixels32();using(var file=new BinaryWriter(File.Create(path)))
        {file.Write(Encoding.ASCII.GetBytes("P6\n"+tex.width+" "+tex.height+"\n255\n"));for(int y=tex.height-1;y>=0;y--)for(int x=0;x<tex.width;x++){var c=pixels[y*tex.width+x];file.Write(c.r);file.Write(c.g);file.Write(c.b);}}
    }
    private static void CharacterPreview()
    {
        var objects=new List<GameObject>();var camera=Camera.main;int mask=camera.cullingMask;bool fog=RenderSettings.fog;
        var meshes=new List<Mesh>();var clear=camera.clearFlags;var background=camera.backgroundColor;
        var floorMaterial=new Material(Shader.Find("Standard")){color=new Color(.18f,.20f,.19f)};
        string[] names={"Player/PlayerPhase3","Enemies/Hunter","Enemies/Runner","Enemies/Ambusher","Enemies/Elite"};
        try
        {
            RenderSettings.fog=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.19f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(floor);floor.layer=31;
            floor.transform.position=new Vector3(0,-.06f,-20);floor.transform.localScale=new Vector3(20,.1f,16);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            for(int i=0;i<names.Length;i++)
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+names[i]+".prefab"));objects.Add(go);
                foreach(var b in go.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                go.transform.SetPositionAndRotation(new Vector3((i-2)*1.45f,.08f,-20),Quaternion.Euler(0,165,0));
                var a=go.GetComponentInChildren<Animator>();a.Rebind();a.Update(.01f);
                if(i==0){a.SetBool("IsGrounded",true);a.SetLayerWeight(a.GetLayerIndex("Weapon Carry"),1);a.Update(.01f);AttachPreviewWeapon(go);}
                float low=float.PositiveInfinity;
                foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {var mesh=new Mesh();meshes.Add(mesh);skin.BakeMesh(mesh);foreach(var v in mesh.vertices)low=Mathf.Min(low,skin.transform.TransformPoint(v).y);var baked=new GameObject("Baked pose",typeof(MeshFilter),typeof(MeshRenderer));baked.transform.SetParent(skin.transform,false);baked.layer=31;baked.GetComponent<MeshFilter>().sharedMesh=mesh;baked.GetComponent<Renderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;}
                go.transform.position+=Vector3.up*(-low);
            }
            Capture("Characters",new Vector3(.2f,1.8f,-26.6f),new Vector3(0,1,-20),60);
        }
        finally{foreach(var go in objects)Object.DestroyImmediate(go);foreach(var mesh in meshes)Object.DestroyImmediate(mesh);Object.DestroyImmediate(floorMaterial);camera.cullingMask=mask;camera.clearFlags=clear;camera.backgroundColor=background;RenderSettings.fog=fog;}
    }

    private static GameObject AttachPreviewWeapon(GameObject player)
    {
        var a=player.GetComponentInChildren<Animator>();
        var socket=player.GetComponentsInChildren<Transform>(true).First(t=>t.name=="WeaponSocket");
        var gun=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Rifle.prefab"),socket);
        gun.transform.localPosition=Vector3.zero;gun.transform.localRotation=Quaternion.identity;
        foreach(var t in gun.GetComponentsInChildren<Transform>(true))t.gameObject.layer=player.layer;
        var view=gun.GetComponent<WeaponView>();view.AimAt(gun.transform.position+player.transform.forward*10,true);
        var upper=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);var lower=a.GetBoneTransform(HumanBodyBones.LeftLowerArm);var hand=a.GetBoneTransform(HumanBodyBones.LeftHand);
        var target=view.SupportHand.position;var start=upper.position;float l1=Vector3.Distance(start,lower.position),l2=Vector3.Distance(lower.position,hand.position);
        var direction=(target-start).normalized;float d=Mathf.Clamp(Vector3.Distance(start,target),.01f,l1+l2-.003f),along=(l1*l1+d*d-l2*l2)/(2*d);
        var bend=Vector3.ProjectOnPlane(-player.transform.right*.7f-player.transform.forward*.25f-Vector3.up*.5f,direction).normalized;
        var elbow=start+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));var wrist=hand.rotation;
        upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,start+direction*d-lower.position)*lower.rotation;hand.rotation=wrist;
        return gun;
    }

    public static void FinishPresentation()
    {
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(material.name.StartsWith("Survivor_Pants"))
            {material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(SurvivalistSetup.Root+"/Vendor/Texture/Bottom/T_Military_Pants2_color_skin3.png"));material.SetColor("_Color",new Color(.76f,.79f,.7f));EditorUtility.SetDirty(material);}
            if(ShaderUtil.ShaderHasError(material.shader))throw new Exception("Shader errors: "+material.shader.name);
        }
        RenderSettings.ambientEquatorColor=new Color(.35f,.39f,.39f);
        foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional)l.shadowStrength=.72f;
        OutbreakEditor.Values(Camera.main.GetComponent<CityContactShadows>(),s=>s.FindProperty("strength").floatValue=.85f);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        Capture("After",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        Capture("Street",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
        CharacterPreview();
        File.AppendAllText("Logs/ReclaimedCityPresentation.txt","PASS: presentation material shaders compile; final fill light and contact shadow strength calibrated.\n");
    }
}
