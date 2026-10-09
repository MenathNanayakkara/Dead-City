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