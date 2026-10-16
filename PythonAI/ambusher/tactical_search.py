from __future__ import annotations

from dataclasses import dataclass

from .config import AmbusherConfig
from .domain import TacticalPoint, Vec2
from .ports import NavigationPort, TacticalWorldPort


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


def score_interception_proximity(config: AmbusherConfig, player: Vec2, predicted: Vec2,
                                 point: TacticalPoint) -> float:
    player_distance = point.position.distance_to(player)
    proximity = 1.0 - min(abs(player_distance - config.preferred_distance) / 25.0, 1.0)
    intercept = 1.0 - min(point.position.distance_to(predicted) / 30.0, 1.0)
    return intercept * config.intercept_weight + proximity * config.proximity_weight


@dataclass(frozen=True)
class ScoredPoint:
    point: TacticalPoint
    score: float
    path_cost: float


class TacticalSearch:
    def __init__(self, config: AmbusherConfig):
        self.config = config

    def choose(self, owner_id: str, agent: Vec2, player: Vec2, forward: Vec2, velocity: Vec2,
               now: float, navigation: NavigationPort, world: TacticalWorldPort,
               rejected: set[str]) -> ScoredPoint | None:
        predicted = predict_position(player, velocity, self.config.prediction_seconds)
        candidates: list[ScoredPoint] = []
        for point in world.tactical_points():
            if point.point_id in rejected or point.kind not in {"ambush", "cover"}:
                continue
            if not world.is_available(point.point_id, owner_id, now):
                continue
            path_cost = navigation.route_cost(agent, point.position)
            if path_cost is None:
                continue
            base_score, flank = score_flank_concealment_travel(
                self.config, player, forward, point, path_cost
            )
            if not point.concealed and flank < 0.65:
                continue
            score = base_score + score_interception_proximity(
                self.config, player, predicted, point
            )
            candidates.append(ScoredPoint(point, score, path_cost))
        return max(candidates, key=lambda item: item.score, default=None)

