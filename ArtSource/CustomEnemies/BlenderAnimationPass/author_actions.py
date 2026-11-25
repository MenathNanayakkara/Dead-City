"""Original, editable Blender keyframe actions for the four custom enemies.
No Unity assets or game scene files are written. Run using Blender 5.2.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix

ROOT=Path(__file__).resolve().parent
SOURCE=ROOT.parent/'Revision2'/'Blender'/'Four_Enemies_Review.blend'
for folder in ('Blender','Exports','Previews','Evidence'):(ROOT/folder).mkdir(parents=True,exist_ok=True)
FPS=30
KINDS=('Hunter','Runner','Ambusher','Elite')
CONFIG={
    'Hunter':dict(width=1.03,lean=12,walk=.95,run=.70,speed=1.15,chase=3.4,stance=.64,lift=.09,asym=.025),
    'Runner':dict(width=.82,lean=20,walk=.85,run=.56,speed=1.45,chase=4.8,stance=.60,lift=.13,asym=.008),
    'Ambusher':dict(width=.91,lean=25,walk=1.1,run=.68,speed=.95,chase=3.8,stance=.68,lift=.065,asym=.015),
    'Elite':dict(width=1.30,lean=9,walk=1.1,run=.78,speed=1.05,chase=3.4,stance=.66,lift=.095,asym=.015),
}

def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)

def rotation(name,axis,degrees):
    p=rig.pose.bones[name]
    rest=p.bone.matrix_local.to_quaternion()
    p.rotation_quaternion=rest.inverted()@Quaternion(Vector(axis),math.radians(degrees))@rest

def reset():
    for p in rig.pose.bones:
        p.rotation_mode='QUATERNION';p.location=(0,0,0);p.rotation_quaternion=(1,0,0,0);p.scale=(1,1,1)

def offset_hips(delta):
    p=rig.pose.bones['Hips'];p.location=p.bone.matrix_local.to_3x3().inverted()@Vector(delta)

def orient(name,q):
    p=rig.pose.bones[name]
    p.matrix=Matrix.LocRotScale(p.head,q,Vector((1,1,1)))

def solve(upper,lower,end,target,pole,end_rotation=None):
    """Two-bone geometric IK baked to normal deform-bone keyframes."""
    bpy.context.view_layer.update()
    a,b,c=(rig.pose.bones[n] for n in (upper,lower,end))
    start=a.head.copy();target=Vector(target)
    l1,l2=a.bone.length,b.bone.length
    delta=target-start;distance=max(abs(l1-l2)+.002,min(delta.length,l1+l2-.003))
    direction=delta.normalized();bend=Vector(pole)
    bend=(bend-direction*bend.dot(direction)).normalized()
    along=(l1*l1+distance*distance-l2*l2)/(2*distance)
    joint=start+direction*along+bend*math.sqrt(max(0,l1*l1-along*along))
    q=(b.head-start).rotation_difference(joint-start)
    orient(upper,q@a.matrix.to_quaternion())
    bpy.context.view_layer.update()
    q=(c.head-b.head).rotation_difference(start+direction*distance-b.head)
    orient(lower,q@b.matrix.to_quaternion())
    bpy.context.view_layer.update()
    if end_rotation is not None:orient(end,end_rotation)
    bpy.context.view_layer.update()

def hand(side,target,curl=.4):
    s=1 if side=='Left' else -1
    solve(side+'UpperArm',side+'LowerArm',side+'Hand',target,(s*.6,.4,-.5))
    for finger in ('Index','Middle','Ring','Little'):
        for segment in (1,2,3):rotation(side+finger+str(segment),(0,1,0),s*(24+segment*7)*curl)

def foot(side,target,pitch=0,yaw=0):
    rest=rig.data.bones[side+'Foot'].matrix_local.to_quaternion()
    q=Quaternion((0,0,1),yaw)@Quaternion((1,0,0),pitch)@rest
    solve(side+'UpperLeg',side+'LowerLeg',side+'Foot',target,(0,-1,0),q)

def base(kind,t):
    c=CONFIG[kind];p=t*math.tau
    reset();offset_hips((.008*math.sin(p),0,-.035-(.035 if kind=='Ambusher' else 0)))
    rotation('Spine',(1,0,0),c['lean']*.6+math.sin(p)*.6)
    rotation('Chest',(1,0,0),c['lean']*.4)
    rotation('Head',(1,0,0),-c['lean']*.45)
    rotation('Head',(0,1,0),3+math.sin(p)*1.2)
    rotation('Head',(0,0,1),math.sin(p)*2)
    bpy.context.view_layer.update()
    for side,s in (('Left',1),('Right',-1)):
        foot(side,(s*.105*c['width'],0,.11))
        hand(side,(s*(.24 if kind!='Elite' else .31),-.21-(.045 if s==1 else 0),1.10+(math.sin(p+s)*.008)),.65)

def gait_foot(phase,length,stance,lift):
    if phase<stance:
        return -length*.5+length*phase/stance,0,0
    t=(phase-stance)/(1-stance)
    # Hermite swing matches the stance velocity at lift-off and landing.
    v=length*(1-stance)/stance
    y=(2*t**3-3*t*t+1)*length*.5+(t**3-2*t*t+t)*v+(-2*t**3+3*t*t)*(-length*.5)+(t**3-t*t)*v
    z=lift*math.sin(math.pi*t)**2
    pitch=math.radians(16)*math.sin(math.tau*t)*math.sin(math.pi*t)
    return y,z,pitch

def gait(kind,phase,running=False):
    c=CONFIG[kind];base(kind,phase)
    duration=c['run'] if running else c['walk']
    stance=.34 if running else c['stance']
    speed=c['chase'] if running else c['speed']
    length=speed*duration*stance
    bob=(.018 if running else .009)*math.cos(phase*math.tau*2)
    offset_hips((math.sin(phase*math.tau)*(.012 if running else .018),0,(-.18 if running else -.105)+( -.035 if kind=='Ambusher' else 0)+bob))
    rotation('Hips',(0,0,1),math.sin(phase*math.tau)*3)
    rotation('Spine',(1,0,0),c['lean']*.6+(5 if running else 0))
    rotation('Chest',(0,0,1),math.sin(phase*math.tau)*(-5 if running else -3))
    bpy.context.view_layer.update()
    for side,s,shift in (('Left',1,0),('Right',-1,.5)):
        ph=(phase+shift)%1
        y,z,pitch=gait_foot(ph,length,stance,(.22 if running else c['lift'])*(1-c['asym']*s))
        # Wider track for the Elite and a lower, quieter step for the Ambusher.
        x=s*.105*c['width']+(s*.018 if kind=='Elite' else 0)
        foot(side,(x,y,.11+z),pitch)
        swing=math.sin((phase+shift)*math.tau)
        reach=(-.35 if running else -.28)+swing*(.11 if running else .055)
        hand(side,(s*(.25 if kind!='Elite' else .33),reach,(1.18 if running else 1.10)+swing*.035+(s*.025)),.8)

def turn(kind,t,direction):
    base(kind,0);c=CONFIG[kind]
    yaw=direction*math.pi*.5*smooth(t)
    rotation('Hips',(0,0,1),math.degrees(yaw))
    rotation('Head',(0,0,1),direction*15*math.sin(math.pi*t))
    q=Quaternion((0,0,1),yaw)
    for side,s,shift in (('Left',1,0),('Right',-1,.5)):
        step=smooth(max(0,min(1,(t-shift)*2)))
        fq=Quaternion((0,0,1),direction*math.pi*.5*step)
        target=fq@Vector((s*.105*c['width'],0,.11))
        target.z+=.07*math.sin(math.pi*max(0,min(1,(t-shift)*2)))**2
        foot(side,target,yaw=direction*math.pi*.5*step)
        hand(side,q@Vector((s*.26,-.22,1.1)),.65)

def attack(kind,t,heavy=False):
    base(kind,0);c=CONFIG[kind]
    wind=smooth(t/.36);strike=smooth((t-.36)/.2);recover=smooth((t-.66)/.34)
    strength=1-recover
    lean=(-7*wind+(26 if heavy else 17)*strike)*strength
    rotation('Spine',(1,0,0),c['lean']*.6+lean)
    rotation('Chest',(0,0,1),(16 if kind=='Ambusher' else -16)*wind*(1-1.7*strike)*strength)
    for side,s in (('Left',1),('Right',-1)):
        start=Vector((s*.26,-.23,1.1))
        if heavy:raised=Vector((s*.19,-.18,1.94));hit=Vector((s*.16,-.62,1.0))
        elif kind=='Runner':raised=Vector((s*.24,-.18,1.37));hit=Vector((s*.13,-.69,1.34))
        elif kind=='Ambusher':raised=Vector((s*.35,.02,1.52 if s==1 else 1.2));hit=Vector((-s*.08,-.63,1.22))
        else:raised=Vector((s*.35,.05,1.5 if s==-1 else 1.25));hit=Vector((-s*.08,-.62,1.23))
        if not heavy and kind!='Runner':
            striking=(s==1 if kind=='Ambusher' else s==-1)
            if not striking:raised=start+Vector((s*.06,.02,.03));hit=start+Vector((s*.04,-.08,-.02))
        target=start.lerp(raised,wind).lerp(hit,strike).lerp(start,recover)
        foot(side,(s*.105*c['width'],0,.11))
        hand(side,target,1)

def reaction(kind,t,heavy=False):
    base(kind,0);c=CONFIG[kind]
    strength=math.sin(math.pi*smooth(t))**2
    rotation('Spine',(1,0,0),c['lean']*.6-(22 if heavy else 12)*strength)
    rotation('Head',(1,0,0),-c['lean']*.45-14*strength)
    rotation('Chest',(0,0,1),-15*strength)
    for side,s in (('Left',1),('Right',-1)):
        y=.22*strength if heavy and s==-1 else 0
        foot(side,(s*.105*c['width'],y,.11+(.045*strength if y else 0)))
        hand(side,(s*(.26+.08*strength),-.2+strength*.08,1.1+strength*.16),.6)

def death(kind,t):
    base(kind,0)
    fall=smooth((t-.12)/.68);settle=math.sin(math.pi*smooth((t-.72)/.28))*.02
    # Full-body forward collapse with progressive knee folding, then a held end pose.
    offset_hips((.08*fall,-.24*fall,-.035-.79*fall+settle))
    rotation('Hips',(1,0,0),90*fall)
    rotation('Spine',(1,0,0),CONFIG[kind]['lean']*.6+18*math.sin(math.pi*fall))
    rotation('Head',(0,1,0),-24*fall)
    for side,s in (('Left',1),('Right',-1)):
        rotation(side+'UpperLeg',(1,0,0),-5*fall-30*math.sin(math.pi*fall))
        rotation(side+'LowerLeg',(1,0,0),10*fall+55*math.sin(math.pi*fall))
        rotation(side+'Foot',(1,0,0),-65*fall)
        rotation(side+'UpperArm',(0,1,0),s*(70-22*fall))
        rotation(side+'LowerArm',(0,0,1),s*(-28-20*fall))

def curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves

reports=[]
for kind in KINDS:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene;scene.render.fps=FPS
    rig=bpy.data.objects[kind+'_Rig'];mesh=bpy.data.objects[kind+'_CustomMesh']
    for other in KINDS:
        if other==kind:continue
        col=bpy.data.collections.get(other)
        for obj in list(col.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.collections.remove(col)
    bpy.data.collections[kind].hide_viewport=False;bpy.data.collections[kind].hide_render=False
    rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.animation_data_clear()
    rig['animation_authoring']='Original Blender keyframes with geometric leg/arm IK baked to deform bones; AI-assisted.'
    rig['preview_scope']='In-place actions, no Unity integration. Turn clips rotate pose 90 degrees; do not also turn gameplay root.'
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
    descriptions={
        'Idle':(3.0,True,lambda t:base(kind,t)),
        'Walk':(CONFIG[kind]['walk'],True,lambda t:gait(kind,t)),
        'Chase':(CONFIG[kind]['run'],True,lambda t:gait(kind,t,True)),
        'TurnLeft':(1.2,False,lambda t:turn(kind,t,1)),
        'TurnRight':(1.2,False,lambda t:turn(kind,t,-1)),
        'Attack':(1.25 if kind!='Runner' else .95,False,lambda t:attack(kind,t)),
        'Hit':(.65,False,lambda t:reaction(kind,t)),
        'Stagger':(1.15,False,lambda t:reaction(kind,t,True)),
        'Death':(1.9,False,lambda t:death(kind,t)),
    }
    if kind=='Elite':descriptions['HeavyAttack']=(1.8,False,lambda t:attack(kind,t,True))
    actions=[]
    for mode,(duration,loop,pose) in descriptions.items():
        action=bpy.data.actions.new(kind+'_'+mode);action.use_fake_user=True;action.asset_mark()
        action['loop']=loop;action['source']='Original Blender-authored procedural keyframes'
        if mode in ('Walk','Chase'):action['recommended_speed_mps']=CONFIG[kind]['speed' if mode=='Walk' else 'chase']*rig.scale.x
        if 'Attack' in mode:action['contact_normalized_time']=.56
        rig.animation_data_create();rig.animation_data.action=action
        count=round(duration*FPS);previous={}
        for f in range(count+1):
            pose(f/count)
            bpy.context.view_layer.update()
            # Death ground alignment is baked once, with no runtime floor snapping.
            if mode=='Death':
                evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());deformed=evaluated.to_mesh()
                low=min((evaluated.matrix_world@v.co).z for v in deformed.vertices);evaluated.to_mesh_clear()
                if low<.004:
                    p=rig.pose.bones['Hips'];p.location+=p.bone.matrix_local.to_3x3().inverted()@Vector((0,0,(.004-low)/rig.scale.x))
            for bone in rig.pose.bones:
                q=bone.rotation_quaternion
                if bone.name in previous and previous[bone.name].dot(q)<0:bone.rotation_quaternion=-q
                previous[bone.name]=bone.rotation_quaternion.copy()
                bone.keyframe_insert('rotation_quaternion',frame=f+1,group=bone.name)
                if bone.name=='Hips':bone.keyframe_insert('location',frame=f+1,group=bone.name)
        for curve in curves(action):
            for key in curve.keyframe_points:key.interpolation='BEZIER';key.handle_left_type='AUTO_CLAMPED';key.handle_right_type='AUTO_CLAMPED'
        action.pose_markers.new('START').frame=1
        if 'Attack' in mode:action.pose_markers.new('CONTACT').frame=round(count*.56)+1
        action.pose_markers.new('LOOP END' if loop else 'END').frame=count+1
        actions.append(dict(name=action.name,frames=count+1,fps=FPS,loop=loop,speed=action.get('recommended_speed_mps',0)))
        print('AUTHORED',action.name,flush=True)
    rig.animation_data.action=bpy.data.actions[kind+'_Walk']
    scene.frame_start=1;scene.frame_end=round(CONFIG[kind]['walk']*FPS);scene.frame_set(1)
    # Opening the file shows an animated, selected rig and the action picker.
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='DOPESHEET_EDITOR':area.spaces.active.mode='ACTION'
    cam=scene.camera;cam.location=(3,-5,2.5);cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.4
    scene.render.resolution_x=900;scene.render.resolution_y=1000
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Blender'/(kind+'_Animated.blend')))
    # Export all of this character's baked actions; do not include studio geometry.
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);mesh.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Exports'/(kind+'_Animated.fbx')),use_selection=True,
        object_types={'ARMATURE','MESH'},add_leaf_bones=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True)
    reports.append(dict(character=kind,actions=actions))
(ROOT/'Evidence'/'actions.json').write_text(json.dumps(reports,indent=2))
print('BLENDER_ACTIONS_COMPLETE',flush=True)
