from __future__ import annotations

from .config import AmbusherConfig
from .domain import TacticalPoint, Vec2


def predict_position(player: Vec2, velocity: Vec2, seconds: float) -> Vec2:
    if seconds < 0:
        raise ValueError("prediction time cannot be negative")
    return player + velocity * seconds


def score_flank_concealment_travel(config: AmbusherConfig, player: Vec2, forward: Vec2,
                                    point: TacticalPoint, path_cost: float) -> tuple[float, float]:
    relative = point.position - player
    flank = (1.0 - forward.normalized().dot(relative.normalized())) * 0.5
    travel = 1.0 - min(max(path_cost, 0.0) / 40.0, 1.0)
    score = (flank * config.flank_weight + float(point.concealed) * config.concealment_weight
             + travel * config.travel_weight)
    return score, flank