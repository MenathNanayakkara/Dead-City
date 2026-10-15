from __future__ import annotations

from .domain import Vec2


def predict_position(player: Vec2, velocity: Vec2, seconds: float) -> Vec2:
    if seconds < 0:
        raise ValueError("prediction time cannot be negative")
    return player + velocity * seconds