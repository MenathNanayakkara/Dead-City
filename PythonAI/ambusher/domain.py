from __future__ import annotations

from dataclasses import dataclass
from math import hypot


@dataclass(frozen=True, order=True)
class Vec2:
    x: float
    y: float

    def distance_to(self, other: "Vec2") -> float:
        return hypot(self.x - other.x, self.y - other.y)

    def normalized(self) -> "Vec2":
        length = hypot(self.x, self.y)
        return Vec2(0.0, 0.0) if length == 0 else Vec2(self.x / length, self.y / length)

    def dot(self, other: "Vec2") -> float:
        return self.x * other.x + self.y * other.y

    def __add__(self, other: "Vec2") -> "Vec2":
        return Vec2(self.x + other.x, self.y + other.y)

    def __sub__(self, other: "Vec2") -> "Vec2":
        return Vec2(self.x - other.x, self.y - other.y)

    def __mul__(self, value: float) -> "Vec2":
        return Vec2(self.x * value, self.y * value)

@dataclass(frozen=True)
class Observation:
    now: float
    agent_position: Vec2
    player_position: Vec2 | None = None
    player_forward: Vec2 = Vec2(0.0, 1.0)
    player_velocity: Vec2 = Vec2(0.0, 0.0)
    player_visible: bool = False
    player_reloading: bool = False
    sound_position: Vec2 | None = None
    route_failed: bool = False
    attack_ready: bool = True


@dataclass(frozen=True)
class TacticalPoint:
    point_id: str
    position: Vec2
    concealed: bool
    kind: str = "ambush"