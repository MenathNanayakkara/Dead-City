"""Shared Blender configuration for portable infected-character PBR atlases."""

from dataclasses import dataclass

import bpy


@dataclass(frozen=True)
class BakeMap:
    name: str
    size: int
    colour_space: str
    bake_type: str
    principled_socket: str | None

    @property
    def resolution_suffix(self):
        return "4K" if self.size == 4096 else "2K"


MAPS = (
    BakeMap("BaseColor", 4096, "sRGB", "EMIT", "Base Color"),
    BakeMap("Roughness", 2048, "Non-Color", "EMIT", "Roughness"),
    BakeMap("Metallic", 2048, "Non-Color", "EMIT", "Metallic"),
    BakeMap("Normal", 2048, "Non-Color", "NORMAL", None),
)


def configure_cycles(scene, samples=8, margin=8):
    """Apply the common deterministic Cycles bake settings."""
    scene.render.engine = "CYCLES"
    scene.cycles.samples = samples
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin = margin
    scene.render.bake.use_clear = True


def create_image(character, bake_map):
    """Create an unpacked image with the correct resolution and colour space."""
    image = bpy.data.images.new(
        f"{character}_{bake_map.name}_{bake_map.resolution_suffix}",
        bake_map.size,
        bake_map.size,
        alpha=False,
    )
    image.colorspace_settings.name = bake_map.colour_space
    return image


def add_active_target(material, image, uv_name):
    """Attach and activate a temporary image target for one material."""
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    target = nodes.new("ShaderNodeTexImage")
    target.image = image
    uv_node = nodes.new("ShaderNodeUVMap")
    uv_node.uv_map = uv_name
    links.new(uv_node.outputs[0], target.inputs[0])

    for node in nodes:
        node.select = False
    target.select = True
    nodes.active = target
    return target, uv_node


def save_and_pack(image, output_path):
    """Save a PNG beside the Blender source and pack it into the blend file."""
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    image.pack()
