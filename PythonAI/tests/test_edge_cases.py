from PythonAI.ambusher.brain import AmbusherBrain
from PythonAI.ambusher.domain import AmbusherState, Observation, Vec2
from PythonAI.tests.helpers import setup_world


def test_route_failure_rejects_point_and_repositions():
    _, navigation, world = setup_world()
    brain = AmbusherBrain("ambusher", navigation, world)
    brain.decide(Observation(0, Vec2(0, 0), Vec2(6, 1), Vec2(1, 0), player_visible=True))
    brain.decide(Observation(1, Vec2(0, 0), Vec2(6, 1), Vec2(1, 0), player_visible=True))
    brain.decide(Observation(2, Vec2(0, 0), Vec2(6, 1), Vec2(1, 0), player_visible=True,
                             route_failed=True))
    assert brain.state == AmbusherState.REPOSITION
    assert brain.rejected_until


def test_shutdown_releases_reservation():
    _, navigation, world = setup_world()
    brain = AmbusherBrain("ambusher", navigation, world)
    brain.decide(Observation(0, Vec2(0, 0), Vec2(6, 1), Vec2(1, 0), player_visible=True))
    brain.decide(Observation(1, Vec2(0, 0), Vec2(6, 1), Vec2(1, 0), player_visible=True))
    selected = brain.selected_id
    brain.shutdown()
    assert world.is_available(selected, "other", 1)


def test_repeated_route_failures_end_in_bounded_strike_state():
    _, navigation, world = setup_world()
    brain = AmbusherBrain("ambusher", navigation, world)
    visible = {
        "agent_position": Vec2(0, 0),
        "player_position": Vec2(6, 1),
        "player_forward": Vec2(1, 0),
        "player_visible": True,
    }
    brain.decide(Observation(now=0, **visible))
    brain.decide(Observation(now=1, route_failed=True, **visible))
    brain.decide(Observation(now=1.1, **visible))
    brain.decide(Observation(now=2, route_failed=True, **visible))
    brain.decide(Observation(now=2.1, **visible))
    assert brain.state == AmbusherState.STRIKE
    assert brain.replans == 2

