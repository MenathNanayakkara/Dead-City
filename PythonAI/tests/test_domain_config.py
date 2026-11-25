from PythonAI.ambusher.config import AmbusherConfig
from PythonAI.ambusher.domain import Observation, TacticalPoint, Vec2


def test_vector_distance_normalization_and_dot_product():
    vector = Vec2(3.0, 4.0)
    assert vector.distance_to(Vec2(0.0, 0.0)) == 5.0
    assert vector.normalized() == Vec2(0.6, 0.8)
    assert vector.dot(Vec2(2.0, 0.0)) == 6.0


def test_vector_arithmetic_is_immutable():
    original = Vec2(2.0, 3.0)
    assert original + Vec2(1.0, -1.0) == Vec2(3.0, 2.0)
    assert original - Vec2(1.0, 1.0) == Vec2(1.0, 2.0)
    assert original * 2.0 == Vec2(4.0, 6.0)
    assert original == Vec2(2.0, 3.0)


def test_observation_and_tactical_point_are_immutable():
    observation = Observation(1.0, Vec2(0.0, 0.0), player_visible=True)
    point = TacticalPoint("shadow", Vec2(5.0, 4.0), concealed=True)
    for value, attribute in ((observation, "now"), (point, "point_id")):
        try:
            setattr(value, attribute, None)
            assert False, "frozen domain values must reject mutation"
        except (AttributeError, TypeError):
            pass


def test_configuration_accepts_normalized_weights():
    config = AmbusherConfig()
    assert config.flank_weight + config.concealment_weight + config.travel_weight + \
        config.intercept_weight + config.proximity_weight == 1.0


def test_configuration_rejects_invalid_values():
    invalid = (
        {"flank_weight": -0.1, "concealment_weight": 0.65},
        {"flank_weight": 0.5},
        {"warning_seconds": 0.0},
        {"maximum_replans": -1},
    )
    for values in invalid:
        try:
            AmbusherConfig(**values)
            assert False, f"invalid configuration accepted: {values}"
        except ValueError:
            pass
