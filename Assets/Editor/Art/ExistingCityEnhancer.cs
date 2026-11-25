using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;
using G = CityArtGeometry;

/// <summary>Additive reference-image environment pass. The original scene and shared art stay untouched.</summary>
public static class ExistingCityEnhancer
{
    public const string ScenePath = "Assets/Scenes/EnhancedCity/ZombieMap.unity";
    public const string Root = "Assets/Art/ReferenceCity";
    private static Material brick, concrete, iron, wood, dark, glass, paint, wear, pale;
    private static readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();

    [MenuItem("Tools/Dead city/Enhance Existing City")]
    public static void Enhance()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing the environment.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != OutbreakEditor.Scene && scene.path != ScenePath)
            throw new InvalidOperationException("Open the existing ZombieMap scene first.");
        EditorSceneManager.SaveOpenScenes();
        Phase2AnimationAssets.Folder("Assets/Scenes/EnhancedCity");
        Phase2AnimationAssets.Folder(Root + "/Materials");
        Phase2AnimationAssets.Folder(Root + "/Meshes");
        Phase2AnimationAssets.Folder(Root + "/Prefabs");
        if (scene.path != ScenePath)
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("An enhanced copy already exists. Open that copy to adjust it.");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        var district = GameObject.Find("OutbreakDistrict").transform;
        if (district.Find("ReferenceCityEnhancement") != null)
        {
            Debug.Log("Enhancement already exists; retained all authored objects. Adjust the existing groups directly.");
            return;
        }
        copies.Clear();
        foreach (var renderer in district.GetComponentsInChildren<MeshRenderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(Clone).ToArray();
        brick = Surface("brick_wall_001",new Color(.76f,.72f,.65f),.47f);
        concrete = Surface("cracked_concrete_wall",new Color(.82f,.8f,.74f),.35f);
        iron = Surface("rusty_corrugated_iron",new Color(.55f,.51f,.44f),.5f);
        wood = Surface("wood_planks_grey",new Color(.65f,.6f,.49f),.7f);
        dark = Solid("Aged iron",new Color(.10f,.115f,.105f),.18f,.3f);
        glass = Solid("Dusty glass",new Color(.11f,.13f,.125f),.38f,.12f);
        paint = Solid("Dusty vehicle paint",new Color(.21f,.235f,.22f),.25f,.3f);
        pale = Solid("Chipped limestone",new Color(.48f,.475f,.425f),.12f);
        wear = WearMaterial();
        foreach (var r in district.GetComponentsInChildren<MeshRenderer>(true))
            if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("asphalt_02"))
            { r.sharedMaterial.SetColor("_Color",new Color(.68f,.69f,.64f));r.sharedMaterial.SetFloat("_Wetness",.05f); }
        var additions = G.Group("ReferenceCityEnhancement",district);
        Architecture(additions);
        Streets(additions,district);
        Polish(additions,district);
        Atmosphere(district);
        Optimize(district, additions);
        var surface=district.GetComponent<NavMeshSurface>();
        surface.BuildNavMesh();
        string navPath=Root+"/EnhancedCityNavMesh.asset";
        if (!AssetDatabase.Contains(surface.navMeshData)) AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(navPath));
        ValidateNavigation();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("REFERENCE_CITY: enhanced existing scene copy saved; original scene and gameplay scripts retained.");
    }

    private static Material Clone(Material source)
    {
        if (source == null || !(source.shader.name == "Standard" || source.shader.name.Contains("Weathered Surface"))) return source;
        if (copies.TryGetValue(source,out var found)) return found;
        string path=Root+"/Materials/"+source.name+".mat";
        var copy=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(copy==null){copy=new Material(source);AssetDatabase.CreateAsset(copy,path);}
        copy.enableInstancing=true;copies[source]=copy;return copy;
    }
    private static Material Surface(string name,Color tint,float scale)
    {
        var m=Clone(AssetDatabase.LoadAssetAtPath<Material>(CityArtAssets.Root+"/Materials/"+name+"_World.mat"));
        m.SetColor("_Color",tint);m.SetFloat("_Scale",scale);m.SetFloat("_Wetness",.03f);EditorUtility.SetDirty(m);return m;
    }
    private static Material Solid(string name,Color color,float smooth,float metal=0)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Glossiness",smooth);m.SetFloat("_Metallic",metal);m.enableInstancing=true;return m;
    }
    private static Material WearMaterial()
    {
        var texture=new Texture2D(256,256,TextureFormat.RGBA32,true){name="Irregular dust and seepage",wrapMode=TextureWrapMode.Clamp};
        var colors=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float u=x/255f,v=y/255f;
            float edge=Mathf.Clamp01(1f-(new Vector2(u-.5f,v-.5f).magnitude*2));
            float noise=Mathf.PerlinNoise(u*11,v*8)*Mathf.PerlinNoise(u*31+12,v*29);
            colors[y*256+x]=new Color(1,1,1,edge*noise*1.8f);
        }
        texture.SetPixels(colors);texture.Apply(true);AssetDatabase.CreateAsset(texture,Root+"/DustSeepage.asset");
        var m=new Material(Shader.Find("Outbreak/City Wear Decal")){name="Seepage and dust"};
        m.SetTexture("_MainTex",texture);m.SetColor("_Color",new Color(.17f,.15f,.11f,.64f));
        AssetDatabase.CreateAsset(m,Root+"/Materials/Seepage.mat");return m;
    }

    private static void Architecture(Transform root)
    {
        var architecture=G.Group("Facade additions",root);
        // Upper levels sit above the existing warehouse; its four entrances remain intact.
        var warehouse=G.Group("Warehouse upper tenement",architecture,new Vector3(-20,0,14));
        G.Box("Weathered upper masonry",warehouse,new Vector3(0,12.2f,0),new Vector3(20,11,20),concrete);
        Roof(warehouse,20,20,17.8f);
        Face(warehouse,new Vector3(10.06f,0,0),-90,20,4,6.7f);
        Face(warehouse,new Vector3(0,0,-10.06f),0,20,4,6.7f);
        Face(warehouse,new Vector3(-10.06f,0,0),90,20,4,6.7f);
        // Supplement the existing clinic rather than replacing its shell or interior.
        var clinic=G.Group("Clinic facade detail",architecture,new Vector3(21,0,15));
        Face(clinic,new Vector3(-9.42f,0,0),90,18,3,4.2f,true);
        Face(clinic,new Vector3(0,0,-9.42f),0,18,3,4.2f,true);
        // Foreground upper structures form the street silhouette; ground level stays open.
        foreach(float side in new[]{-1f,1f})
        {
            var upper=G.Group(side<0?"West street apartments":"East street apartments",architecture,new Vector3(side*17.5f,0,-25));
            float height=side<0?20.2f:16.9f;
            G.Box("Upper apartments",upper,new Vector3(0,(5+height)*.5f,0),new Vector3(12,height-5,18),side<0?brick:concrete);
            Roof(upper,12,18,height);
            Face(upper,new Vector3(-side*6.04f,0,0),side<0?-90:90,18,side<0?5:4,5);
            Face(upper,new Vector3(0,0,9.04f),180,12,side<0?5:4,5);
            // Ground-floor arcade columns leave existing alleys and tactical points reachable.
            foreach(float z in new[]{-8f,-3f,3f,8f})
            {
                var column=G.Box("Ground arcade pier",upper,new Vector3(-side*5.6f,2.5f,z),new Vector3(.55f,5,.65f),concrete);
                column.layer=8;column.AddComponent<BoxCollider>();
            }
            var shopfront=G.Group("Ground floor arcade",upper,new Vector3(-side*6.04f,0,0),side<0?-90:90);
            Storefront(shopfront,-5,2);Storefront(shopfront,5,2);
        }
    }

    // Facade faces local -Z. A single deterministic module contains a whole frontage.
    private static void Face(Transform parent,Vector3 position,float yaw,float width,int floors,float bottom,bool detailOnly=false)
    {
        var face=G.Group("Weathered facade",parent,position,yaw);
        int count=Mathf.FloorToInt(width/2.7f);
        for(int floor=0;floor<floors;floor++)
        {
            float y=bottom+1.4f+floor*2.95f;
            G.Box("Floor ledge",face,new Vector3(0,y-1.4f,-.15f),new Vector3(width+.2f,.13f,.4f),pale);
            for(int j=0;j<count;j++)
            {
                float x=(j-(count-1)*.5f)*2.7f;
                Window(face,new Vector3(x,y,-.10f),(j+floor)%7==2);
                if((j+floor)%5==0) Decal(face,new Vector3(x,y-1,-.43f),new Vector2(1.5f,2.8f));
            }
        }
        float top=bottom+floors*2.95f;
        G.Box("Ornamental roof cornice",face,new Vector3(0,top+.2f,-.24f),new Vector3(width+.4f,.35f,.55f),pale);
        foreach(float x in new[]{-width*.46f,width*.46f})
        {
            G.Box("Masonry pilaster",face,new Vector3(x,(bottom+top)*.5f,-.18f),new Vector3(.25f,top-bottom,.3f),concrete);
            G.Primitive(PrimitiveType.Cylinder,"Rusted downpipe",face,new Vector3(x+.18f,(bottom+top)*.5f,-.45f),new Vector3(.075f,(top-bottom)*.5f,.075f),dark);
        }
        Escape(face,width*.28f,bottom,floors);
        if (!detailOnly && bottom>5) Storefront(face,-width*.3f,bottom-3.4f);
    }
    private static void Window(Transform parent,Vector3 p,bool boarded)
    {
        G.Box("Deep dark window",parent,p,new Vector3(1.18f,1.73f,.10f),dark);
        G.Box("Dusty window panes",parent,p+new Vector3(0,0,-.06f),new Vector3(1.02f,1.56f,.025f),glass);
        foreach(float x in new[]{-.60f,0,.60f})G.Box("Window mullion",parent,p+new Vector3(x,0,-.12f),new Vector3(.055f,1.78f,.07f),pale);
        foreach(float y in new[]{-.88f,.05f,.88f})G.Box("Window crossbar",parent,p+new Vector3(0,y,-.12f),new Vector3(1.25f,.055f,.08f),pale);
        G.Box("Weathered sill",parent,p+new Vector3(0,-.98f,-.15f),new Vector3(1.48f,.14f,.35f),concrete);
        if(boarded)for(int b=0;b<3;b++)G.Box("Uneven window board",parent,p+new Vector3(0,-.55f+b*.48f,-.18f),new Vector3(1.3f,.19f,.07f),wood,new Vector3(0,0,b%2==0?9:-12));
    }
    private static void Escape(Transform parent,float x,float start,int floors)
    {
        for(int level=0;level<floors;level++)
        {
            float y=start+level*2.95f;
            var landing=G.Group("Fire escape landing",parent,new Vector3(x,y,-.95f));
            G.Box("Steel platform",landing,Vector3.zero,new Vector3(2.6f,.1f,1.4f),dark);
            for(int bar=-5;bar<=5;bar++)G.Box("Balustrade",landing,new Vector3(bar*.24f,.55f,-.68f),new Vector3(.03f,1.1f,.035f),dark);
            G.Box("Top rail",landing,new Vector3(0,1.1f,-.68f),new Vector3(2.6f,.045f,.05f),dark);
            foreach(float side in new[]{-1f,1f})G.Beam("Wall bracket",landing,new Vector3(side*1.2f,-.6f,.65f),new Vector3(side*1.2f,0,-.6f),.06f,dark);
            if(level==0)continue;
            float sign=level%2==0?-1:1;
            for(int step=0;step<14;step++)G.Box("Stair tread",landing,new Vector3(sign*(-1.15f+step*.17f),-2.95f+step*.211f,-.12f),new Vector3(.22f,.05f,.8f),dark);
            foreach(float z in new[]{-.55f,.30f})G.Beam("Stair handrail",landing,new Vector3(-sign*1.2f,-2.05f,z),new Vector3(sign*1.2f,.9f,z),.045f,dark);
        }
    }
    private static void Roof(Transform parent,float w,float d,float y)
    {
        G.Box("Flat roof cap",parent,new Vector3(0,y,0),new Vector3(w+.3f,.28f,d+.3f),pale);
        foreach(float x in new[]{-w*.5f,w*.5f})G.Box("Side parapet",parent,new Vector3(x,y+.5f,0),new Vector3(.3f,.9f,d),concrete);
        G.Box("Roof access",parent,new Vector3(-2,y+1.2f,1),new Vector3(3,2.4f,3),brick);
        G.Box("Roof air handler",parent,new Vector3(3,y+.7f,-2),new Vector3(2.3f,1.4f,2),iron);
        for(int i=0;i<3;i++)G.Primitive(PrimitiveType.Cylinder,"Roof vent",parent,new Vector3(-3+i*2,y+.6f,-4),new Vector3(.3f,.6f,.3f),dark);
        G.Beam("Antenna",parent,new Vector3(4,y,3),new Vector3(4,y+4,3),.045f,dark);
        G.Beam("Antenna array",parent,new Vector3(3,y+3,3),new Vector3(5,y+3,3),.035f,dark);
    }
    private static void Storefront(Transform parent,float x,float y)
    {
        G.Box("Rusted storefront shutter",parent,new Vector3(x,y,-.08f),new Vector3(3.4f,2.7f,.08f),iron);
        for(int i=0;i<22;i++)G.Box("Shutter rib",parent,new Vector3(x,y-1.2f+i*.11f,-.14f),new Vector3(3.4f,.028f,.045f),dark);
        G.Box("Old shop sign",parent,new Vector3(x,y+1.7f,-.22f),new Vector3(4,.62f,.15f),wood);
        G.Box("Sloped shop awning",parent,new Vector3(x,y+1.28f,-.65f),new Vector3(3.7f,.12f,1.3f),iron,new Vector3(12,0,0));
    }
    private static void Decal(Transform parent,Vector3 position,Vector2 size)
    {
        var quad=G.Primitive(PrimitiveType.Quad,"Water seepage",parent,position,new Vector3(size.x,size.y,1),wear);
        quad.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }

    private static void Streets(Transform root,Transform district)
    {
        var dressing=G.Group("Street decay",root);
        var random=new System.Random(3419);
        for(int i=0;i<115;i++)
        {
            float side=i%2==0?-1:1;float z=-35+(float)random.NextDouble()*69;
            float x=side*(4.35f+(float)random.NextDouble()*2.7f);
            G.Box("Small broken masonry",dressing,new Vector3(x,.09f,z),new Vector3(.05f+(float)random.NextDouble()*.28f,.035f+(float)random.NextDouble()*.07f,.08f+(float)random.NextDouble()*.3f),i%3==0?brick:concrete,new Vector3(random.Next(-8,9),random.Next(360),random.Next(-8,9)));
        }
        for(int i=0;i<20;i++)
        {
            float z=-34+(float)random.NextDouble()*67,x=-4+(float)random.NextDouble()*8;
            var q=G.Primitive(PrimitiveType.Quad,"Dust accumulation",dressing,new Vector3(x,.053f,z),new Vector3(2+(float)random.NextDouble()*2,1+(float)random.NextDouble()*2,1),wear,new Vector3(90,0,random.Next(360)));
            q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            for(int k=0;k<4;k++) G.Beam("Asphalt fracture",dressing,new Vector3(x+k*.23f,.054f,z+Mathf.Sin(k)*.2f),new Vector3(x+(k+1)*.23f,.054f,z+Mathf.Sin(k+1)*.2f),.016f,dark);
        }
        var template=district.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="WreckedSedan");
        if(template!=null)
        {
            foreach(var location in new[]{new Vector4(-3.7f,0,-27,22),new Vector4(3.8f,0,9,-12),new Vector4(-3.6f,0,25,9)})
            {
                var car=Object.Instantiate(template.gameObject,dressing);car.name="Abandoned vehicle";
                car.transform.localPosition=new Vector3(location.x,0,location.z);car.transform.localRotation=Quaternion.Euler(0,location.w,0);
                foreach(var r in car.GetComponentsInChildren<MeshRenderer>())if(r.name=="DentedBody"||r.name=="Roof")r.sharedMaterial=paint;
                VehicleDetails(car.transform);
                car.layer=8;var collider=car.AddComponent<BoxCollider>();collider.center=new Vector3(0,.7f,0);collider.size=new Vector3(1.9f,1.4f,4.3f);
            }
        }
        foreach(var p in new[]{new Vector3(-7.1f,0,-13),new Vector3(8,0,26)})Dumpster(dressing,p);
        foreach(var p in new[]{new Vector3(6.8f,0,-29),new Vector3(-7.5f,0,13),new Vector3(8.5f,0,28)})Tree(dressing,p);
        var fence=G.Group("Damaged railing",dressing,new Vector3(-7,0,28));
        for(int i=0;i<12;i++)G.Box("Fence picket",fence,new Vector3(i*.35f,.75f,0),new Vector3(.035f,1.5f,.035f),dark,new Vector3(0,0,i==3?13:0));
        G.Box("Fence rail",fence,new Vector3(1.9f,1.1f,0),new Vector3(4.1f,.05f,.05f),dark);
    }
    private static void VehicleDetails(Transform car)
    {
        foreach(float side in new[]{-1f,1f})
        {
            G.Box("Dusty door sill",car,new Vector3(side*.97f,.49f,0),new Vector3(.035f,.04f,3),dark);
            G.Box("Door seam",car,new Vector3(side*.975f,.72f,-.12f),new Vector3(.02f,.45f,.018f),dark);
            G.Box("Wing mirror",car,new Vector3(side*1.02f,1.06f,-.72f),new Vector3(.2f,.12f,.22f),paint);
            for(int j=0;j<7;j++)G.Box("Front grille",car,new Vector3(j*.13f-.39f,.58f,-2.14f),new Vector3(.045f,.2f,.035f),dark);
        }
        Decal(car,new Vector3(0,.64f,-2.18f),new Vector2(1.6f,.5f));
        G.Box("Rear plate",car,new Vector3(0,.56f,2.16f),new Vector3(.52f,.18f,.025f),pale);
    }
    private static void Dumpster(Transform parent,Vector3 p)
    {
        var d=G.Group("Rusted dumpster",parent,p,25);
        G.Box("Dumpster body",d,new Vector3(0,.55f,0),new Vector3(1.8f,1,.95f),iron);
        foreach(float x in new[]{-.7f,0,.7f})G.Box("Dumpster pressed rib",d,new Vector3(x,.55f,-.49f),new Vector3(.05f,.8f,.05f),dark);
        G.Box("Open lid",d,new Vector3(0,1.2f,.32f),new Vector3(1.9f,.06f,1),dark,new Vector3(-28,0,0));
        d.gameObject.layer=8;var c=d.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,.6f,0);c.size=new Vector3(1.8f,1.2f,1);
    }
    private static void Tree(Transform parent,Vector3 p)
    {
        var tree=G.Group("Dead street tree",parent,p);
        G.Beam("Bare trunk",tree,Vector3.zero,new Vector3(.1f,3.5f,.03f),.16f,wood);
        var r=new System.Random((int)(p.z*10+1000));
        for(int i=0;i<9;i++)
        {
            float y=1.7f+i*.19f,sign=i%2==0?-1:1;
            var end=new Vector3(sign*(.6f+(float)r.NextDouble()*.6f),y+.8f,(float)r.NextDouble()-.5f);
            G.Beam("Dead branch",tree,new Vector3(.06f,y,0),end,.055f,wood);
            G.Beam("Dead twig",tree,end,end+new Vector3(sign*.3f,.5f,-.2f),.022f,wood);
        }
    }

    private static void Atmosphere(Transform district)
    {
        string skyPath=Root+"/Materials/Overcast sky.mat";
        var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if(sky==null){sky=new Material(Shader.Find("Outbreak/Overcast City Sky")){name="Overcast gray city sky"};AssetDatabase.CreateAsset(sky,skyPath);}
        sky.SetColor("_SkyColor",new Color(.85f,.84f,.8f));sky.SetColor("_Horizon",new Color(.65f,.67f,.63f));sky.SetColor("_CloudDark",new Color(.53f,.55f,.52f));
        EditorUtility.SetDirty(sky);RenderSettings.skybox=sky;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.52f,.545f,.515f);RenderSettings.fogDensity=.014f;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.53f,.55f,.52f);
        RenderSettings.ambientEquatorColor=new Color(.38f,.395f,.37f);
        RenderSettings.ambientGroundColor=new Color(.22f,.235f,.22f);
        foreach(var l in Object.FindObjectsByType<Light>())
        {
            if(l.type==LightType.Directional){l.transform.rotation=Quaternion.Euler(47,-35,0);l.color=new Color(1,.95f,.84f);l.intensity=.82f;l.shadows=LightShadows.Soft;l.shadowStrength=.58f;}
            else if(l.name=="StreetPool")l.intensity=.12f;
        }
        var camera=Camera.main;camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=200;
        var atmosphere=camera.GetComponent<CityCameraAtmosphere>();
        if(atmosphere!=null)OutbreakEditor.Values(atmosphere,s=>{
            s.FindProperty("exposure").floatValue=1.05f;s.FindProperty("saturation").floatValue=.73f;
            s.FindProperty("contrast").floatValue=1.04f;s.FindProperty("vignette").floatValue=.12f;s.FindProperty("bloom").floatValue=.07f;
        });
        ConfigureStaticReflections(district);
    }

    public static void ConfigureStaticReflections(Transform district)
    {
        string path=Root+"/OvercastReflection.asset";
        var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if(cube==null)
        {
            const int size=64;
            cube=new Cubemap(size,TextureFormat.RGBA32,true){name="Overcast static reflection"};
            for(int face=0;face<6;face++)
            {
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=x/(float)(size-1)*2-1,v=y/(float)(size-1)*2-1;
                    Vector3 d=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);
                    d.Normalize();float noise=Mathf.PerlinNoise(d.x*4+12,d.z*4+8)*.11f;
                    pixels[y*size+x]=Color.Lerp(new Color(.19f,.20f,.18f),new Color(.64f-noise,.66f-noise,.62f-noise),Mathf.InverseLerp(-.3f,.5f,d.y));
                }
                cube.SetPixels(pixels,(CubemapFace)face);
            }
            cube.Apply(true);AssetDatabase.CreateAsset(cube,path);
        }
        foreach(var probe in district.GetComponentsInChildren<ReflectionProbe>())
        {probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=cube;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;EditorUtility.SetDirty(probe);}
    }

    private static void Polish(Transform additions,Transform district)
    {
        foreach(var r in district.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(r.sharedMaterial!=null && (r.sharedMaterial.name=="RoadPaint"||r.sharedMaterial.name=="FadedRoadPaint"))
            {r.sharedMaterial.color=new Color(.32f,.30f,.22f);EditorUtility.SetDirty(r.sharedMaterial);}
            if(r.name=="DentedBody"||r.name=="Roof"&&r.transform.parent.name=="WreckedSedan")r.sharedMaterial=paint;
        }
        var warehouse=district.Find("Buildings/Warehouse");
        if(warehouse!=null)foreach(var r in warehouse.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=concrete;
        var roof=district.Find("HorrorCity/Architecture/RiversideFreight");
        if(roof!=null)foreach(var r in roof.GetComponentsInChildren<MeshRenderer>())
            if(r.name=="RoofLeft"||r.name=="RoofRight")r.enabled=false;
        var infill=G.Group("Warehouse masonry transition",additions.Find("Facade additions"),new Vector3(-20,0,14));
        G.Box("Upper floor transition",infill,new Vector3(0,5.9f,0),new Vector3(20,1.8f,20),concrete);
        var skyline=G.Group("Skyline masonry details",additions.Find("Facade additions"));
        var distant=district.Find("HorrorCity/DistantCity");
        if(distant!=null)foreach(var t in distant.GetComponentsInChildren<Transform>().Where(t=>t.name=="BackgroundTenement"))
        {
            var masonry=t.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Masonry");float height=masonry.bounds.size.y;
            var detail=G.Group("Highrise pilasters",skyline);detail.position=t.position;detail.rotation=t.rotation;
            foreach(float x in new[]{-7.7f,-4.1f,4.1f,7.7f})G.Box("Vertical masonry bay",detail,new Vector3(x,height*.5f,-6.11f),new Vector3(.18f,height,.16f),pale);
            for(float y=5;y<height-1;y+=3.1f)G.Box("Highrise sill course",detail,new Vector3(0,y,-6.11f),new Vector3(16,.10f,.14f),pale);
        }
        // Low-cost LODs for the distant silhouettes: cull tiny screen-space details.
        foreach(Transform child in skyline)
        {
            var lod=child.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,child.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        }
        foreach(var t in additions.GetComponentsInChildren<Transform>())
            if(t.name=="Upper apartments"||t.name=="Weathered upper masonry"||t.name=="Upper floor transition"||t.name=="Rusted storefront shutter")
            {t.gameObject.layer=8;if(t.GetComponent<Collider>()==null)t.gameObject.AddComponent<BoxCollider>();}
    }

    private static void Optimize(Transform district,Transform additions)
    {
        // Combine static decorative renderers per building/material, preserving source objects.
        var old=district.Find("HorrorCity");
        if(old!=null)foreach(Transform group in old)
        {
            if(group.name=="Architecture" || group.name=="DistantCity")foreach(Transform child in group)Combine(child);
            else if(group.name=="RoadAndDrainage")Combine(group);
        }
        foreach(Transform group in additions)foreach(Transform child in group.Cast<Transform>().ToArray())
            if(group.name=="Facade additions" || child.name=="Abandoned vehicle" || child.name=="Dead street tree" || child.name=="Rusted dumpster")
            {
                if(child.name=="Skyline masonry details")foreach(Transform tower in child){Combine(tower);var lod=tower.GetComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.018f,tower.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray())});lod.RecalculateBounds();}
                else Combine(child);
            }
        foreach(Transform group in additions)foreach(Transform child in group)
            if(child.name=="Abandoned vehicle"||child.name=="Rusted dumpster"||child.name=="Dead street tree"||child.name=="Warehouse upper tenement")
                PrefabUtility.SaveAsPrefabAsset(child.gameObject,Root+"/Prefabs/"+child.name+".prefab");
    }
    private static void Combine(Transform chunk)
    {
        var renderers=chunk.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.GetComponent<MeshFilter>()!=null&&r.sharedMaterials.Length==1&&r.sharedMaterial!=wear&&r.GetComponent<MeshFilter>().sharedMesh.isReadable).ToArray();
        foreach(var batch in renderers.GroupBy(r=>r.sharedMaterial))
        {
            var mesh=new Mesh{name=chunk.name+" "+batch.Key.name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(batch.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=chunk.worldToLocalMatrix*r.localToWorldMatrix}).ToArray());
            string path=AssetDatabase.GenerateUniqueAssetPath(Root+"/Meshes/"+mesh.name.Replace('/','_')+".asset");AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject("Batched "+batch.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(chunk,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=batch.Key;
            foreach(var renderer in batch)renderer.enabled=false;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
        }
    }
    public static void ValidateNavigation()
    {
        var markers=Object.FindObjectsByType<MapMarker>();
        var spawn=markers.First(m=>m.kind==MapMarkerKind.PlayerSpawn);
        if(!NavMesh.SamplePosition(spawn.transform.position,out var start,2,NavMesh.AllAreas))throw new Exception("Player spawn is not on NavMesh.");
        foreach(var marker in markers)
        {
            if(!NavMesh.SamplePosition(marker.transform.position,out var hit,2,NavMesh.AllAreas))throw new Exception(marker.name+" is outside navigation.");
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(hit.position,start.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception(marker.name+" lost its route to the player.");
        }
    }
}
