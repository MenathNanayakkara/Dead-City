"""Render the actual Blender actions; no AI-generated video or Unity playback."""
import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
KINDS=('Hunter','Runner','Ambusher','Elite')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Blender'/'Hunter_Animated.blend'))
scene=bpy.context.scene
for kind in KINDS[1:]:
    with bpy.data.libraries.load(str(ROOT/'Blender'/(kind+'_Animated.blend')),link=False) as (source,target):
        target.objects=[n for n in source.objects if n in (kind+'_Rig',kind+'_CustomMesh')]
        target.actions=list(source.actions)
    col=bpy.data.collections.new(kind+' animation review');scene.collection.children.link(col)
    for obj in target.objects:col.objects.link(obj)
scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=True
scene.cycles.use_light_tree=False;scene.render.use_simplify=True;scene.cycles.texture_limit='512'
scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.fps=24
cam=scene.camera;cam.location=(2,-10,3.1);cam.rotation_euler=(Vector((0,0,1.1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=5.7
rigs=[]
for i,kind in enumerate(KINDS):
    rig=bpy.data.objects[kind+'_Rig'];rig.location=((i-1.5)*1.35,0,0);rigs.append(rig)
    data=bpy.data.curves.new(kind+' label','FONT');data.body=kind.upper();data.align_x='CENTER';data.size=.13
    obj=bpy.data.objects.new(kind+' label',data);scene.collection.objects.link(obj);obj.location=(rig.location.x,0,2.3);obj.rotation_euler=cam.rotation_euler
    mat=bpy.data.materials.get('Preview label') or bpy.data.materials.new('Preview label');mat.diffuse_color=(.06,.08,.08,1);obj.data.materials.append(mat)

def set_action(kind,mode,time):
    rig=bpy.data.objects[kind+'_Rig'];action=bpy.data.actions[kind+'_'+mode]
    rig.animation_data.action=action
    if action.slots:rig.animation_data.action_slot=action.slots[0]
    return action

for mode,t in (() if '--motion-only' in sys.argv else (('Walk',.35),('Chase',.35),('Attack',.56),('Death',1))):
    # NLA strips allow independent local frame rates on the shared preview timeline.
    for kind,rig in zip(KINDS,rigs):
        action=set_action(kind,mode,t)
        for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
        rig.animation_data.action=None
        track=rig.animation_data.nla_tracks.new();strip=track.strips.new(mode,1,action)
        strip.action_frame_start=action.frame_range[0];strip.action_frame_end=action.frame_range[1]
        strip.scale=60/max(1,action.frame_range[1]-action.frame_range[0])
    scene.frame_set(1+round(t*60))
    scene.render.filepath=str(ROOT/'Previews'/(mode+'_Blender.png'))
    bpy.ops.render.render(write_still=True)
    print('POSE_RENDER',mode,flush=True)

# Save an editable combined review with the walking action on each character.
for kind,rig in zip(KINDS,rigs):
    for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
    set_action(kind,'Walk',0)
scene.frame_start=1;scene.frame_end=90;scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Blender'/'Four_Enemies_Animated_Review.blend'))

if '--stills' in sys.argv:sys.exit(0)
# CPU Cycles avoids a graphics-driver failure observed during Eevee capture.
scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=2
scene.cycles.texture_limit='256'
scene.render.resolution_x=768;scene.render.resolution_y=432
frames=ROOT/'Previews'/'Frames';frames.mkdir(exist_ok=True)
segments=(('Walk',48),('Chase',48),('Attack',48))
offset=0
for mode,count in segments:
    for kind,rig in zip(KINDS,rigs):
        for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
        action=set_action(kind,mode,0);rig.animation_data.action=None
        track=rig.animation_data.nla_tracks.new();strip=track.strips.new(mode,1,action)
        strip.scale=24/30
        strip.repeat=8 if mode in ('Walk','Chase') else 1
        strip.extrapolation='HOLD_FORWARD'
    for f in range(count):
        scene.frame_set(f+1)
        scene.render.filepath=str(frames/('Motion_%04d.png'%(offset+f)))
        bpy.ops.render.render(write_still=True)
    offset+=count;print('MOTION_RENDER',mode,flush=True)
print('BLENDER_PREVIEW_COMPLETE',flush=True)
