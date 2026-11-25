import bpy,json
from pathlib import Path
r=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(r/'Blender'/'Hunter_Animated.blend'))
a=bpy.data.objects['Hunter_Rig'];m=bpy.data.objects['Hunter_CustomMesh']
for mode in ('Walk','Chase'):
    action=bpy.data.actions['Hunter_'+mode];a.animation_data.action=action;a.animation_data.action_slot=action.slots[0]
    for t in (0,.2,.35,.5,.75):
        bpy.context.scene.frame_set(round(1+t*(action.frame_range[1]-1)));bpy.context.view_layer.update()
        print(mode,t,{n:list(a.pose.bones[n].head) for n in ('Hips','LeftUpperLeg','LeftLowerLeg','LeftFoot','RightFoot')},flush=True)
