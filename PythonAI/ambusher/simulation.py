from __future__ import annotations

import argparse
import json
from pathlib import Path

from .brain import AmbusherBrain
from .domain import Observation, Vec2
from .dynamic_astar import GraphNavigator
from .scenario_io import load_scenario


def run(path: str | Path) -> dict:
    scenario = load_scenario(path)
    navigator = GraphNavigator(scenario.graph)
    brain = AmbusherBrain("ambusher-demo", navigator, scenario.world)
    position = scenario.start
    commands = []
    timeline = [
        Observation(0.0, position, scenario.player, Vec2(0, 1), Vec2(1, 0), True),
        Observation(1.0, position, scenario.player, Vec2(0, 1), Vec2(1, 0), True),
    ]
    for observation in timeline:
        command = brain.decide(observation)
        commands.append({"time": observation.now, "state": command.state.value, "command": command.kind.value,
                         "reason": command.reason, "score": command.score})
        if command.target is not None and command.kind.value == "move":
            position = command.target
    commands.append({"time": 2.0, "state": brain.decide(Observation(
        2.0, position, scenario.player, Vec2(0, 1), Vec2(0, 0), True, True)).state.value,
                     "command": "evaluate_opportunity", "reason": "player reload event"})
    return {"commands": commands, "metrics": {"replans": brain.diagnostics.replans,
            "state_transitions": brain.diagnostics.state_transitions,
            "events": brain.diagnostics.events}}


def main() -> None:
    parser = argparse.ArgumentParser(description="Run the deterministic Ambusher AI scenario")
    parser.add_argument("scenario", nargs="?", default="PythonAI/scenarios/ambusher_city.json")
    parser.add_argument("--output")
    args = parser.parse_args()
    report = run(args.scenario)
    rendered = json.dumps(report, indent=2)
    if args.output:
        Path(args.output).write_text(rendered + "\n", encoding="utf-8")
    print(rendered)


if __name__ == "__main__":
    main()

