from PythonAI.ambusher.simulation import run


def test_game_derived_scenario_runs_deterministically():
    path = "PythonAI/scenarios/ambusher_city.json"
    report = run(path)
    assert report == run(path)
    assert [item["state"] for item in report["commands"]] == ["move", "move", "wait"]
    assert report["commands"][0]["reason"] == "weighted tactical selection"
    assert report["metrics"]["replans"] == 0
    assert report["metrics"]["state_transitions"] == 3
    assert report["metrics"]["events"]

