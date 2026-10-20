# Ambusher Python AI - Technical Defense

## Ownership and scope

This package implements the decision-making architecture for the Ambusher agent only. Unity remains responsible for rendering, animation, collision, and final movement. The Python package owns bounded perception memory, tactical evaluation, dynamic A* routing, reservations, state transitions, diagnostics, and deterministic tests.

## Architecture

The implementation separates domain data, ports, algorithms, and adapters:

1. `domain.py` contains immutable observations, commands, states, vectors, and diagnostics.
2. `ports.py` defines navigation and tactical-world contracts without Unity dependencies.
3. `perception.py` prevents omniscient tracking by retaining only visible or audible evidence for a bounded duration.
4. `dynamic_astar.py` implements informed A* search with `f(n) = g(n) + h(n)`. Runtime edge blocking and danger costs increment the graph version and cause the next request to replan.
5. `tactical_search.py` evaluates reachable candidates using flank, concealment, travel, interception, and preferred-distance utility.
6. `reservations.py` prevents multiple agents from selecting the same hiding location and expires abandoned claims.
7. `brain.py` coordinates Observe, Evaluate, Move, Wait, Warn, Strike, and Reposition states with explicit exits and bounded retries.

## Intelligent behavior

The Ambusher predicts the player's short-term position from observed velocity. It favors concealed rear or side approaches, but only when A* confirms a reachable route. It waits rather than attacking blindly, strikes when the player looks away or reloads, provides a warning window, and relocates when exposed. Failed routes and stale hiding places are temporarily rejected to prevent loops.

## Edge cases

- Stale visual information expires and unseen movement does not update memory.
- Sound creates evidence without revealing forward direction, velocity, or reload state.
- Unreachable points never enter tactical scoring.
- Dynamic obstacles and danger costs alter A* routes.
- A bounded replan budget prevents infinite retry loops.
- Reservations are released on strike, failure, shutdown, or expiry.
- Ties are deterministic so tests and demonstrations are repeatable.

## Running the evidence

```powershell
python -m pytest
python -m PythonAI.ambusher.simulation PythonAI/scenarios/ambusher_city.json --output Documentation/AMBUSHER_SIMULATION_REPORT.json
```

The simulation is a reproducible game-derived demonstration, not a substitute for Unity playtesting. `PythonAmbusherBridge.cs` is an optional JSON-lines adapter for a live editor demonstration. It is disabled by default because shipping a Python runtime with Unity requires separate deployment and security decisions.

## Assessment evidence

The tests cover informed path search, dynamic route changes, tactical ranking, perception limits, exclusive reservations, state transitions, failed-route recovery, cleanup, and deterministic scenario execution. Diagnostic events expose the reason for every transition, while metrics count replans and state changes.

