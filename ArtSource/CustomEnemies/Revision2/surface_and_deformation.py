"""Revision 2: original PBR maps and continuous, joint-weighted surfaces.
No vendor geometry or texture inputs. Intended for isolated motion review.
"""
import bpy, bmesh, math
import numpy as np
from mathutils import Vector


def make_material(root, name, color, rough, metal, mode):
    size = 1024
    rng = np.random.default_rng(sum(map(ord, name)))
    y, x = np.mgrid[0:size, 0:size] / size

    def noise(grid):
        values = rng.random((grid+1, grid+1))
        values[-1, :] = values[0, :]
        values[:, -1] = values[:, 0]
        px, py = x*grid, y*grid
        ix, iy = px.astype(int), py.astype(int)
        tx, ty = px-ix, py-iy
        tx, ty = tx*tx*(3-2*tx), ty*ty*(3-2*ty)
        return ((values[iy, ix]*(1-tx)+values[iy, ix+1]*tx)*(1-ty)
                +(values[iy+1, ix]*(1-tx)+values[iy+1, ix+1]*tx)*ty)

    broad, medium, fine = noise(6), noise(31), noise(110)
    dust = np.clip((noise(13)-.42)*2, 0, 1)
    grain = rng.normal(0, .012, (size, size))
    rgb = np.array(color)[None, None, :]*(.89+.12*broad[:, :, None]+.08*medium[:, :, None])
    rgb = rgb*(1-dust[:, :, None]*.28)+dust[:, :, None]*np.array([.027, .025, .019])
    height = medium*.3+fine*.13
    rough_map = np.clip(rough+(medium-.5)*.17+dust*.08, .15, .99)
    metal_map = np.full((size, size), metal)
    if mode == 'skin':
        bruise = np.clip((broad-.52)*2.3, 0, .65)
        rgb *= 1-bruise[:, :, None]*np.array([.05, .30, .19])
        height = fine*.075+grain*.6
    elif mode == 'metal':
        rust = np.clip((medium+broad-.95)*2, 0, 1)
        rgb = rgb*(1-rust[:, :, None]*.7)+rust[:, :, None]*np.array([.12, .047, .018])
        metal_map *= 1-rust*.95
        rough_map = np.clip(rough_map+rust*.3, 0, .98)
        height += rust*.2
    else:
        weave = (np.sin(x*math.tau*190)*np.sin(y*math.tau*190))*.045
        height += weave
        rgb += weave[:, :, None]*.035
        # Subtle irregular wear rather than painted-on lighting or large spots.
        scratch = np.maximum(0, np.sin(x*math.tau*47+medium*2)-.98)*dust
        rgb += scratch[:, :, None]*.06
    rgb = np.clip(rgb+grain[:, :, None]*.22, .004, 1)
    dx = (np.roll(height, -1, axis=1)-np.roll(height, 1, axis=1))*1.6
    dy = (np.roll(height, -1, axis=0)-np.roll(height, 1, axis=0))*1.6
    normal = np.stack((-dx, -dy, np.ones_like(dx)), axis=2)
    normal /= np.linalg.norm(normal, axis=2)[:, :, None]
    normal = normal*.5+.5

    def image(suffix, values, data=False):
        pixels = np.ones((size, size, 4), np.float32)
        if values.ndim == 2:
            pixels[:, :, :3] = values[:, :, None]
        else:
            pixels[:, :, :values.shape[2]] = values
        im = bpy.data.images.new(name+'_'+suffix, width=size, height=size, alpha=True)
        if data:
            im.colorspace_settings.name = 'Non-Color'
        im.pixels.foreach_set(pixels.ravel())
        im.filepath_raw = str(root/'Textures'/(name+'_'+suffix+'.png'))
        im.file_format = 'PNG'
        im.save()
        im.pack()
        return im

    base, norm = image('BaseColor', rgb), image('Normal', normal, True)
    roughness = image('Roughness', rough_map, True)
    image('MetallicSmoothness', np.stack((metal_map, np.zeros_like(x), np.zeros_like(x), 1-rough_map), axis=2), True)
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    p = nodes.get('Principled BSDF')
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = rough
    def tex(im):
        t = nodes.new('ShaderNodeTexImage'); t.image = im
        return t
    links.new(tex(base).outputs['Color'], p.inputs['Base Color'])
    links.new(tex(roughness).outputs['Color'], p.inputs['Roughness'])
    normal_node = nodes.new('ShaderNodeNormalMap')
    links.new(tex(norm).outputs['Color'], normal_node.inputs['Color'])
    links.new(normal_node.outputs['Normal'], p.inputs['Normal'])
    if metal:
        metallic = image('Metallic', metal_map, True)
        links.new(tex(metallic).outputs['Color'], p.inputs['Metallic'])
    m.diffuse_color = (*color, 1)
    return m


def smoothstep(lo, hi, value):
    t = max(0, min(1, (value-lo)/(hi-lo)))
    return t*t*(3-2*t)


def weights(p, width):
    side = 'Left' if p.x > 0 else 'Right'
    x, z = abs(p.x), p.z
    shoulder = .205*width
    elbow, wrist = shoulder+.255, shoulder+.49
    if z > 1.60:
        t = smoothstep(1.60, 1.67, z)
        return {'Neck': 1-t, 'Head': t}
    if x > shoulder-.03 and z > 1.32:
        if x > wrist-.03:
            t = smoothstep(wrist-.03, wrist+.025, x)
            return {side+'LowerArm': 1-t, side+'Hand': t}
        if x > elbow-.075:
            t = smoothstep(elbow-.075, elbow+.075, x)
            return {side+'UpperArm': 1-t, side+'LowerArm': t}
        t = smoothstep(shoulder-.03, shoulder+.065, x)
        return {'UpperChest': 1-t, side+'UpperArm': t}
    if z > 1.48:
        t = smoothstep(1.48, 1.60, z)
        return {'UpperChest': 1-t, 'Neck': t}
    if z > 1.35:
        t = smoothstep(1.35, 1.47, z)
        return {'Chest': 1-t, 'UpperChest': t}
    if z > 1.12:
        t = smoothstep(1.12, 1.29, z)
        return {'Spine': 1-t, 'Chest': t}
    if z > 1.00:
        t = smoothstep(1.00, 1.12, z)
        return {'Hips': 1-t, 'Spine': t}
    if z > .81:
        # Wide, smooth hip crease. Centre crotch remains primarily pelvis-bound.
        t = smoothstep(.81, 1.0, z)
        return {side+'UpperLeg': 1-t, 'Hips': t}
    t = smoothstep(.43, .61, z)
    return {side+'LowerLeg': 1-t, side+'UpperLeg': t}


def reweight(obj, width):
    obj.vertex_groups.clear()
    for v in obj.data.vertices:
        p = obj.matrix_world @ v.co
        for name, weight in weights(p, width).items():
            if weight <= 0:
                continue
            group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
            group.add([v.index], weight, 'REPLACE')


def refine_parts(pieces, collection, rig, width, kind, skin):
    def fuse(objects, name, voxel, ratio):
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:
            o.select_set(True)
            pieces.remove(o)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = name
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        rem = obj.modifiers.new('Continuous surface', 'REMESH')
        rem.mode = 'VOXEL'; rem.voxel_size = voxel
        bpy.ops.object.modifier_apply(modifier=rem.name)
        sm = obj.modifiers.new('Soften garment joins', 'SMOOTH')
        sm.factor = .7; sm.iterations = 3
        bpy.ops.object.modifier_apply(modifier=sm.name)
        dec = obj.modifiers.new('Bounded game topology', 'DECIMATE'); dec.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=dec.name)
        for p in obj.data.polygons:
            p.use_smooth = True
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=.015)
        bpy.ops.object.mode_set(mode='OBJECT')
        reweight(obj, width)
        pieces.append(obj)
        return obj

    # Remove covered skin surfaces that otherwise intersect trousers during motion.
    for obj in list(pieces):
        if obj.name.split('.')[0] == 'Right leg' and kind == 'Runner':
            bm = bmesh.new(); bm.from_mesh(obj.data)
            bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z > .27], context='VERTS')
            bm.to_mesh(obj.data); bm.free()
        elif obj.name.split('.')[0] in ('Pelvis', 'Left leg', 'Right leg'):
            pieces.remove(obj); bpy.data.objects.remove(obj, do_unlink=True)
    # Fingers retain their individually authored articulation and weights.
    skin_parts = [o for o in pieces if o.data.materials[0] == skin and
                  not any(f in o.name.lower() for f in ('index', 'middle', 'ring', 'little', 'thumb', 'leg'))]
    fuse(skin_parts, 'Continuous upper body and arms', .006, .16)
    pants = [o for o in pieces if 'trousers' in o.name or 'waist and seat' in o.name]
    trousers = fuse(pants, 'Continuous trousers with hip and knee bends', .006, .22)
    shirts = [o for o in pieces if o.name.startswith('Torn shirt') or 'Work shirt sleeve' in o.name]
    if len(shirts) > 1:
        # The torso shell is open at its neck and hem. Give it thickness before
        # volume fusion or voxel remeshing can discard that entire garment.
        for obj in shirts:
            if obj.name.startswith('Torn shirt'):
                bpy.context.view_layer.objects.active=obj
                mod=obj.modifiers.new('Cloth shell thickness','SOLIDIFY')
                mod.thickness=.009;mod.offset=0
                bpy.ops.object.modifier_apply(modifier=mod.name)
        fuse(shirts, 'Continuous work shirt', .006, .22)
    # Geometric compression folds; kept small enough to preserve the silhouette.
    for obj in pieces:
        mat_name = obj.data.materials[0].name
        cloth = any(key in mat_name for key in ('workwear', 'cotton', 'canvas', 'denim', 'Webbing'))
        if not cloth or len(obj.data.vertices) < 150:
            continue
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            joint = math.exp(-((p.z-.52)/.12)**2)+math.exp(-((p.z-.98)/.1)**2)
            wave = math.sin(p.z*91+p.x*20+math.sin(p.y*25))*min(.0018, .0003+joint*.0014)
            v.co += v.normal*wave
        if 'hood' not in obj.name.lower() and 'mantle' not in obj.name.lower():
            reweight(obj, width)
    # A less uniform mouth and smaller clouded irises reduce the toy-like expression.
    for obj in pieces:
        if obj.name.startswith('Clouded recessed eye'):
            obj.scale *= .82
        if obj.name.startswith('Broken tooth'):
            obj.scale.z *= .66
    rig['deformation_notes'] = 'Continuous elbows/shoulders; blended hips/knees. Requires motion preview; no automatic quality guarantee.'
