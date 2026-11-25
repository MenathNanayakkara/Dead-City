using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using G = CityArtGeometry;

/// <summary>A repeatable, additive art pass over the existing collision and navigation layout.</summary>
public static class AbandonedCityArtPass
{
    private static Material brick, concrete, asphalt, iron, wood, paving, trim, glass, glow, black;

    [MenuItem("Dead city/Art/Apply abandoned city art pass")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing art.");
        EditorSceneManager.SaveOpenScenes();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != OutbreakEditor.Scene) OutbreakEditor.Open();
        CityArtAssets.Prepare();
        Materials();
        var district = GameObject.Find("OutbreakDistrict").transform;
        RefinishExisting(district);
        if (district.Find("HorrorCity") == null)
        {
            var stale = district.Find("HorrorCity_Draft");
            if (stale != null) Object.DestroyImmediate(stale.gameObject);
            var art = G.Group("HorrorCity_Draft",district);
            Streets(art);
            Buildings(art);
            Skyline(art);
            Props(art,district);
            art.name = "HorrorCity";
        }
        Lighting(district);
        PolishSurfaces(district);
        CityCharacterArt.Apply();
        Phase5Setup.Validate();
        Phase18Setup.Validate();
        OutbreakEditor.Save("ABANDONED_CITY_ART");
        var view = SceneView.lastActiveSceneView;
        if (view != null) view.LookAt(new Vector3(0,2,-6),Quaternion.Euler(12,-12,0),22);
        Debug.Log("CITY_ART_PASS: photographic materials, city architecture, street dressing and lighting applied. Gameplay navigation checks passed.");
    }

    private static void Materials()
    {
        brick=CityArtAssets.Surface("brick_wall_001",.4f);
        concrete=CityArtAssets.Surface("cracked_concrete_wall",.42f);
        asphalt=CityArtAssets.Surface("asphalt_02",.33f,0,.4f);
        iron=CityArtAssets.Surface("rusty_corrugated_iron",.5f,.35f);
        wood=CityArtAssets.Surface("wood_planks_grey",.6f);
        paving=CityArtAssets.Surface("concrete_pavement",.5f);
        trim=CityArtAssets.Solid("OxidizedFrames",new Color(.14f,.17f,.16f),.32f,.45f);
        glass=CityArtAssets.Solid("BlackenedWindowGlass",new Color(.075f,.12f,.13f),.8f,.25f);
        black=CityArtAssets.Solid("RubberAndTar",new Color(.035f,.04f,.042f),.15f);
        glow=CityArtAssets.Solid("EmergencyAmber",new Color(.6f,.37f,.11f),.35f,0,new Color(1.3f,.59f,.16f));
    }

    private static void RefinishExisting(Transform district)
    {
        foreach(var renderer in district.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial == null) continue;
            string name=renderer.sharedMaterial.name;
            switch(name)
            {
                case "Concrete": case "Rubble": renderer.sharedMaterial=concrete; break;
                case "Asphalt": renderer.sharedMaterial=asphalt; break;
                case "Brick": renderer.sharedMaterial=brick; break;
                case "Wood": renderer.sharedMaterial=wood; break;
                case "Metal": case "DoorMetal": case "AbandonedVehicle": renderer.sharedMaterial=iron; break;
            }
        }
        var originalArt=district.Find("ArtPass");
        if(originalArt != null)
            foreach(Transform child in originalArt)
                if(child.name.StartsWith("AbandonedCar") || child.name=="OfficeWindow" || child.name=="HOLD THE LINE")
                    foreach(var renderer in child.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
        var atmosphere=district.Find("AtmosphereDetails");
        if(atmosphere != null)
            foreach(Transform child in atmosphere)
                if(child.name=="GroundGrime") child.gameObject.SetActive(false);
    }

    private static void Streets(Transform parent)
    {
        var street=G.Group("RoadAndDrainage",parent);
        foreach(float side in new[]{-1f,1f})
        {
            for(int z=-35;z<=35;z+=2)
            {
                if(z>-14 && z<-2)continue;
                G.Box("Kerbstone",street,new Vector3(side*5.12f,.085f,z),new Vector3(.28f,.14f,1.93f),concrete);
                G.Box("PavementSlab",street,new Vector3(side*6.25f,.057f,z),new Vector3(1.95f,.025f,1.96f),paving);
            }
        }
        var faded=CityArtAssets.Solid("FadedRoadPaint",new Color(.46f,.43f,.31f),.12f);
        foreach(float z in new[]{-14.5f,-1.5f})
            for(int i=-4;i<=4;i++) G.Box("WornCrosswalk",street,new Vector3(i,.055f,z),new Vector3(.53f,.015f,2.3f),faded);
        for(int z=-29;z<=28;z+=19)
        {
            G.Primitive(PrimitiveType.Cylinder,"Manhole",street,new Vector3(1.9f,.068f,z),new Vector3(.8f,.014f,.8f),trim);
            for(int i=-3;i<=3;i++) G.Box("ManholeRidge",street,new Vector3(1.9f+i*.085f,.086f,z),new Vector3(.028f,.012f,.57f),black);
            foreach(float side in new[]{-1f,1f})
            {
                G.Box("StormDrain",street,new Vector3(side*4.75f,.06f,z+3),new Vector3(.45f,.022f,.8f),black);
                for(int i=0;i<7;i++) G.Box("DrainBar",street,new Vector3(side*4.75f,.079f,z+2.69f+i*.1f),new Vector3(.43f,.025f,.025f),trim);
            }
        }
        var wet=CityArtAssets.Solid("StandingRainwater",new Color(.047f,.07f,.075f),.94f,.28f);
        var random=new System.Random(104);
        for(int i=0;i<19;i++) G.Puddle(street,new Vector3(-4+(float)random.NextDouble()*8,.059f,-34+(float)random.NextDouble()*68),.65f+(float)random.NextDouble()*1.7f,wet,random);
        // Small papers, stones and cable fragments stay below the controller step height.
        var paper=CityArtAssets.Solid("WaterloggedPaper",new Color(.42f,.4f,.31f),.1f);
        for(int i=0;i<70;i++)
        {
            float side=i%2==0?-1:1;
            var pos=new Vector3(side*(5.5f+(float)random.NextDouble()*2.3f),.09f,-34+(float)random.NextDouble()*68);
            G.Box("StreetLitter",street,pos,new Vector3(.08f+(float)random.NextDouble()*.22f,.018f,.1f+(float)random.NextDouble()*.19f),i%4==0?paper:concrete,new Vector3(0,random.Next(360),random.Next(-8,8)));
        }
    }

    private static void Buildings(Transform parent)
    {
        var block=G.Group("Architecture",parent);
        Apartment(block,"QuarantineClinic",new Vector3(21,0,15),18,18,3,"ST. BRIGID  /  MEDICAL CENTRE");
        Apartment(block,"MunicipalUtilities",new Vector3(-22,0,-24),15,14,1,"CITY UTILITIES  /  NO ENTRY");
        var warehouse=G.Group("RiversideFreight",block,new Vector3(-20,0,14));
        G.Box("RoofLeft",warehouse,new Vector3(-5,5.7f,0),new Vector3(10.5f,.18f,21),iron,new Vector3(0,0,9));
        G.Box("RoofRight",warehouse,new Vector3(5,5.7f,0),new Vector3(10.5f,.18f,21),iron,new Vector3(0,0,-9));
        for(int i=-9;i<=9;i+=3)
        {
            foreach(float side in new[]{-1f,1f})
            {
                G.Box("WallColumn",warehouse,new Vector3(side*10.34f,2.6f,i),new Vector3(.18f,5.2f,.22f),trim);
                G.Box("RoofBeam",warehouse,new Vector3(i,4.85f,0),new Vector3(.15f,.25f,20),trim);
            }
        }
        foreach(float side in new[]{-1f,1f})
        {
            var face=G.Group("LoadingFacade",warehouse,new Vector3(0,0,side*10.35f),side<0?0:180);
            G.Box("PortalHeader",face,new Vector3(0,3.85f,-.05f),new Vector3(4.4f,.3f,.3f),trim);
            foreach(float x in new[]{-2.15f,2.15f})G.Box("PortalUpright",face,new Vector3(x,1.85f,0),new Vector3(.18f,3.7f,.22f),trim);
            for(int x=-8;x<=8;x+=4) if(x!=0) Window(face,new Vector3(x,3.2f,-.04f),false);
            Sign(face,"RIVERSIDE FREIGHT",new Vector3(0,4.72f,-.2f),6.4f,.65f,Color.white);
        }
        for(int z=-7;z<=7;z+=7)
        {
            G.Beam("RoofTruss",warehouse,new Vector3(-9.6f,4.7f,z),new Vector3(0,6.4f,z),.13f,trim);
            G.Beam("RoofTruss",warehouse,new Vector3(9.6f,4.7f,z),new Vector3(0,6.4f,z),.13f,trim);
            G.Beam("TieBeam",warehouse,new Vector3(-9.6f,4.7f,z),new Vector3(9.6f,4.7f,z),.1f,trim);
        }
        // Signs sit against physical walls, clear of the four existing entrances.
        Sign(warehouse,"07",new Vector3(-5.5f,2,-10.4f),1.4f,1.7f,new Color(.67f,.62f,.43f));
        foreach(float side in new[]{-1f,1f})
            G.Primitive(PrimitiveType.Cylinder,"Downpipe",warehouse,new Vector3(side*10.5f,2.4f,9),new Vector3(.14f,2.4f,.14f),trim);
    }

    private static void Apartment(Transform parent,string name,Vector3 position,float width,float depth,int floors,string sign)
    {
        var root=G.Group(name,parent,position);
        float top=4+floors*3.1f;
        G.Box("UpperBuilding",root,new Vector3(0,(4+top)/2,0),new Vector3(width,top-4,depth),floors>1?brick:concrete);
        G.Box("Parapet",root,new Vector3(0,top+.17f,0),new Vector3(width+.6f,.34f,depth+.6f),concrete);
        G.Box("InteriorCeiling",root,new Vector3(0,3.96f,0),new Vector3(width,.12f,depth),concrete);
        for(int side=0;side<4;side++)
        {
            bool front=side%2==0;
            float span=front?width:depth;
            Vector3 offset=side==0?new Vector3(0,0,-depth/2):side==1?new Vector3(-width/2,0,0):side==2?new Vector3(0,0,depth/2):new Vector3(width/2,0,0);
            var face=G.Group("Facade",root,offset,side*90);
            for(int floor=0;floor<floors;floor++)
            {
                float y=5.55f+floor*3.1f;
                G.Box("FloorCornice",face,new Vector3(0,y-1.35f,-.2f),new Vector3(span+.3f,.18f,.42f),concrete);
                int count=Mathf.FloorToInt(span/3);
                for(int j=0;j<count;j++)
                {
                    float x=(j-(count-1)*.5f)*2.8f;
                    Window(face,new Vector3(x,y,-.34f),(j+floor+side)%6==0);
                    if(floor==0 && j%3==1) G.Box("AirConditioner",face,new Vector3(x,y-1.1f,-.57f),new Vector3(.82f,.42f,.52f),iron);
                }
            }
            foreach(float x in new[]{-span*.36f,span*.36f})
            {
                Window(face,new Vector3(x,2.1f,-.34f),false);
                for(int plank=0;plank<3;plank++)G.Box("BoardedWindow",face,new Vector3(x,1.55f+plank*.44f,-.59f),new Vector3(1.62f,.24f,.085f),wood,new Vector3(0,0,plank%2==0?7:-9));
            }
        }
        Sign(root,sign,new Vector3(0,3.38f,-depth/2-.4f),width*.83f,.56f,new Color(.76f,.76f,.63f));
        G.Box("RoofPlant",root,new Vector3(-width*.2f,top+.7f,0),new Vector3(3.1f,1.3f,2.1f),iron);
        for(int i=0;i<8;i++) G.Box("PlantLouvre",root,new Vector3(-width*.2f-1.3f+i*.36f,top+.7f,-1.07f),new Vector3(.09f,1.1f,.06f),trim);
    }

    private static void Window(Transform parent,Vector3 position,bool lit)
    {
        G.Box("WindowRecess",parent,position,new Vector3(1.48f,1.88f,.09f),black);
        G.Box("WindowGlass",parent,position+new Vector3(0,0,-.06f),new Vector3(1.3f,1.68f,.035f),lit?glow:glass);
        foreach(float x in new[]{-.72f,0,.72f})G.Box("WindowFrame",parent,position+new Vector3(x,0,-.12f),new Vector3(.055f,1.85f,.08f),trim);
        foreach(float y in new[]{-.92f,0,.92f})G.Box("WindowFrame",parent,position+new Vector3(0,y,-.12f),new Vector3(1.46f,.06f,.08f),trim);
        G.Box("WindowSill",parent,position+new Vector3(0,-1.01f,-.15f),new Vector3(1.7f,.16f,.38f),concrete);
    }

    private static void Skyline(Transform parent)
    {
        var skyline=G.Group("DistantCity",parent);
        var random=new System.Random(73);
        for(int side=0;side<4;side++)
        {
            var row=G.Group("CityBlock",skyline,Vector3.zero,side*90);
            for(int i=-2;i<=2;i++)
            {
                float height=13+random.Next(16),x=i*19+random.Next(-2,3);
                var building=G.Group("BackgroundTenement",row,new Vector3(x,0,49));
                G.Box("Masonry",building,new Vector3(0,height/2,0),new Vector3(16,height,12),i%2==0?brick:concrete);
                G.Box("RoofLine",building,new Vector3(0,height,0),new Vector3(16.5f,.45f,12.4f),trim);
                for(float y=6;y<height-1;y+=3.1f)
                    for(int j=-2;j<=2;j++)G.Box("DistantWindow",building,new Vector3(j*2.8f,y,-6.03f),new Vector3(1.05f,1.5f,.06f),random.Next(12)==0?glow:glass);
            }
        }
    }

    private static void Props(Transform parent,Transform district)
    {
        var props=G.Group("StreetFurniture",parent);
        G.Car(props,new Vector3(13,0,-6),65,iron,trim,glass,black);
        G.Car(props,new Vector3(-9,0,27),-15,iron,trim,glass,black);
        foreach(var pos in new[]{new Vector3(-6.4f,0,-27),new Vector3(6.5f,0,14),new Vector3(6.3f,0,-19)})
            CityArtAssets.Model("fire_hydrant",props,pos,.88f,35);
        foreach(var pos in new[]{new Vector3(7.3f,0,-31),new Vector3(-7.1f,0,3),new Vector3(10.6f,0,8),new Vector3(-29,0,-17)})
        {
            CityArtAssets.Model("metal_trash_can",props,pos,1.05f,60);
            CityArtAssets.Model("trashbag",props,pos+new Vector3(.7f,0,.25f),.65f,130);
            CityArtAssets.Model("trashbag",props,pos+new Vector3(.3f,0,-.5f),.48f,35);
        }
        var physics=district.Find("PhysicsObjects");
        foreach(Transform item in physics)
        {
            var own=item.GetComponent<Renderer>();
            if(item.name.StartsWith("Barrel"))
            {
                if(own!=null)own.enabled=false;
                CityArtAssets.Model("Barrel_01",item,item.position-Vector3.up*.6f,1.2f,15);
            }
            else
            {
                var detail=G.Group("CrateStraps",item);
                detail.localScale=new Vector3(1/item.lossyScale.x,1/item.lossyScale.y,1/item.lossyScale.z);
                foreach(float z in new[]{-.4f,.4f})G.Box("SteelBand",detail,new Vector3(0,0,z),new Vector3(1.13f,1.24f,.06f),trim);
            }
        }
        foreach(Transform barricade in district.Find("Barricades"))
        {
            // Supplied team models take priority over the generated fallback.
            if(barricade.Find("TeamBarricadeVisual")!=null)continue;
            var own=barricade.GetComponent<Renderer>(); if(own!=null)own.enabled=false;
            var detail=G.Group("TimberBarricadeDetail",barricade);
            detail.localScale=new Vector3(1/barricade.lossyScale.x,1/barricade.lossyScale.y,1/barricade.lossyScale.z);
            foreach(float x in new[]{-.87f,.87f})G.Box("SupportPost",detail,new Vector3(x,0,0),new Vector3(.13f,1.3f,.18f),wood,new Vector3(0,0,x*4));
            for(int i=0;i<4;i++)G.Box("SplinteredPlank",detail,new Vector3(0,-.4f+i*.27f,-.16f),new Vector3(2.4f,.2f,.1f),wood,new Vector3(0,0,i%2==0?2:-3));
            G.Beam("CrossBrace",detail,new Vector3(-1,-.53f,.15f),new Vector3(1,.53f,.15f),.14f,wood);
        }
        for(int side=-1;side<=1;side+=2)
        {
            var pole=G.Group("UtilityPole",props,new Vector3(side*7.3f,0,-24));
            G.Primitive(PrimitiveType.Cylinder,"Pole",pole,new Vector3(0,4.2f,0),new Vector3(.21f,4.2f,.21f),wood);
            G.Box("CrossArm",pole,new Vector3(0,7.3f,0),new Vector3(2,.14f,.16f),trim);
        }
        for(int wire=0;wire<3;wire++)
        {
            Vector3 previous=new Vector3(-7.3f,7.4f,-24+wire*.25f);
            for(int i=1;i<=16;i++)
            {
                float t=i/16f;
                var next=new Vector3(Mathf.Lerp(-7.3f,7.3f,t),7.4f-Mathf.Sin(t*Mathf.PI)*1.15f,-24+wire*.25f);
                G.Beam("SaggingCable",props,previous,next,.023f,black); previous=next;
            }
        }
    }

    private static void Sign(Transform parent,string text,Vector3 pos,float width,float height,Color color)
    {
        G.Box("WeatheredSignBacking",parent,pos,new Vector3(width+.2f,height+.14f,.08f),trim);
        var go=new GameObject("Sign - "+text,typeof(RectTransform),typeof(TextMeshPro));
        go.transform.SetParent(parent,false); go.transform.localPosition=pos+new Vector3(0,0,-.052f);
        var label=go.GetComponent<TextMeshPro>(); label.text=text;
        label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase4UIFactory.FontPath);
        label.fontSize=2.2f; label.enableAutoSizing=true; label.fontSizeMin=.7f; label.fontSizeMax=2.2f;
        label.color=color; label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(width,height);
        label.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }

    private static void Lighting(Transform district)
    {
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.075f,.1f,.115f); RenderSettings.fogDensity=.018f;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.33f,.39f,.43f);
        RenderSettings.ambientEquatorColor=new Color(.17f,.205f,.23f);
        RenderSettings.ambientGroundColor=new Color(.085f,.095f,.1f);
        foreach(var light in Object.FindObjectsByType<Light>())
        {
            if(light.type==LightType.Directional)
            {
                light.transform.rotation=Quaternion.Euler(32,-35,0);
                light.color=new Color(.67f,.79f,.87f); light.intensity=.82f;
                light.shadows=LightShadows.Soft; light.shadowBias=.035f; light.shadowNormalBias=.25f;
            }
            else if(light.name=="StreetPool")
            {
                light.type=LightType.Spot; light.spotAngle=112; light.innerSpotAngle=65;
                light.transform.rotation=Quaternion.Euler(90,0,0); light.range=17;
                light.intensity=2.2f; light.color=new Color(1,.69f,.36f);
            }
        }
        var camera=Camera.main;
        camera.backgroundColor=RenderSettings.fogColor; camera.farClipPlane=180; camera.allowHDR=true;
        QualitySettings.shadowDistance=48; QualitySettings.shadowResolution=ShadowResolution.High;
        QualitySettings.pixelLightCount=8; QualitySettings.anisotropicFiltering=AnisotropicFiltering.Enable;
        QualitySettings.antiAliasing=4;
        var effects=OutbreakEditor.Add<CityCameraAtmosphere>(camera.gameObject);
        effects.Configure(Shader.Find("Hidden/Outbreak/Atmosphere"));
        EditorUtility.SetDirty(effects);
        if(district.Find("CityReflectionProbe")==null)
        {
            var probe=G.Group("CityReflectionProbe",district,new Vector3(0,4,-4)).gameObject.AddComponent<ReflectionProbe>();
            probe.mode=ReflectionProbeMode.Realtime; probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution=128; probe.size=new Vector3(85,35,85); probe.boxProjection=true;
            probe.cullingMask=~(1<<9); probe.clearFlags=ReflectionProbeClearFlags.SolidColor; probe.backgroundColor=RenderSettings.fogColor;
        }
    }

    private static void PolishSurfaces(Transform district)
    {
        // Match the underlying asphalt at the puddle boundary; a black polygon reads as a hole.
        string path=CityArtAssets.Root+"/Materials/StandingRainwater.mat";
        var wet=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(wet!=null)
        {
            wet.shader=asphalt.shader;
            wet.CopyPropertiesFromMaterial(asphalt);
            wet.SetFloat("_Wetness",.8f);
            wet.SetFloat("_SmoothnessFloor",.86f);
            wet.SetFloat("_NormalStrength",.12f);
            EditorUtility.SetDirty(wet);
        }
        var art=district.Find("HorrorCity");
        if(art!=null)
        {
            foreach(var renderer in art.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
            foreach(var filter in art.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                if(filter.name!="DentedBody" || mesh.vertexCount!=48 || mesh.triangles.Length!=240)continue;
                var triangles=mesh.triangles.ToList();
                for(int j=1;j<7;j++)triangles.AddRange(new[]{0,j+1,j,40,40+j,41+j});
                mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateTangents();
                EditorUtility.SetDirty(mesh);
            }
        }
        // Refresh only generated scanned prop instances, never their internal source transforms.
        var props=art!=null?art.Find("StreetFurniture"):null;
        if(props!=null)
        {
            foreach(var item in props.Cast<Transform>().ToArray())
                if(item.name=="fire_hydrant" || item.name=="trashbag" || item.name=="metal_trash_can")
                    Object.DestroyImmediate(item.gameObject);
            foreach(var pos in new[]{new Vector3(-6.4f,0,-27),new Vector3(6.5f,0,14),new Vector3(6.3f,0,-19)})
                CityArtAssets.Model("fire_hydrant",props,pos,.88f,35);
            foreach(var pos in new[]{new Vector3(7.3f,0,-31),new Vector3(-7.1f,0,3),new Vector3(10.6f,0,8),new Vector3(-29,0,-17)})
            {
                CityArtAssets.Model("metal_trash_can",props,pos,1.05f,60);
                CityArtAssets.Model("trashbag",props,pos+new Vector3(.7f,0,.25f),.65f,130);
                CityArtAssets.Model("trashbag",props,pos+new Vector3(.3f,0,-.5f),.48f,35);
            }
        }
        foreach(Transform item in district.Find("PhysicsObjects"))
        {
            if(!item.name.StartsWith("Barrel"))continue;
            var old=item.Find("Barrel_01");
            if(old!=null)Object.DestroyImmediate(old.gameObject);
            CityArtAssets.Model("Barrel_01",item,item.position-Vector3.up*.6f,1.2f,15);
        }
    }
}
