using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;
using Leaves=ReclaimedCityPresentation.Leaves;

/// <summary>Video-reference surface detail, inside existing street-edge and facade footprints.</summary>
public static class StealthReferenceDressing
{
    private static readonly System.Random random=new System.Random(22941);
    private static float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
    private static Color Green()=>Color.Lerp(new Color(.085f,.16f,.035f),new Color(.26f,.35f,.075f),Range(0,1));
    [MenuItem("Tools/Dead city/Apply stealth reference environment detail")]
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Leave Play Mode first.");
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        var district=GameObject.Find("OutbreakDistrict").transform;
        if(district.Find("Reference foliage detail")!=null)throw new Exception("Reference dressing is already present; edit the existing group.");
        var transforms=district.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale));
        var colliders=district.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
        var nav=district.GetComponent<NavMeshSurface>().navMeshData;
        Directory.CreateDirectory("Previews/StealthReference");
        ReclaimedCityPresentation.Capture("StealthBefore",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        var root=new GameObject("Reference foliage detail").transform;root.SetParent(district,false);
        var foliage=AssetDatabase.LoadAssetAtPath<Material>(ReclaimedCityPresentation.Root+"/Materials/Foliage.mat");
        var litter=new Material(foliage){name="Dry leaf litter"};litter.SetFloat("_Wind",0);AssetDatabase.CreateAsset(litter,ReclaimedCityPresentation.Root+"/Materials/Leaf litter.mat");
        Physics.SyncTransforms();
        for(int zone=0;zone<8;zone++)
        {
            var ferns=new Leaves();var groundLeaves=new Leaves();float start=-34+zone*8;
            for(int patch=0;patch<12;patch++)
            {
                float side=patch%2==0?-1:1;var point=new Vector3(side*Range(5.7f,7.5f),3,start+Range(0,8));
                if(!Physics.Raycast(point,Vector3.down,out var hit,4,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.5f)continue;
                Fern(ferns,hit.point,Range(.25f,.45f));
                for(int leaf=0;leaf<28;leaf++)
                {
                    var p=hit.point+new Vector3(Range(-.65f,.65f),.008f,Range(-.65f,.65f));
                    float angle=Range(0,Mathf.PI*2);var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    groundLeaves.Leaf(p,dir,Vector3.Cross(dir,Vector3.up),Range(.055f,.16f),Range(.018f,.045f),Color.Lerp(new Color(.12f,.08f,.035f),new Color(.33f,.24f,.085f),Range(0,1)));
                }
            }
            ferns.Save(root,"Curb ferns "+zone,foliage);groundLeaves.Save(root,"Leaf litter "+zone,litter);
        }
        int id=0;
        foreach(float side in new[]{-1f,1f})foreach(float z in new[]{-32f,-17f,4f,23f})
        {
            var point=new Vector3(side*10.7f,4,z);
            if(!Physics.Raycast(point,Vector3.down,out var hit,5,1<<8,QueryTriggerInteraction.Ignore)||hit.point.y>.6f)continue;
            var bush=new Leaves();
            for(int stem=0;stem<20;stem++)
            {
                float angle=Range(0,Mathf.PI*2);var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var basePoint=hit.point+radial*Range(0,.3f);var end=basePoint+Vector3.up*Range(.3f,.65f)+radial*.22f;
                for(int k=1;k<=7;k++)
                {
                    var p=Vector3.Lerp(basePoint,end,k/7f);float theta=angle+k*2.3f;var dir=new Vector3(Mathf.Cos(theta),.35f,Mathf.Sin(theta));
                    bush.Leaf(p,dir,Vector3.Cross(dir,Vector3.up),Range(.12f,.24f),Range(.045f,.075f),Green());
                }
            }
            bush.Save(root,"Wall base shrubs "+id++,foliage);
        }
        id=0;
        foreach(var face in district.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Weathered facade").ToArray())
        {
            var ledges=face.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Floor ledge").ToArray();if(ledges.Length==0)continue;
            float width=ledges.Max(t=>t.localScale.x),height=ledges.Max(t=>t.localPosition.y)+1;
            var ivy=new Leaves();
            for(int patch=0;patch<18;patch++)
            {
                float sign=patch%2==0?-1:1;float cx=width*sign*Range(.31f,.43f),cy=Range(3,Mathf.Max(4,height));
                for(int leaf=0;leaf<45;leaf++)
                {
                    float angle=Range(0,Mathf.PI*2),r=Mathf.Sqrt(Range(0,1));
                    var p=face.TransformPoint(new Vector3(cx+Mathf.Cos(angle)*r*.65f,cy+Mathf.Sin(angle)*r*.9f,-Range(.20f,.31f)));
                    var d=face.TransformDirection(new Vector3(Range(-.8f,.8f),Range(.2f,1),-.05f));
                    var side=Vector3.Cross(face.forward,d).normalized;
                    ivy.Leaf(p,d,side,Range(.15f,.29f),Range(.06f,.115f),Green());
                }
            }
            ivy.Save(root,"Layered facade ivy "+id++,foliage);
        }
        foreach(var material in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}).Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))))
        {
            string name=material.name.ToLowerInvariant();
            if(name.Contains("asphalt")&&material.HasProperty("_Wetness")){material.SetColor("_Color",new Color(.57f,.60f,.55f));material.SetFloat("_Wetness",.42f);material.SetFloat("_Moss",.88f);}
            if(name.Contains("puddle")||name.Contains("rainwater"))PolishWater(material);
            EditorUtility.SetDirty(material);
        }
        foreach(var entry in transforms)if(entry.Key==null||Matrix4x4.TRS(entry.Key.localPosition,entry.Key.localRotation,entry.Key.localScale)!=entry.Value)throw new Exception("Existing layout changed.");
        foreach(var entry in colliders)if(entry.Key==null||EditorJsonUtility.ToJson(entry.Key)!=entry.Value)throw new Exception("Existing collider changed.");
        if(district.GetComponentsInChildren<Collider>(true).Length!=colliders.Count||district.GetComponent<NavMeshSurface>().navMeshData!=nav)throw new Exception("Navigation/physics changed.");
        ExistingCityEnhancer.ValidateNavigation();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StealthReferenceLayout.txt","PASS: "+transforms.Count+" existing environment transforms unchanged; "+colliders.Count+" collider definitions unchanged; same navigation data.\nAdditional foliage triangles="+root.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3));
        ReclaimedCityPresentation.Capture("StealthAfter",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        ReclaimedCityPresentation.Capture("StealthStreet",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
    }
    private static void PolishWater(Material material)
    {
        // The puddle uses the same photographic asphalt as the road, so keep its base tint.
        material.SetColor("_Color",new Color(.57f,.60f,.55f));
        if(material.HasProperty("_Wetness"))material.SetFloat("_Wetness",.55f);
        if(material.HasProperty("_SmoothnessFloor"))material.SetFloat("_SmoothnessFloor",.68f);
        if(material.HasProperty("_NormalStrength"))material.SetFloat("_NormalStrength",.2f);
        if(material.HasProperty("_Moss"))material.SetFloat("_Moss",.88f);
        EditorUtility.SetDirty(material);
    }
    public static void RefineWater()
    {
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{ReclaimedCityPresentation.Root+"/Materials"}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            string name=material.name.ToLowerInvariant();if(name.Contains("puddle")||name.Contains("rainwater"))PolishWater(material);
        }
        AssetDatabase.SaveAssets();
        ReclaimedCityPresentation.Capture("StealthAfter",new Vector3(.45f,2.7f,-23),new Vector3(0,3,12),60);
        ReclaimedCityPresentation.Capture("StealthStreet",new Vector3(-2.3f,1.9f,-8),new Vector3(-9,4,14),62);
    }
    private static void Fern(Leaves mesh,Vector3 p,float height)
    {
        for(int frond=0;frond<7;frond++)
        {
            float angle=frond*Mathf.PI*2/7+Range(-.15f,.15f);var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var side=Vector3.Cross(radial,Vector3.up);
            for(int k=1;k<=8;k++)
            {
                float u=k/9f;var origin=p+Vector3.up*(Mathf.Sin(u*Mathf.PI*.75f)*height)+radial*(u*height*1.15f);
                float length=Mathf.Sin(u*Mathf.PI)*height*.32f;
                foreach(float sign in new[]{-1f,1f})mesh.Leaf(origin,side*sign+radial*.3f+Vector3.up*.12f,radial,length,length*.18f,Green());
            }
        }
    }
}
