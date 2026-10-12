from __future__ import annotations

from dataclasses import dataclass

from .domain import Vec2


@dataclass
class DynamicGraph:
    positions: dict[str, Vec2]
    edges: dict[str, dict[str, float]]

    def neighbors(self, node: str):
        yield from self.edges.get(node, {}).items()