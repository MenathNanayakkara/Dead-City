from dataclasses import dataclass


@dataclass
class Reservation:
    owner_id: str
    expires_at: float


class ReservationTable:
    def __init__(self):
        self._by_point: dict[str, Reservation] = {}

    def is_available(self, point_id: str, owner_id: str, now: float) -> bool:
        current = self._by_point.get(point_id)
        return current is None or current.owner_id == owner_id

    def reserve(self, point_id: str, owner_id: str, now: float, ttl: float) -> bool:
        if ttl <= 0 or not self.is_available(point_id, owner_id, now):
            return False
        self._by_point[point_id] = Reservation(owner_id, now + ttl)
        return True

