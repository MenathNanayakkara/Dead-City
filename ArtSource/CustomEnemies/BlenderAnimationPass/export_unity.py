"""Export the approved animated sources without embedding duplicate PBR images."""
from pathlib import Path
import bpy

root = Path(__file__).resolve().parent
out = root / 'UnityExports'
out.mkdir(exist_ok=True)
for kind in ('Hunter', 'Runner', 'Ambusher', 'Elite'):
    bpy.ops.wm.open_mainfile(filepath=str(root / 'Blender' / (kind + '_Animated.blend')))
    rig = bpy.data.objects[kind + '_Rig']
    mesh = bpy.data.objects[kind + '_CustomMesh']
    # Remove only image nodes in memory; saved .blend sources remain untouched.
    for material in bpy.data.materials:
        if material.use_nodes:
            for node in list(material.node_tree.nodes):
                if node.type == 'TEX_IMAGE':
                    material.node_tree.nodes.remove(node)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(out / (kind + '_Animated.fbx')), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_anim=True,
        bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0, path_mode='STRIP', embed_textures=False)
    print('UNITY_EXPORT_COMPLETE ' + kind, flush=True)
