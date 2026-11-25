from PythonAI.ambusher.config import AmbusherConfig
from PythonAI.ambusher.domain import TacticalPoint, Vec2
from PythonAI.ambusher.tactical_search import TacticalSearch


class FakeNavigation:
    def __init__(self, costs):
        self.costs = costs

    def route_cost(self, _start, goal):
        return self.costs.get(goal)


class FakeWorld:
    def __init__(self, points, occupied=()):
        self.points = points
        self.occupied = set(occupied)

    def tactical_points(self):
        return self.points

    def is_available(self, point_id, _owner_id, _now):
        return point_id not in self.occupied


def choose(points, costs, rejected=(), occupied=()):
    return TacticalSearch(AmbusherConfig()).choose(
        "ambusher", Vec2(0, 0), Vec2(6, 1), Vec2(1, 0), Vec2(0, 0), 0,
        FakeNavigation(costs), FakeWorld(points, occupied), set(rejected),
    )


def test_concealed_rear_point_wins_and_rejected_point_is_skipped():
    rear = TacticalPoint("rear", Vec2(-2, 1), True)
    side = TacticalPoint("side", Vec2(6, 9), True)
    costs = {rear.position: 8, side.position: 8}
    assert choose((side, rear), costs).point == rear
    assert choose((side, rear), costs, {"rear"}).point == side


def test_occupied_invalid_unreachable_and_exposed_points_are_filtered():
    occupied = TacticalPoint("occupied", Vec2(6, 0), True)
    invalid = TacticalPoint("spawn", Vec2(5, 0), True, "spawn")
    unreachable = TacticalPoint("blocked", Vec2(4, 0), True)
    exposed = TacticalPoint("front", Vec2(8, 1), False)
    safe = TacticalPoint("safe", Vec2(4, 5), True)
    points = (occupied, invalid, unreachable, exposed, safe)
    costs = {occupied.position: 3, invalid.position: 3, exposed.position: 3, safe.position: 7}
    assert choose(points, costs, occupied={"occupied"}).point == safe


def test_equal_candidates_use_path_cost_then_stable_point_id():
    zed = TacticalPoint("zed", Vec2(6, 0), True)
    alpha = TacticalPoint("alpha", Vec2(6, 0), True)
    assert choose((zed, alpha), {zed.position: 6}).point.point_id == "alpha"


def test_no_safe_candidate_returns_none():
    point = TacticalPoint("blocked", Vec2(6, 0), True)
    assert choose((point,), {}) is None

