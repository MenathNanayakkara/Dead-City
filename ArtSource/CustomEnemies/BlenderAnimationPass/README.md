# Blender-authored enemy animation pass

These are new, editable Blender animation files for the original custom enemy models. The live Unity game, city layout and player are unchanged.

## Open and play

1. Open `Blender/Hunter_Animated.blend`, `Runner_Animated.blend`, `Ambusher_Animated.blend` or `Elite_Animated.blend` in Blender 5.2 or newer.
2. The selected armature starts with its **Walk** action. Press Space to play.
3. In the Animation workspace, open the **Dope Sheet → Action Editor**. Select the armature, then choose another action using the action dropdown.
4. Set the timeline start to **1** and end to that action's last keyframe. For looping playback, exclude the duplicated final frame. Exact ranges are listed in `Evidence/actions.json`.
5. Edit the bone keyframes or Graph Editor curves. These are baked deform-bone actions, without a separate interactive IK control rig.

Actions are retained with Fake User and marked as assets. The `.blend` files contain packed material textures. The combined review file shows all four rigs, each with its own walking action.

## Actions

Each enemy has **Idle, Walk, Chase, TurnLeft, TurnRight, Attack, Hit, Stagger and Death**. Elite also has **HeavyAttack**: **37 actions total**.

- Hunter: uneven restrained walk and a leading-hand swipe.
- Runner: faster stride and forward-reaching attack.
- Ambusher: lower posture, lower foot lift and opposite leading-hand attack.
- Elite: wider stance, slower walk and an extra overhead heavy attack.

Locomotion is authored in place. Foot targets move backward during stance to counter the intended forward movement, with lifted swing phases and knees directed forward. Body weight shift and small arm variation are keyed. All character movement must remain owned by navigation when integrating later; do not apply root translation a second time.

Turn clips contain a 90-degree body turn in the pose. They require a deliberate root-orientation handoff when integrated: do not play the full turn while also rotating the gameplay root by another 90 degrees. They are preview assets, not wired gameplay transitions.

Attack actions have a **CONTACT** marker at 0.56 normalized time. This is an authoring marker; it does not automatically damage anything in Unity. Hit, stagger and death are non-looping. Death ground correction is baked into the action, with a held final pose; it is not a physics ragdoll.

## Exports and integration limits

`Exports/*_Animated.fbx` contains the model, skeleton and baked action takes. Existing unanimated files are preserved in `Revision2`. The animated exports have not been installed or validated as a replacement in the live game. Unity avatar mapping, transition timing, collision heights, navigation speeds, turning and uneven-ground behaviour still need a separate integration pass.

`Previews` contains actual Blender renders. This pass improves the editable motion foundation; it is not motion capture, photorealistic character art, or a guarantee of flawless gameplay animation. Clothing intersections and the stylized face/shoulder shapes may need further mesh work, particularly in extreme poses.

## Delivered preview

`Previews/Blender_Enemy_Animations.mp4` shows 144 actual Blender-rendered frames at 24 fps: two seconds each of walking, chasing and attacking. The characters remain in place to make the joint motion visible. This video does not demonstrate world-space foot locking against navigation movement. Still renders of walk, chase, attack and death are alongside it.

The original GPU video attempt failed in the graphics driver; the completed preview uses CPU Cycles. The animation sources and exports were saved independently of that render failure.

`Enemy_Blender_Animations.zip` contains the four individual animated Blender files, this guide as it stood when packaged, and the action/frame listing. The combined review file and video are separate downloads.

Authorship: AI-assisted procedural modelling and animation authored by Codex in Blender at the project owner's request. No Unity motion clips or downloaded animation data were used to generate these new actions. Do not describe them as manually keyframed by a student.

Source recipe: `author_actions.py`. It writes only this animation pass. Preview recipe: `render_actions.py`.
