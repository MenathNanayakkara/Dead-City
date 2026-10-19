from dataclasses import dataclass

from .domain import TacticalPoint
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
