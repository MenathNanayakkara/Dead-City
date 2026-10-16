"""Back up replaced assets; copy reviewed exports and convert PBR textures."""
import json
import hashlib
import shutil
from datetime import datetime
from pathlib import Path
from PIL import Image, ImageChops

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[2]
DEST=PROJECT/"Assets/Art/CustomEnemies"
TEXTURES=DEST/"Textures/Sculpt4"
BACKUP=PROJECT/"Backups"/("Sculpt4Import-"+datetime.now().strftime("%Y%m%d-%H%M%S"))
KINDS=("Hunter","Runner","Ambusher","Elite")
receipt=json.loads((ROOT/"UnityExports/export-receipt.json").read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for item in receipt:
    if sha(Path(item["export"]))!=item["export_sha256"]:
        raise RuntimeError("Export digest mismatch: "+item["character"])
BACKUP.mkdir(parents=True)
identity_files = [
    *("Assets/Art/CustomEnemies/Models/"+k+"_Animated.fbx"+s for k in KINDS for s in ("",".meta")),
    *("Assets/Prefabs/Enemies/"+k+".prefab"+s for k in (*KINDS,"EnemyBase") for s in ("",".meta")),
]
identity_hashes = {
    relative: sha(PROJECT/relative)
    for relative in identity_files
    if relative.endswith(".meta")
}
for relative in identity_files:
    src=PROJECT/relative
    if not src.is_file():raise RuntimeError("Missing existing asset: "+str(src))
    dst=BACKUP/relative;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
protected={}
for directory in (PROJECT/"Assets/Art/CustomEnemies/Controllers",PROJECT/"Assets/Scripts/AI"):
    for p in directory.rglob("*"):
        if p.is_file():protected[str(p.relative_to(PROJECT))]=sha(p)
TEXTURES.mkdir(parents=True,exist_ok=True)
files=[]
for item in receipt:
    kind=item["character"]
    target=DEST/"Models"/(kind+"_Animated.fbx")
    shutil.copy2(item["export"],target)
    files.append(dict(path=str(target.relative_to(PROJECT)),sha256=sha(target)))
    for channel,size in (("BaseColor","4K"),("Normal","2K")):
        src=ROOT/"Textures"/(kind+"_"+channel+"_"+size+".png")
        target=TEXTURES/(kind+"_"+channel+".png")
        shutil.copy2(src,target)
        files.append(dict(path=str(target.relative_to(PROJECT)),sha256=sha(target),source=str(src)))
    metal=Image.open(ROOT/"Textures"/(kind+"_Metallic_2K.png")).convert("L")
    rough=Image.open(ROOT/"Textures"/(kind+"_Roughness_2K.png")).convert("L")
    if metal.size!=rough.size:raise RuntimeError("Texture dimensions mismatch")
    # Both source maps are linear scalar images. Standard reads metallic from
    # red and smoothness from alpha; smoothness = 1 - roughness.
    packed=Image.merge("RGBA",(metal,metal,metal,ImageChops.invert(rough)))
    target=TEXTURES/(kind+"_MetallicSmoothness.png");packed.save(target)
    files.append(dict(path=str(target.relative_to(PROJECT)),sha256=sha(target),
                      conversion="R=metallic; A=1-roughness; linear"))
for relative, expected_hash in identity_hashes.items():
    actual_hash = sha(PROJECT/relative)
    if actual_hash != expected_hash:
        raise RuntimeError("Unity identity metadata changed while staging: "+relative)
result=dict(revision="SculptTextureRevision4",backup=str(BACKUP),files=files,
            preserved_identity_files=identity_hashes,
            protected_files=protected,source_export_receipt=receipt,
            provenance="Project-owned AI-assisted Blender models and generated texture sources; native Unity import.")
(ROOT/"UnityExports/import-receipt.json").write_text(json.dumps(result,indent=2))
print("STAGED",len(files),"files. Backup:",BACKUP)
