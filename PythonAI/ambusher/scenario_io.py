from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .domain import TacticalPoint, Vec2
from .dynamic_astar import DynamicGraph
from .reservations import ReservationTable


@dataclass
class ScenarioWorld:
    points: tuple[TacticalPoint, ...]
    reservations: ReservationTable

    def tactical_points(self) -> tuple[TacticalPoint, ...]:
        return self.points

    def is_available(self, point_id: str, owner_id: str, now: float) -> bool:
        return self.reservations.is_available(point_id, owner_id, now)

    def reserve(self, point_id: str, owner_id: str, now: float, ttl: float) -> bool:
        return self.reservations.reserve(point_id, owner_id, now, ttl)

    def release(self, owner_id: str) -> None:
        self.reservations.release(owner_id)


@dataclass(frozen=True)
class Scenario:
    graph: DynamicGraph
    world: ScenarioWorld
    start: Vec2
    player: Vec2


def _vec(values: list[float]) -> Vec2:
    if len(values) != 2:
        raise ValueError("Coordinates require exactly two values")
    return Vec2(float(values[0]), float(values[1]))


def load_scenario(path: str | Path) -> Scenario:
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    positions = {name: _vec(value) for name, value in data["nodes"].items()}
    edges: dict[str, dict[str, float]] = {name: {} for name in positions}
    for start, end, cost in data["edges"]:
        if start not in positions or end not in positions or float(cost) <= 0:
            raise ValueError(f"Invalid edge {start}-{end}")
        edges[start][end] = edges[end][start] = float(cost)

    points = tuple(
        TacticalPoint(
            item["id"], positions[item["node"]], bool(item["concealed"]),
            item.get("kind", "ambush"),
        )
        for item in data["tactical_points"]
    )
    return Scenario(
        DynamicGraph(positions, edges),
        ScenarioWorld(points, ReservationTable()),
        _vec(data["agent_start"]),
        _vec(data["player_start"]),
    )
