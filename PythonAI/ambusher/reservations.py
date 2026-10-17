from dataclasses import dataclass


@dataclass
class Reservation:
    owner_id: str
    expires_at: float


class ReservationTable:
    def __init__(self):
        self._by_point: dict[str, Reservation] = {}

    def _expire(self, now: float) -> None:
        for point_id in [key for key, value in self._by_point.items() if value.expires_at <= now]:
            del self._by_point[point_id]

    def is_available(self, point_id: str, owner_id: str, now: float) -> bool:
        self._expire(now)
        current = self._by_point.get(point_id)
        return current is None or current.owner_id == owner_id

    def reserve(self, point_id: str, owner_id: str, now: float, ttl: float) -> bool:
        if ttl <= 0 or not self.is_available(point_id, owner_id, now):
            return False
        self.release(owner_id)
        self._by_point[point_id] = Reservation(owner_id, now + ttl)
        return True

    def release(self, owner_id: str) -> None:
        for point_id in [key for key, value in self._by_point.items() if value.owner_id == owner_id]:
            del self._by_point[point_id]

