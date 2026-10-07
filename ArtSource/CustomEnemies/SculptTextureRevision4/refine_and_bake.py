"""Editable mesh refinement and native Blender texture baking.

Original meshes, skeletons, and animation actions are preserved in Revision3.
Only this revision directory is written. No Unity assets are changed.
"""
import bpy
import bmesh
import sys
import math
import json
import hashlib
import random
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
PREV = ROOT.parent / "ReferenceRevision3"
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(PREV))
from pbr_bake import MAPS, add_active_target, configure_cycles, create_image, save_and_pack
from revise_models import clear_pose, a_pose, set_camera, make_material

for folder in ("Blender", "Textures", "Sources", "Previews", "Evidence"):
    (ROOT / folder).mkdir(exist_ok=True)


def smooth(a, b, x):
    t = max(0., min(1., (x-a)/(b-a)))
    return t*t*(3-2*t)


def gauss(x, z, cx, cz, rx, rz):
    return math.exp(-((x-cx)/rx)**2-((z-cz)/rz)**2)


def mesh_part(name, verts, faces, material, bone):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.materials.append(material)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = RIG
    obj.vertex_groups.new(name=bone).add(list(range(len(verts))), 1., "REPLACE")
    for p in data.polygons:
        p.use_smooth = True
    PARTS.append(obj)
    return obj


def tube(name, points, radius, mat, bone, sides=7):
    verts, faces = [], []
    for i, p in enumerate(points):
        p = Vector(p)
        tangent = Vector(points[min(i+1, len(points)-1)])-Vector(points[max(0, i-1)])
        tangent.normalize()
        axis = tangent.cross(Vector((0, 0, 1)))
        if axis.length < .01:
            axis = tangent.cross(Vector((0, 1, 0)))
        axis.normalize()
        other = tangent.cross(axis).normalized()
        for j in range(sides):
            v = p+radius*(axis*math.cos(j*math.tau/sides)+other*math.sin(j*math.tau/sides))
            verts.append(v)
        if i:
            for j in range(sides):
                a=(i-1)*sides+j; b=(i-1)*sides+(j+1)%sides
                faces.append((a,b,b+sides,a+sides))
    faces.extend([tuple(reversed(range(sides))), tuple(range(len(verts)-sides,len(verts)))])
    return mesh_part(name, verts, faces, mat, bone)


def ellipsoid(name, center, scale, mat, bone, rings=10, sides=16):
    verts, faces=[], []
    for i in range(rings+1):
        phi=math.pi*(.001+(1-.002)*i/rings)
        for j in range(sides):
            a=math.tau*j/sides
            verts.append((center[0]+scale[0]*math.sin(phi)*math.cos(a),
                          center[1]+scale[1]*math.sin(phi)*math.sin(a),
                          center[2]+scale[2]*math.cos(phi)))
            if i:
                k=i*sides+j; n=i*sides+(j+1)%sides
                faces.append((k-sides,n-sides,n,k))
    faces.extend([tuple(reversed(range(sides))),tuple(range(rings*sides,(rings+1)*sides))])
    return mesh_part(name,verts,faces,mat,bone)


def components(mesh):
    parents=list(range(len(mesh.vertices)))
    def root(i):
        while parents[i]!=i:
            parents[i]=parents[parents[i]];i=parents[i]
        return i
    for edge in mesh.edges:
        a,b=map(root,edge.vertices)
        if a!=b:parents[b]=a
    groups={}
    for i in range(len(parents)):
        groups.setdefault(root(i),[]).append(i)
    return list(groups.values())


def remove_old_parts(mesh, kind, matrix):
    positions=[matrix@v.co for v in mesh.data.vertices]
    to_remove=set()
    removed=[]
    old_hair=set()
    if kind in ("Hunter","Runner"):
        for poly in mesh.data.polygons:
            if "R3_Hair" in mesh.data.materials[poly.material_index].name:
                old_hair.update(poly.vertices)
    for group in components(mesh.data):
        lo=Vector(tuple(min(positions[i][axis] for i in group) for axis in range(3)))
        hi=Vector(tuple(max(positions[i][axis] for i in group) for axis in range(3)))
        # Each old shoe/sole/lace is a separate component; never cut the shin.
        shoe=hi.z<.235 and lo.z<.18
        plate=kind=="Elite" and lo.z>1.03 and hi.z<1.57 and hi.x-lo.x>.29 and hi.z-lo.z>.20
        hair=bool(old_hair) and all(i in old_hair for i in group)
        if shoe or plate or hair:
            to_remove.update(group)
            removed.append(dict(vertices=len(group),bounds=[list(lo),list(hi)],type="footwear" if shoe else "hair" if hair else "armour"))
    bm=bmesh.new();bm.from_mesh(mesh.data);bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[bm.verts[i] for i in sorted(to_remove)],context="VERTS")
    bm.to_mesh(mesh.data);bm.free();mesh.data.update()
    return removed


def short_hair(kind):
    if kind not in ("Hunter","Runner"):return
    hair=make_material(ROOT,kind+"_R4_short_matted_hair",(.018,.014,.010),.86,0,"cloth")
    # Close-fitting scalp cap and fine short strands, bound to the head.
    verts,faces=[],[]
    for k in range(15):
        phi=.015+k/14*1.13
        for j in range(48):
            a=j*math.tau/48
            verts.append((.09*math.sin(phi)*math.cos(a),.009+.076*math.sin(phi)*math.sin(a),
                          1.802+.098*math.cos(phi)))
            if k:
                i=k*48+j;nj=k*48+(j+1)%48
                faces.append((i-48,nj-48,nj,i))
    # Keep the real head surface visible between sparse strands. A solid dark
    # cap would create an artificial helmet edge in close-up renders.
    rng=random.Random(802 if kind=="Hunter" else 909)
    for i in range(420):
        a=rng.uniform(0,math.tau);phi=rng.uniform(.08,1.27)
        root=Vector((.091*math.sin(phi)*math.cos(a),.009+.078*math.sin(phi)*math.sin(a),
                     1.802+.099*math.cos(phi)))
        outward=Vector((math.cos(a),math.sin(a),.4)).normalized()
        length=rng.uniform(.009,.025) if kind=="Hunter" else rng.uniform(.006,.018)
        points=[root,root+Vector((0,0,length*.55))+outward*.003,
                root+outward*length*.55+Vector((0,0,length*.48)),
                root+outward*length*.9+Vector((0,0,length*.22))]
        tube("Fine matted hair strand",points,rng.uniform(.0003,.00065),hair,"Head",5)


def footwear(kind, width):
    leather=make_material(ROOT,kind+"_R4_boot_upper",(.055,.046,.033),.75,0,"cloth")
    rubber=make_material(ROOT,kind+"_R4_worn_rubber",(.024,.027,.025),.94,0,"cloth")
    stitch=make_material(ROOT,kind+"_R4_dirty_stitch",(.18,.145,.09),.86,0,"cloth")
    lace=make_material(ROOT,kind+"_R4_boot_lace",(.095,.082,.056),.93,0,"cloth")
    first_part=len(PARTS)
    for side,s in (("Left",1),("Right",-1)):
        bone=side+"Foot";cx=s*.105*width
        stations=[(-.227,.011,.038,.065),(-.216,.039,.032,.094),
                  (-.193,.058,.031,.111),(-.15,.065,.032,.129),
                  (-.09,.064,.033,.151),(-.03,.057,.035,.174),
                  (.025,.052,.038,.165),(.062,.048,.039,.142),
                  (.078,.025,.041,.12),(.080,.008,.048,.10)]
        verts,faces=[],[]
        n=24
        for i,(y,w,bottom,top) in enumerate(stations):
            for j in range(n):
                a=math.tau*j/n
                # Squared-off leather cross-section, rounded seams.
                x=math.copysign(abs(math.cos(a))**.72,math.cos(a))
                z=math.copysign(abs(math.sin(a))**.72,math.sin(a))
                verts.append((cx+w*x,y,(top+bottom)/2+(top-bottom)/2*z))
                if i:
                    faces.append(((i-1)*n+j,(i-1)*n+(j+1)%n,i*n+(j+1)%n,i*n+j))
        faces.extend([tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))])
        mesh_part(side+" shaped leather vamp",verts,faces,leather,bone)
        # Separate thin sole, following the shoe footprint.
        verts,faces=[],[]
        outline=[(cx+w,y) for y,w,_,_ in stations]+[(cx-w,y) for y,w,_,_ in reversed(stations)]
        count=len(outline)
        for z in (.007,.015,.032,.037):
            factor=.99 if z in (.007,.037) else 1.04
            verts += [(cx+(x-cx)*factor,y,z) for x,y in outline]
        for k in range(3):
            for j in range(count):
                faces.append((k*count+j,k*count+(j+1)%count,(k+1)*count+(j+1)%count,(k+1)*count+j))
        faces.extend([tuple(reversed(range(count))),tuple(range(count*3,count*4))])
        mesh_part(side+" thin welt and outsole",verts,faces,rubber,bone)
        tube(side+" welt stitching",[(x,y,.037) for x,y in outline+[outline[0]]],.0012,stitch,bone)
        # Open cuff surrounds the ankle instead of a closed egg-shaped upper.
        verts,faces=[],[]
        cuff_top=.143 if kind=="Runner" else .215
        for k,(z,rx,ry) in enumerate(((.105,.050,.065),(cuff_top,.048,.047),
                                     (cuff_top+.002,.041,.040),(.11,.042,.055))):
            for j in range(32):
                a=math.tau*j/32
                verts.append((cx+rx*math.cos(a),.014+ry*math.sin(a),z))
                if k:
                    faces.append(((k-1)*32+j,(k-1)*32+(j+1)%32,k*32+(j+1)%32,k*32+j))
        mesh_part(side+" open ankle collar",verts,faces,leather,bone)
        tube(side+" cuff seam",[(cx+.049*math.cos(j*math.tau/40),.014+.048*math.sin(j*math.tau/40),cuff_top) for j in range(41)],.0015,stitch,bone)
        for k in range(6):
            y=-.117+k*.023
            z=.150+k*.007 if k<4 else .174+(k-4)*.008
            tube(side+" crossed lace",[(cx-.025,y,z),(cx+.025,y+.016,z+.006)],.0022,lace,bone)
            tube(side+" crossed lace",[(cx+.025,y,z),(cx-.025,y+.016,z+.006)],.0022,lace,bone)
        for s2 in (-1,1):
            tube(side+" stitched quarter",[(cx+s2*.048,-.16,.098),(cx+s2*.060,-.08,.115),
                                          (cx+s2*.052,.015,.143),(cx+s2*.044,.045,cuff_top-.01)],.0012,stitch,bone)
        for k in range(7):
            y=-.18+k*.032
            tube(side+" shallow tread",[(cx-.043,y,.008),(cx+.043,y,.008)],.0025,rubber,bone)
        tube(side+" toe cap seam",[(cx+.058*math.cos(a),-.153,.079+.048*math.sin(a))
                                  for a in np.linspace(0,math.pi,17)],.0014,stitch,bone)
    # A low, fitted shoe silhouette; keep heel aligned with the original foot.
    for ob in PARTS[first_part:]:
        for v in ob.data.vertices:
            cx=(1 if v.co.x>0 else -1)*.105*width
            v.co.x=cx+(v.co.x-cx)*.89
            v.co.y=.025+(v.co.y-.025)*.89
            v.co.z*=.92


def curved_armour(width):
    metal=make_material(ROOT,"Elite_R4_curved_oxidized_plate",(.075,.085,.075),.58,.63,"metal")
    trim=make_material(ROOT,"Elite_R4_plate_edge",(.10,.085,.055),.73,.38,"metal")
    for sign in (-1,1):
        verts,faces=[],[]
        for k in range(13):
            t=k/12;z=1.13+t*.32
            half=.176+.027*math.sin(t*math.pi)
            for j in range(19):
                u=j/18*2-1
                y=sign*(.121+.031*(1-u*u))
                verts.append((u*half,y,z-.014*u*u))
                if k and j:
                    i=k*19+j;faces.append((i-20,i-19,i,i-1))
        ob=mesh_part("Curved chest plate" if sign<0 else "Curved back plate",verts,faces,metal,"Chest")
        bpy.context.view_layer.objects.active=ob;ob.select_set(True)
        mod=ob.modifiers.new("Real plate thickness","SOLIDIFY");mod.thickness=.008
        bpy.ops.object.modifier_apply(modifier=mod.name);ob.select_set(False)
        for edge in (0,12):
            points=[verts[edge*19+j] for j in range(19)]
            tube("Raised plate rim",points,.003,trim,"Chest")
        for x in (-.158,.158):
            for z in (1.18,1.40):
                ellipsoid("Plate rivet",(x,sign*.151,z),(.006,.004,.006),trim,"Chest",6,10)


def sculpt(mesh, matrix, kind, width):
    inverse=matrix.inverted()
    normals=[(matrix.to_3x3()@v.normal).normalized() for v in mesh.data.vertices]
    positions=[matrix@v.co for v in mesh.data.vertices]
    skin=set();cloth=set()
    for poly in mesh.data.polygons:
        name=mesh.data.materials[poly.material_index].name
        if "Infected_" in name:
            skin.update(poly.vertices)
        if any(t in name for t in ("workwear","cotton","canvas","denim","Industrial_orange")):
            cloth.update(poly.vertices)
    moved=0
    for i,v in enumerate(mesh.data.vertices):
        p=positions[i].copy();n=normals[i]
        if i in skin and p.z>1.60 and abs(p.x)<.14:
            front=smooth(.005,.07,-p.y)
            depth=0.
            for s in (-1,1):
                depth += .006*gauss(p.x,p.z,s*.038,1.766,.024,.016) # orbit
                depth -= .009*gauss(p.x,p.z,s*.039,1.792,.035,.014) # brow ridge
                depth -= .006*gauss(p.x,p.z,s*.062,1.719,.025,.022) # cheekbone
                depth += .004*gauss(p.x,p.z,s*.064,1.684,.025,.021) # hollow cheeks
                depth -= .003*gauss(p.x,p.z,s*.022,1.692,.008,.022) # nasolabial
            depth += .006*gauss(p.x,p.z,0,1.674,.035,.012)
            p.y += front*depth
            if kind=="Elite":
                p.x *= 1+.17*smooth(1.61,1.65,p.z)*(1-smooth(1.73,1.83,p.z))
            if p.z>1.79:
                p.z -= .014*smooth(1.79,1.91,p.z)
            moved+=1
        if i in cloth:
            amplitude=0.
            if p.z<1.03:
                knee=math.exp(-((p.z-.55)/.13)**2)
                hem=math.exp(-((p.z-.20)/.08)**2)
                waist=math.exp(-((p.z-.92)/.08)**2)
                amplitude=.0038*math.sin(p.z*99+abs(p.x)*19+p.y*11)*knee
                amplitude+=.0024*math.sin(p.z*115+p.x*23)*hem
                amplitude+=.0025*math.sin(p.z*94+p.y*27)*waist
            elif abs(p.x)>.205*width:
                elbow=.205*width+.255
                amplitude=.0033*math.sin(abs(p.x)*115+p.z*12)*math.exp(-((abs(p.x)-elbow)/.09)**2)
            else:
                amplitude=.0022*math.sin(p.z*83+p.x*22)*math.exp(-((p.z-1.08)/.11)**2)
            p+=n*amplitude
            moved+=1
        v.co=inverse@p
    mesh.data.update()
    return moved


def detailed_materials(mesh, kind):
    # Procedural microstructure remains editable in the material-source file.
    for mat in mesh.data.materials:
        if not mat or not mat.use_nodes:continue
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        bs=nodes.get("Principled BSDF")
        if not bs:continue
        name=mat.name.lower()
        skin="infected" in name or ("handdetail" in name and kind!="Elite")
        cloth=any(k in name for k in ("workwear","cotton","canvas","denim","industrial","undershirt"))
        if not (skin or cloth):continue
        if skin and bs.inputs["Base Color"].is_linked:
            original=bs.inputs["Base Color"].links[0].from_socket
            hsv=nodes.new("ShaderNodeSeparateColor");hsv.mode="HSV"
            links.new(original,hsv.inputs[0])
            sat=nodes.new("ShaderNodeMath");sat.operation="LESS_THAN";sat.inputs[1].default_value=.24
            val=nodes.new("ShaderNodeMath");val.operation="GREATER_THAN";val.inputs[1].default_value=.30
            mask=nodes.new("ShaderNodeMath");mask.operation="MULTIPLY"
            links.new(hsv.outputs[1],sat.inputs[0]);links.new(hsv.outputs[2],val.inputs[0])
            links.new(sat.outputs[0],mask.inputs[0]);links.new(val.outputs[0],mask.inputs[1])
            safe=nodes.new("ShaderNodeMixRGB")
            links.new(mask.outputs[0],safe.inputs[0]);links.new(original,safe.inputs[1])
            safe.inputs[2].default_value=(.30,.22,.16,1)
            links.new(safe.outputs[0],bs.inputs["Base Color"])
        tex=nodes.new("ShaderNodeTexNoise");tex.name="R4 pores and weave"
        tex.inputs["Scale"].default_value=410 if skin else 300
        tex.inputs["Detail"].default_value=2
        bump=nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value=.24 if skin else .29
        bump.inputs["Distance"].default_value=.00045 if skin else .0007
        if bs.inputs["Normal"].is_linked:
            links.new(bs.inputs["Normal"].links[0].from_socket,bump.inputs["Normal"])
        links.new(tex.outputs["Fac"],bump.inputs["Height"]);links.new(bump.outputs[0],bs.inputs["Normal"])
        ramp=nodes.new("ShaderNodeMapRange")
        ramp.inputs["From Min"].default_value=0;ramp.inputs["From Max"].default_value=1
        ramp.inputs["To Min"].default_value=.48 if skin else .73
        ramp.inputs["To Max"].default_value=.82 if skin else .96
        links.new(tex.outputs["Fac"],ramp.inputs[0]);links.new(ramp.outputs[0],bs.inputs["Roughness"])


def face_texture(mesh, kind, matrix):
    path=ROOT/"Sources"/(kind+"_Face.png")
    if not path.exists():
        raise RuntimeError("Missing face texture source: "+str(path))
    image=bpy.data.images.load(str(path),check_existing=True);image.pack()
    uv=mesh.data.uv_layers.get("FaceDetail") or mesh.data.uv_layers.new(name="FaceDetail")
    mask=mesh.data.color_attributes.new(name="FaceDetailBlend",type="FLOAT_COLOR",domain="CORNER")
    pos=[matrix@v.co for v in mesh.data.vertices]
    # A separate continuous head material removes background patches from the
    # old four-view projections. It also gives ears the same skin treatment.
    headmat=next(m for m in mesh.data.materials
                 if "Infected_" in m.name and "_R4_head" not in m.name).copy()
    headmat.name=kind+"_Infected_R4_head"
    mesh.data.materials.append(headmat);headslot=len(mesh.data.materials)-1
    for poly in mesh.data.polygons:
        oldname=mesh.data.materials[poly.material_index].name
        if all(pos[i].z>1.595 for i in poly.vertices) and any(k in oldname for k in ("Infected_","03_","04_","05_")):
            poly.material_index=headslot
    uv_values=[];colors=[]
    for p in pos:
        px=p.x/(1.12 if kind=="Elite" else 1.)
        eye_y={"Hunter":.395,"Runner":.415,"Ambusher":.402,"Elite":.419}[kind]
        py=float(np.interp(p.z,[1.57,1.61,1.674,1.766,1.896],[1,.88,.655,eye_y,.035]))
        xscale=float(np.interp(p.z,[1.61,1.68,1.766,1.90],[2.25,2.5,3.0,3.0]))
        uv_values.append((.5+px*xscale,1-py))
        f=smooth(.005,.055,-p.y)*smooth(1.57,1.63,p.z)
        colors.append((f,f,f,smooth(1.60,1.70,p.z)))
    ids=np.empty(len(mesh.data.loops),dtype=np.int32)
    mesh.data.loops.foreach_get("vertex_index",ids)
    uv.data.foreach_set("uv",np.asarray(uv_values,np.float32)[ids].ravel())
    mask.data.foreach_set("color",np.asarray(colors,np.float32)[ids].ravel())
    for mat in mesh.data.materials:
        if mat!=headmat:continue
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        bs=nodes.get("Principled BSDF")
        old=bs.inputs["Base Color"].links[0].from_socket if bs.inputs["Base Color"].is_linked else None
        coord=nodes.new("ShaderNodeUVMap");coord.uv_map=uv.name
        tex=nodes.new("ShaderNodeTexImage");tex.image=image;tex.extension="EXTEND"
        links.new(coord.outputs[0],tex.inputs[0])
        attr=nodes.new("ShaderNodeVertexColor");attr.layer_name=mask.name
        mix=nodes.new("ShaderNodeMixRGB");links.new(attr.outputs[0],mix.inputs[0])
        headbase=nodes.new("ShaderNodeMixRGB")
        links.new(attr.outputs["Alpha"],headbase.inputs[0])
        if old:links.new(old,headbase.inputs[1])
        else:headbase.inputs[1].default_value=bs.inputs["Base Color"].default_value
        mottling=nodes.new("ShaderNodeTexNoise");mottling.inputs["Scale"].default_value=48
        ramp=nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color=(.21,.15,.115,1)
        ramp.color_ramp.elements[1].color=(.39,.30,.235,1)
        links.new(mottling.outputs["Fac"],ramp.inputs[0])
        links.new(ramp.outputs[0],headbase.inputs[2]);links.new(headbase.outputs[0],mix.inputs[1])
        # Exclude the neutral backdrop around the narrower jaw in the source.
        hsv=nodes.new("ShaderNodeSeparateColor");hsv.mode="HSV"
        links.new(tex.outputs[0],hsv.inputs[0])
        is_skin=nodes.new("ShaderNodeMapRange")
        is_skin.inputs["From Min"].default_value=.10
        is_skin.inputs["From Max"].default_value=.24
        links.new(hsv.outputs[1],is_skin.inputs[0])
        factor=nodes.new("ShaderNodeMath");factor.operation="MULTIPLY"
        links.new(attr.outputs[0],factor.inputs[0]);links.new(is_skin.outputs[0],factor.inputs[1])
        links.new(factor.outputs[0],mix.inputs[0])
        links.new(tex.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],bs.inputs["Base Color"])
        # Pores and small-scale mottling remain procedural in the editable source.
        noise=nodes.new("ShaderNodeTexNoise");noise.inputs["Scale"].default_value=320
        noise.inputs["Detail"].default_value=3
        bump=nodes.new("ShaderNodeBump");bump.inputs["Strength"].default_value=.22
        bump.inputs["Distance"].default_value=.00055
        links.new(noise.outputs["Fac"],bump.inputs["Height"]);links.new(bump.outputs[0],bs.inputs["Normal"])


def eye_surfaces(mesh):
    skin=next(m for m in mesh.data.materials if "Infected_" in m.name)
    matrix=RIG.matrix_world.inverted()@mesh.matrix_world
    head_points=[matrix@v.co for v in mesh.data.vertices]
    for s in (-1,1):
        candidates=[p for p in head_points if p.y<-.025 and abs(p.x-s*.038)<.022 and abs(p.z-1.766)<.012]
        nearest=sorted(candidates,key=lambda p:(p.x-s*.038)**2+(p.z-1.766)**2)[:6]
        surface_y=sum(p.y for p in nearest)/len(nearest) if nearest else -.08
        ellipsoid("Sculpted eye surface",(s*.038,surface_y+.002,1.766),(.016,.008,.007),skin,"Head",10,20)
        for upper in (True,False):
            points=[]
            for a in np.linspace(0,math.pi,15):
                points.append((s*.038+.016*math.cos(a),surface_y-.002-.001*math.sin(a),
                               1.766+(1 if upper else -1)*.007*math.sin(a)))
            tube("Upper eyelid" if upper else "Lower eyelid",points,.0008,skin,"Head",6)


def join_parts(mesh):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in PARTS:ob.select_set(True)
    mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
    bpy.ops.object.join()
    for polygon in mesh.data.polygons:polygon.use_smooth=True


def bake_atlas(mesh, kind):
    bpy.ops.object.select_all(action="DESELECT");mesh.select_set(True)
    bpy.context.view_layer.objects.active=mesh
    uv=mesh.data.uv_layers.get("AtlasUV") or mesh.data.uv_layers.new(name="AtlasUV")
    mesh.data.uv_layers.active=uv;uv.active_render=True
    bpy.ops.object.mode_set(mode="EDIT");bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.004,
                             area_weight=.6,correct_aspect=True,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    scene=bpy.context.scene
    configure_cycles(scene)
    mats=list(set(m for m in mesh.data.materials if m))
    images={}
    for bake_map in MAPS:
        name=bake_map.name
        image=create_image(kind,bake_map)
        restores=[]
        for mat in mats:
            nodes,links=mat.node_tree.nodes,mat.node_tree.links
            bs=nodes.get("Principled BSDF")
            out=next(n for n in nodes if n.type=="OUTPUT_MATERIAL" and n.is_active_output)
            target,uvnode=add_active_target(mat,image,"AtlasUV")
            original=out.inputs["Surface"].links[0].from_socket
            emission=None
            if name!="Normal":
                emission=nodes.new("ShaderNodeEmission")
                inp=bs.inputs[bake_map.principled_socket]
                if inp.is_linked:links.new(inp.links[0].from_socket,emission.inputs[0])
                else:
                    val=inp.default_value
                    emission.inputs[0].default_value=tuple(val) if name=="BaseColor" else (val,val,val,1)
                links.new(emission.outputs[0],out.inputs["Surface"])
            restores.append((mat,out,original,target,uvnode,emission))
        print("BAKE",kind,name,flush=True)
        bpy.ops.object.bake(type=bake_map.bake_type)
        output=ROOT/"Textures"/(kind+"_"+name+"_"+bake_map.resolution_suffix+".png")
        save_and_pack(image,output);images[name]=image
        for mat,out,original,target,uvnode,emission in restores:
            nodes,links=mat.node_tree.nodes,mat.node_tree.links
            links.new(original,out.inputs["Surface"])
            for n in (target,uvnode,emission):
                if n:nodes.remove(n)
    # Save the actual editable material graph and UV bake sources.
    for mod in mesh.modifiers:
        if mod.type=="ARMATURE":mod.show_render=True;mod.show_viewport=True
    a_pose(RIG)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Blender"/(kind+"_Sculpt4_EditableMaterials.blend")))
    atlas=bpy.data.materials.new(kind+"_Sculpt4_PBR_Atlas");atlas.use_nodes=True
    nodes,links=atlas.node_tree.nodes,atlas.node_tree.links;bs=nodes.get("Principled BSDF")
    coord=nodes.new("ShaderNodeUVMap");coord.uv_map="AtlasUV"
    for name,img in images.items():
        tex=nodes.new("ShaderNodeTexImage");tex.image=img;tex.label=name
        links.new(coord.outputs[0],tex.inputs[0])
        if name=="Normal":
            normal=nodes.new("ShaderNodeNormalMap");normal.uv_map="AtlasUV"
            links.new(tex.outputs[0],normal.inputs["Color"]);links.new(normal.outputs[0],bs.inputs["Normal"])
        else:
            links.new(tex.outputs[0],bs.inputs[{"BaseColor":"Base Color","Roughness":"Roughness","Metallic":"Metallic"}[name]])
    mesh.data.materials.clear();mesh.data.materials.append(atlas)
    for poly in mesh.data.polygons:poly.material_index=0
    return [i.filepath_raw for i in images.values()]


def main(kind):
    global RIG,PARTS
    PARTS=[];random.seed(241020)
    source=PREV/"Blender"/(kind+"_ReferenceRevision3.blend")
    source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source))
    bpy.context.preferences.filepaths.save_version=0
    RIG=bpy.data.objects[kind+"_Rig"];mesh=bpy.data.objects[kind+"_CustomMesh"]
    clear_pose(RIG);bpy.context.view_layer.update()
    matrix=RIG.matrix_world.inverted()@mesh.matrix_world
    width={"Hunter":1.03,"Runner":.82,"Ambusher":.91,"Elite":1.30}[kind]
    removed=remove_old_parts(mesh,kind,matrix)
    moved=sculpt(mesh,matrix,kind,width)
    footwear(kind,width)
    eye_surfaces(mesh)
    short_hair(kind)
    if kind=="Elite":curved_armour(width)
    join_parts(mesh)
    detailed_materials(mesh,kind)
    face_texture(mesh,kind,matrix)
    scene=bpy.context.scene;cam=scene.camera
    scene.render.resolution_x=1000;scene.render.resolution_y=1200
    scene.cycles.samples=24
    set_camera(cam,(2.4,-6,2.3),(0,0,1.04),2.30 if kind!="Elite" else 2.49)
    RIG["revision"]="SculptTextureRevision4"
    RIG["provenance"]="AI-assisted mesh refinement; generated face references; native Blender UV baking."
    RIG["review_status"]="Reviewable art revision. Existing actions retained; Unity integration not performed."
    mesh["reference_texture_note"]="4K colour and 2K roughness, metallic and material-detail tangent normal atlases. Clothing still derives from reference-sheet projections and retains some painted shading."
    # Bake with the armature in rest position; pose only after the bake.
    for mod in mesh.modifiers:
        if mod.type=="ARMATURE":mod.show_render=False;mod.show_viewport=False
    textures=bake_atlas(mesh,kind)
    for mod in mesh.modifiers:
        if mod.type=="ARMATURE":mod.show_render=True;mod.show_viewport=True
    # Ensure editable-source copy also opens with deformation enabled.
    editable=ROOT/"Blender"/(kind+"_Sculpt4_EditableMaterials.blend")
    a_pose(RIG)
    scene.cycles.samples=24
    out=ROOT/"Blender"/(kind+"_Sculpt4.blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(out))
    scene.render.filepath=str(ROOT/"Previews"/(kind+"_Actual_Blender.png"))
    bpy.ops.render.render(write_still=True)
    set_camera(cam,(.45,-4,1.83),(0,0,1.70 if kind!="Elite" else 1.82),.65)
    scene.render.resolution_x=1000;scene.render.resolution_y=1000
    scene.render.filepath=str(ROOT/"Previews"/(kind+"_Face_Actual_Blender.png"))
    bpy.ops.render.render(write_still=True)
    mesh.data.calc_loop_triangles()
    report=dict(character=kind,source=str(source),source_sha256=source_hash,
                source_unchanged=hashlib.sha256(source.read_bytes()).hexdigest()==source_hash,
                vertices=len(mesh.data.vertices),triangles=len(mesh.data.loop_triangles),
                bones=len(RIG.data.bones),actions=[a.name for a in bpy.data.actions if a.name.startswith(kind+"_")],
                sculpted_vertices=moved,replaced_components=removed,textures=textures,
                unweighted_vertices=sum(not v.groups for v in mesh.data.vertices),
                weight_sum_errors=sum(abs(sum(g.weight for g in v.groups)-1)>.002 for v in mesh.data.vertices),
                output=str(out),output_sha256=hashlib.sha256(out.read_bytes()).hexdigest())
    (ROOT/"Evidence"/(kind+".json")).write_text(json.dumps(report,indent=2))
    print("COMPLETE",kind,flush=True)


if __name__=="__main__":
    main(sys.argv[sys.argv.index("--")+1])
