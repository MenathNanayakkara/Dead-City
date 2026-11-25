"""Validate the export UV channel in a SculptTextureRevision4 Blender file.

Run inside Blender:
    blender --background Character.blend --python validate_atlas_uv.py -- Character

The check intentionally covers deterministic properties that can be measured
without rendering: channel naming, active/export selection, finite coordinates,
0-1 bounds, loop coverage, and degenerate UV triangles.
"""

import json
import math
import sys
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parent
ATLAS_NAME = "AtlasUV"
BOUNDS_EPSILON = 1e-5
AREA_EPSILON = 1e-10


def triangle_area(a, b, c):
    """Return the unsigned area of a triangle in UV space."""
    return abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5


def validate_mesh(mesh_object):
    mesh = mesh_object.data
    layer = mesh.uv_layers.get(ATLAS_NAME)
    issues = []

    if layer is None:
        return {
            "mesh": mesh_object.name,
            "valid": False,
            "issues": [f"Missing required UV layer: {ATLAS_NAME}"],
        }

    if mesh.uv_layers.active != layer:
        issues.append(f"{ATLAS_NAME} is not the active UV layer")
    if not layer.active_render:
        issues.append(f"{ATLAS_NAME} is not the render/export UV layer")
    if len(layer.data) != len(mesh.loops):
        issues.append("UV loop count does not match mesh loop count")

    outside = 0
    non_finite = 0
    for item in layer.data:
        u, v = item.uv
        if not math.isfinite(u) or not math.isfinite(v):
            non_finite += 1
        elif (
            u < -BOUNDS_EPSILON
            or u > 1.0 + BOUNDS_EPSILON
            or v < -BOUNDS_EPSILON
            or v > 1.0 + BOUNDS_EPSILON
        ):
            outside += 1

    mesh.calc_loop_triangles()
    degenerate = 0
    for triangle in mesh.loop_triangles:
        coordinates = [layer.data[index].uv for index in triangle.loops]
        if triangle_area(*coordinates) <= AREA_EPSILON:
            degenerate += 1

    if non_finite:
        issues.append(f"{non_finite} UV loops contain non-finite coordinates")
    if outside:
        issues.append(f"{outside} UV loops fall outside the 0-1 atlas")
    if degenerate:
        issues.append(f"{degenerate} triangles have degenerate UV area")

    return {
        "mesh": mesh_object.name,
        "vertices": len(mesh.vertices),
        "loops": len(mesh.loops),
        "triangles": len(mesh.loop_triangles),
        "uv_layer": layer.name,
        "outside_01": outside,
        "non_finite": non_finite,
        "degenerate_triangles": degenerate,
        "valid": not issues,
        "issues": issues,
    }


def main(character):
    expected_name = f"{character}_CustomMesh"
    mesh_object = bpy.data.objects.get(expected_name)
    if mesh_object is None or mesh_object.type != "MESH":
        raise RuntimeError(f"Missing mesh object: {expected_name}")

    result = {
        "schema_version": 1,
        "character": character,
        "blend_file": bpy.data.filepath,
        "atlas": validate_mesh(mesh_object),
    }
    report_path = ROOT / "Evidence" / f"{character}_AtlasUV.json"
    report_path.parent.mkdir(exist_ok=True)
    report_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2), flush=True)

    if not result["atlas"]["valid"]:
        raise RuntimeError(f"AtlasUV validation failed for {character}")


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--") + 1 :]
    if len(arguments) != 1:
        raise SystemExit("Expected one character name after --")
    main(arguments[0])
