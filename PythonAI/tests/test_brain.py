from PythonAI.ambusher.brain import AmbusherBrain
from PythonAI.ambusher.domain import (
    AmbusherState,
    CommandKind,
    Observation,
    TacticalPoint,
    Vec2,
)
from PythonAI.ambusher.reservations import ReservationTable


class FakeNavigation:
    def route_cost(self, start, goal):
        return start.distance_to(goal)

    def route(self, start, goal):
        return (start, goal)


class FakeWorld:
    def __init__(self):
        self.points = (
            TacticalPoint("rear", Vec2(6, 0), True),
            TacticalPoint("side", Vec2(3, 4), False),
        )
        self.reservations = ReservationTable()

    def tactical_points(self):
        return self.points

    def is_available(self, point_id, owner_id, now):
        return self.reservations.is_available(point_id, owner_id, now)

    def reserve(self, point_id, owner_id, now, ttl):
        return self.reservations.reserve(point_id, owner_id, now, ttl)

    def release(self, owner_id):
        self.reservations.release(owner_id)


def visible(now, agent=Vec2(0, 0), **changes):
    values = {
        "now": now,
        "agent_position": agent,
        "player_position": Vec2(6, 1),
        "player_forward": Vec2(1, 0),
        "player_visible": True,
    }
    values.update(changes)
    return Observation(**values)


def test_brain_holds_without_player_evidence():
    brain = AmbusherBrain("ambusher", FakeNavigation(), FakeWorld())
    decision = brain.decide(Observation(0, Vec2(0, 0)))
    assert decision.kind == CommandKind.HOLD
    assert decision.state == AmbusherState.OBSERVE


def test_brain_selects_moves_waits_warns_and_strikes():
    brain = AmbusherBrain("ambusher", FakeNavigation(), FakeWorld())
    move = brain.decide(visible(0))
    assert move.kind == CommandKind.MOVE
    assert move.state == AmbusherState.MOVE

    wait = brain.decide(visible(1, move.target))
    assert wait.state == AmbusherState.WAIT

    warn = brain.decide(visible(2, move.target, player_reloading=True))
    assert warn.kind == CommandKind.WARN

    attack = brain.decide(visible(3, move.target, player_reloading=True))
    assert attack.kind == CommandKind.ATTACK
    assert brain.state == AmbusherState.REPOSITION


def test_failed_route_rejects_point_and_selects_alternative():
    brain = AmbusherBrain("ambusher", FakeNavigation(), FakeWorld())
    first = brain.decide(visible(0))
    reposition = brain.decide(visible(1, route_failed=True))
    replacement = brain.decide(visible(1.1))
    assert reposition.state == AmbusherState.REPOSITION
    assert first.target != replacement.target
    assert replacement.kind == CommandKind.MOVE
    assert brain.replans == 1
    assert brain.diagnostics.replans == 1
