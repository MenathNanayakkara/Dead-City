from __future__ import annotations

from dataclasses import dataclass
from heapq import heappop, heappush
from math import inf

from .domain import Vec2


@dataclass
class DynamicGraph:
    positions: dict[str, Vec2]
    edges: dict[str, dict[str, float]]

    def neighbors(self, node: str):
        yield from self.edges.get(node, {}).items()


@dataclass(frozen=True)
class _SearchState:
    came_from: dict[str, str]
    g_score: dict[str, float]
    reached: bool
    expanded: int


class DynamicAStar:
    def __init__(self, graph: DynamicGraph):
        self.graph = graph

    def _explore(self, start: str, goal: str) -> _SearchState:
        if start not in self.graph.positions or goal not in self.graph.positions:
            return _SearchState({}, {}, False, 0)
        frontier: list[tuple[float, str]] = [(0.0, start)]
        came_from: dict[str, str] = {}
        g_score = {start: 0.0}
        expanded = 0
        while frontier:
            _, current = heappop(frontier)
            expanded += 1
            if current == goal:
                return _SearchState(came_from, g_score, True, expanded)
            for neighbor, edge_cost in self.graph.neighbors(current):
                tentative = g_score[current] + edge_cost
                if tentative >= g_score.get(neighbor, inf):
                    continue
                came_from[neighbor] = current
                g_score[neighbor] = tentative
                heuristic = self.graph.positions[neighbor].distance_to(self.graph.positions[goal])
                heappush(frontier, (tentative + heuristic, neighbor))
        return _SearchState(came_from, g_score, False, expanded)