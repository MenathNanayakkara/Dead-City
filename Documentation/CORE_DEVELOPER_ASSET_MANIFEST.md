# Core Developer graphics asset manifest

This manifest defines the files owned by the custom 3D modeling and visualization
workstream. Assets are introduced incrementally so each Git commit represents one
reviewable modeling, texturing, export, integration, or evidence milestone.

## Character scope

The workstream contains four original infected character variants:

| Character | Visual direction | Primary delivery |
| --- | --- | --- |
| Hunter | Lean infected worker with worn workwear | Rigged Blender source and animated FBX |
| Runner | Lightweight infected with a fast silhouette | Rigged Blender source and animated FBX |
| Ambusher | Concealed infected with rotten canvas clothing | Rigged Blender source and animated FBX |
| Elite | Heavy infected with damaged metal armour | Rigged Blender source and animated FBX |

## Required asset groups

Each character is complete only when all of the following groups are present.

1. `ArtSource/CustomEnemies/SculptTextureRevision4/Blender/`
   contains the baked-material and editable-material Blender files.
2. `ArtSource/CustomEnemies/SculptTextureRevision4/Textures/`
   contains base-colour, normal, roughness, and metallic texture maps.
3. `ArtSource/CustomEnemies/SculptTextureRevision4/UnityExports/`
   contains the animated FBX exported from Blender.
4. `Assets/Art/CustomEnemies/Models/` contains the Unity-imported FBX and its
   metadata.
5. `Assets/Art/CustomEnemies/Materials/` and
   `Assets/Art/CustomEnemies/Textures/Sculpt4/` contain the Unity PBR setup.
6. `Assets/Prefabs/Enemies/` contains the configured gameplay prefab with the
   intended material overrides, scale, visual placement, and shared collision.
7. `ArtSource/CustomEnemies/SculptTextureRevision4/Previews/` and `Evidence/`
   contain Blender renders and technical review records.

## Shared visualization assets

- `Four_Enemies_Sculpt4_Review.blend` provides a combined Blender review scene.
- `Four_Enemies_Actual_Blender.png` presents the final character lineup.
- `Walk_Actual_Blender.png` and `Attack_Actual_Blender.png` demonstrate posed
  deformation using the authored meshes and rigs.
- `review.json` records topology, bones, weights, packed images, and deformation
  samples.

## Commit boundaries

Text source and documentation should be staged by logical hunk. Blender files,
FBX exports, textures, Unity assets, and their `.meta` files are indivisible and
must be committed atomically. Generated caches and temporary renders are excluded.

This manifest identifies project artifacts; personal authorship must be supported
by truthful contribution records and work-in-progress evidence.
