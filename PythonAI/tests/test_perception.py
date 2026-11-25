from PythonAI.ambusher.domain import Observation, Vec2
from PythonAI.ambusher.perception import PerceptionMemory


def test_hidden_player_movement_does_not_update_visual_memory():
    memory = PerceptionMemory(5)
    seen = memory.update(Observation(
        0, Vec2(0, 0), Vec2(2, 2), Vec2(1, 0), Vec2(3, 0), True, True
    ))
    assert seen.position == Vec2(2, 2)
    assert seen.forward == Vec2(1, 0)
    assert seen.velocity == Vec2(3, 0)
    assert seen.reloading and seen.visible

    hidden = memory.update(Observation(
        1, Vec2(0, 0), Vec2(9, 9), Vec2(-1, 0), Vec2(8, 0), False, True
    ))
    assert hidden.position == Vec2(2, 2)
    assert hidden.forward == Vec2(1, 0)
    assert hidden.velocity == Vec2(3, 0)
    assert not hidden.reloading and not hidden.visible


def test_sound_reveals_position_without_tactical_facts():
    memory = PerceptionMemory(5)
    heard = memory.update(Observation(2, Vec2(0, 0), sound_position=Vec2(4, 4)))
    assert heard.position == Vec2(4, 4)
    assert heard.forward == Vec2(0, 0)
    assert heard.velocity == Vec2(0, 0)
    assert not heard.reloading and not heard.visible


def test_stale_evidence_expires_and_memory_can_be_cleared():
    memory = PerceptionMemory(5)
    memory.update(Observation(0, Vec2(0, 0), Vec2(2, 2), player_visible=True))
    assert memory.update(Observation(5, Vec2(0, 0))) is not None
    assert memory.update(Observation(5.01, Vec2(0, 0))) is None

    memory.update(Observation(6, Vec2(0, 0), sound_position=Vec2(4, 4)))
    memory.clear()
    assert memory.update(Observation(6.1, Vec2(0, 0))) is None

