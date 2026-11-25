using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Small native mesh helpers for decorative architecture. No gameplay components.</summary>
public static class CityArtGeometry
{
    public static Transform Group(string name, Transform parent, Vector3 position = default, float yaw = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0,yaw,0);
        return go.transform;
    }
    public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 rotation = default)
        => Primitive(PrimitiveType.Cube,name,parent,position,size,material,rotation);
    public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 rotation = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(rotation);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
    public static void Beam(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
    {
        var go = Box(name,parent,(from+to)*.5f,new Vector3(width,width,Vector3.Distance(from,to)),material);
        go.transform.localRotation = Quaternion.LookRotation((to-from).normalized);
    }
    public static GameObject MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var uv = new Vector2[vertices.Count];
        for(int i=0;i<uv.Length;i++) uv[i] = new Vector2(vertices[i].x,vertices[i].z);
        mesh.uv = uv;
        mesh.RecalculateTangents();
        string path = AssetDatabase.GenerateUniqueAssetPath(CityArtAssets.Root + "/Meshes/" + name + ".asset");
        AssetDatabase.CreateAsset(mesh,path);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }
    public static void Puddle(Transform parent, Vector3 position, float radius, Material material, System.Random random)
    {
        var vertices = new List<Vector3> { Vector3.zero };
        var triangles = new List<int>();
        const int sides=18;
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2/sides, r=radius*(.65f+(float)random.NextDouble()*.35f);
            vertices.Add(new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*.55f));
        }
        for(int i=0;i<sides;i++) { triangles.Add(0); triangles.Add((i+1)%sides+1); triangles.Add(i+1); }
        MeshObject("RainPuddle",parent,vertices,triangles,material).transform.localPosition = position;
    }
    public static void Car(Transform parent, Vector3 position, float yaw, Material paint, Material metal, Material glass, Material rubber)
    {
        var car = Group("WreckedSedan",parent,position,yaw);
        var v=new List<Vector3>(); var t=new List<int>();
        float[] z={-2.15f,-1.8f,-1.1f,.9f,1.85f,2.12f};
        float[] widths={.74f,.95f,.96f,.96f,.88f,.73f};
        float[] tops={.64f,.88f,.97f,.94f,.83f,.67f};
        for(int k=0;k<z.Length;k++)
        {
            float w=widths[k],h=tops[k];
            v.AddRange(new[]{ new Vector3(-w+.1f,.33f,z[k]),new Vector3(w-.1f,.33f,z[k]),new Vector3(w,.48f,z[k]),new Vector3(w,h-.1f,z[k]),new Vector3(w-.13f,h,z[k]),new Vector3(-w+.13f,h,z[k]),new Vector3(-w,h-.1f,z[k]),new Vector3(-w,.48f,z[k]) });
        }
        for(int k=0;k<z.Length-1;k++) for(int j=0;j<8;j++)
        { int a=k*8+j,b=k*8+(j+1)%8,c=b+8,d=a+8; t.AddRange(new[]{a,b,c,a,c,d}); }
        for(int j=1;j<7;j++)
        {
            t.AddRange(new[]{0,j+1,j});
            int rear=(z.Length-1)*8;
            t.AddRange(new[]{rear,rear+j,rear+j+1});
        }
        MeshObject("DentedBody",car,v,t,paint);
        v=new List<Vector3> {new Vector3(-.84f,.89f,-1.2f),new Vector3(.84f,.89f,-1.2f),new Vector3(-.65f,1.52f,-.65f),new Vector3(.65f,1.52f,-.65f),new Vector3(-.65f,1.48f,.48f),new Vector3(.65f,1.48f,.48f),new Vector3(-.83f,.9f,1.13f),new Vector3(.83f,.9f,1.13f)};
        t=new List<int>{0,2,3,0,3,1,2,4,5,2,5,3,4,6,7,4,7,5,0,6,4,0,4,2,1,3,5,1,5,7};
        MeshObject("SmashedDarkGlass",car,v,t,glass);
        Box("Roof",car,new Vector3(0,1.51f,-.08f),new Vector3(1.36f,.08f,1.2f),paint);
        foreach(float side in new[]{-1f,1f})
        {
            Beam("Pillar",car,new Vector3(side*.86f,.9f,-1.17f),new Vector3(side*.66f,1.5f,-.65f),.07f,paint);
            Beam("Pillar",car,new Vector3(side*.84f,.9f,1.1f),new Vector3(side*.65f,1.47f,.47f),.075f,paint);
            Beam("DoorPillar",car,new Vector3(side*.88f,.9f,0),new Vector3(side*.67f,1.5f,0),.06f,paint);
            Box("Mirror",car,new Vector3(side*1.04f,.98f,.65f),new Vector3(.22f,.13f,.18f),metal);
            for(int i=0;i<2;i++)
            {
                float wheelZ=i==0?-1.38f:1.35f;
                Primitive(PrimitiveType.Cylinder,"Tyre",car,new Vector3(side*.93f,.39f,wheelZ),new Vector3(.76f,.14f,.76f),rubber,new Vector3(0,0,90));
                Primitive(PrimitiveType.Cylinder,"WheelRim",car,new Vector3(side*1.085f,.39f,wheelZ),new Vector3(.43f,.012f,.43f),metal,new Vector3(0,0,90));
                Box("DoorHandle",car,new Vector3(side*.966f,.79f,i==0?-.8f:.28f),new Vector3(.035f,.04f,.15f),metal);
            }
        }
        foreach(float end in new[]{-1f,1f})
        {
            Box("Bumper",car,new Vector3(0,.45f,end*2.11f),new Vector3(1.65f,.12f,.16f),metal);
            foreach(float side in new[]{-1f,1f}) Box("BrokenLamp",car,new Vector3(side*.54f,.66f,end*2.1f),new Vector3(.36f,.16f,.055f),glass);
        }
    }
}
