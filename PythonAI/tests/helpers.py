from PythonAI.ambusher.domain import TacticalPoint, Vec2
from PythonAI.ambusher.dynamic_astar import DynamicGraph, GraphNavigator
from PythonAI.ambusher.reservations import ReservationTable
from PythonAI.ambusher.scenario_io import ScenarioWorld


def setup_world():
    graph = DynamicGraph(
        {"a": Vec2(0, 0), "b": Vec2(3, 0), "c": Vec2(6, 0), "d": Vec2(3, 4)},
        {"a": {"b": 3, "d": 5}, "b": {"a": 3, "c": 3},
         "c": {"b": 3, "d": 5}, "d": {"a": 5, "c": 5}},
    )
    points = (TacticalPoint("rear", Vec2(6, 0), True), TacticalPoint("side", Vec2(3, 4), False))
    return graph, GraphNavigator(graph), ScenarioWorld(points, ReservationTable())

