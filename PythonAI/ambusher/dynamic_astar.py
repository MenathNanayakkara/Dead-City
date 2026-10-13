from __future__ import annotations

from dataclasses import dataclass, field
from heapq import heappop, heappush
from math import inf

from .domain import Vec2


@dataclass
class DynamicGraph:
    positions: dict[str, Vec2]
    edges: dict[str, dict[str, float]]
    blocked: set[tuple[str, str]] = field(default_factory=set)
    version: int = 0

    def set_blocked(self, a: str, b: str, blocked: bool = True) -> None:
        edge = tuple(sorted((a, b)))
        if blocked:
            self.blocked.add(edge)
        else:
            self.blocked.discard(edge)
        self.version += 1

    def neighbors(self, node: str):
        for neighbor, cost in self.edges.get(node, {}).items():
            if tuple(sorted((node, neighbor))) not in self.blocked:
                yield neighbor, cost


@dataclass(frozen=True)
class SearchResult:
    nodes: tuple[str, ...]
    positions: tuple[Vec2, ...]
    cost: float
    expanded: int
    graph_version: int


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

    def search(self, start: str, goal: str) -> SearchResult | None:
        state = self._explore(start, goal)
        if not state.reached:
            return None
        nodes = [goal]
        current = goal
        while current in state.came_from:
            current = state.came_from[current]
            nodes.append(current)
        nodes.reverse()
        return SearchResult(tuple(nodes), tuple(self.graph.positions[node] for node in nodes),
                            state.g_score[goal], state.expanded, self.graph.version)


class GraphNavigator:
    def __init__(self, graph: DynamicGraph):
        self.graph = graph
        self.searcher = DynamicAStar(graph)

    def _nearest(self, position: Vec2) -> str:
        return min(self.graph.positions,
                   key=lambda node: (position.distance_to(self.graph.positions[node]), node))

    def route_result(self, start: Vec2, goal: Vec2) -> SearchResult | None:
        return self.searcher.search(self._nearest(start), self._nearest(goal))

    def route(self, start: Vec2, goal: Vec2) -> tuple[Vec2, ...] | None:
        result = self.route_result(start, goal)
        return None if result is None else result.positions

    def route_cost(self, start: Vec2, goal: Vec2) -> float | None:
        result = self.route_result(start, goal)
        return None if result is None else result.cost