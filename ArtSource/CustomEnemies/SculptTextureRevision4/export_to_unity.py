"""Export the approved sculpt revision with the atlas as Unity UV0.

Only exports are written. Blender source files are never saved by this script.
"""
import bpy
import sys
import json
import hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parent
OUT=ROOT/"UnityExports"
OUT.mkdir(exist_ok=True)
reports=json.loads((ROOT/"Evidence"/"review.json").read_text())
exports=[]
for kind in ("Hunter","Runner","Ambusher","Elite"):
    source=ROOT/"Blender"/(kind+"_Sculpt4.blend")
    digest=hashlib.sha256(source.read_bytes()).hexdigest()
    recorded=next(r for r in reports if r["character"]==kind)
    if digest!=recorded["output_sha256"]:
        raise RuntimeError("Reviewed model changed: "+str(source))
    bpy.ops.wm.open_mainfile(filepath=str(source))
    rig=bpy.data.objects[kind+"_Rig"]
    mesh=bpy.data.objects[kind+"_CustomMesh"]
    rig.animation_data_clear()
    rig.location=(0,0,0)
    for bone in rig.pose.bones:
        bone.rotation_mode="QUATERNION"
        bone.rotation_quaternion=(1,0,0,0)
        bone.location=(0,0,0);bone.scale=(1,1,1)
    # FBX/Unity use the first exported UV channel for the Standard shader.
    for layer in list(mesh.data.uv_layers):
        if layer.name!="AtlasUV":mesh.data.uv_layers.remove(layer)
    if len(mesh.data.uv_layers)!=1 or mesh.data.uv_layers[0].name!="AtlasUV":
        raise RuntimeError("Missing atlas UV: "+kind)
    mesh.data.uv_layers.active_index=0
    mesh.data.uv_layers[0].active_render=True
    for attr in list(mesh.data.color_attributes):mesh.data.color_attributes.remove(attr)
    for mat in mesh.data.materials:
        for node in list(mat.node_tree.nodes):
            if node.type=="TEX_IMAGE":mat.node_tree.nodes.remove(node)
    for action in list(bpy.data.actions):
        if not action.name.startswith(kind+"_"):bpy.data.actions.remove(action)
    # Blender's FBX action exporter requires animation data on the armature.
    # The review files intentionally open with no active action.
    rig.animation_data_create()
    rig.animation_data.action=bpy.data.actions[kind+"_Idle"]
    rig.animation_data.action_slot=rig.animation_data.action.slots[0]
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=rig
    path=OUT/(kind+"_Animated.fbx")
    bpy.ops.export_scene.fbx(
        filepath=str(path),use_selection=True,object_types={"ARMATURE","MESH"},
        add_leaf_bones=False,apply_unit_scale=True,apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",axis_up="Y",bake_anim=True,bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
        path_mode="STRIP",embed_textures=False)
    exports.append(dict(character=kind,source=str(source),source_sha256=digest,
                        export=str(path),export_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                        uv0="AtlasUV",materials=[m.name for m in mesh.data.materials],
                        actions=[a.name for a in bpy.data.actions]))
    print("EXPORTED",kind,flush=True)
(OUT/"export-receipt.json").write_text(json.dumps(exports,indent=2))
