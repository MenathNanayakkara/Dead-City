"""JSON-lines process used by Unity's optional PythonAmbusherBridge."""

from __future__ import annotations

import json
import sys

from .brain import AmbusherBrain
from .domain import Observation, Vec2
from .dynamic_astar import GraphNavigator
from .scenario_io import load_scenario


def _vec(data) -> Vec2:
    return Vec2(float(data[0]), float(data[1]))


def main() -> None:
    scenario = load_scenario("PythonAI/scenarios/ambusher_city.json")
    brain = AmbusherBrain("unity-ambusher", GraphNavigator(scenario.graph), scenario.world)
    for line in sys.stdin:
        try:
            data = json.loads(line)
            command = brain.decide(Observation(
                now=float(data["now"]), agent_position=_vec(data["agent"]),
                player_position=_vec(data["player"]) if data.get("player") else None,
                player_forward=_vec(data.get("forward", [0, 1])),
                player_velocity=_vec(data.get("velocity", [0, 0])),
                player_visible=bool(data.get("visible")), player_reloading=bool(data.get("reloading")),
                sound_position=_vec(data["sound"]) if data.get("sound") else None,
                route_failed=bool(data.get("route_failed")), attack_ready=bool(data.get("attack_ready", True))))
            target = None if command.target is None else [command.target.x, command.target.y]
            print(json.dumps({"command": command.kind.value, "state": command.state.value,
                              "target": target, "reason": command.reason}), flush=True)
        except Exception as error:
            print(json.dumps({"error": str(error)}), flush=True)


if __name__ == "__main__":
    main()

