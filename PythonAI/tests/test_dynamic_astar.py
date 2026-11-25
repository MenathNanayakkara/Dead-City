from PythonAI.ambusher.domain import Vec2
from PythonAI.ambusher.dynamic_astar import DynamicAStar, DynamicGraph


def make_graph():
    return DynamicGraph(
        {
            "a": Vec2(0.0, 0.0),
            "b": Vec2(3.0, 0.0),
            "c": Vec2(6.0, 0.0),
            "d": Vec2(3.0, 4.0),
        },
        {
            "a": {"b": 3.0, "d": 5.0},
            "b": {"a": 3.0, "c": 3.0},
            "c": {"b": 3.0, "d": 5.0},
            "d": {"a": 5.0, "c": 5.0},
        },
    )


def test_astar_uses_lowest_cost_path_and_replans():
    graph = make_graph()
    search = DynamicAStar(graph)
    assert search.search("a", "c").nodes == ("a", "b", "c")
    graph.set_blocked("b", "c")
    result = search.search("a", "c")
    assert result.nodes == ("a", "d", "c")
    assert result.graph_version == 1


def test_danger_cost_changes_route_without_blocking_edge():
    graph = make_graph()
    graph.set_danger("b", "c", 10)
    assert DynamicAStar(graph).search("a", "c").nodes == ("a", "d", "c")


def test_unreachable_and_unknown_goals_return_no_route():
    graph = make_graph()
    graph.set_blocked("a", "b")
    graph.set_blocked("a", "d")
    search = DynamicAStar(graph)
    assert search.search("a", "c") is None
    assert search.search("a", "missing") is None


def test_negative_danger_cost_is_rejected():
    graph = make_graph()
    try:
        graph.set_danger("a", "b", -1.0)
        assert False, "negative danger costs would break A* assumptions"
    except ValueError:
        pass

