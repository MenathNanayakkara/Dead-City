# Ambusher AI - Technical Defense

## 1. Purpose

The Ambusher is a tactical enemy. It does not simply run directly toward the player.

Its goal is to:

1. find the player through sight or sound;
2. choose a useful cover or flanking position;
3. reach that position safely;
4. wait for the player to become vulnerable;
5. warn and attack;
6. relocate when its plan fails or the player discovers it.

Unity handles movement, animation, collision, and combat effects. The Python system chooses the Ambusher's actions and returns commands such as `move`, `face`, `warn`, `attack`, and `investigate`.

## 2. Architecture

The Ambusher uses a hybrid architecture:

- a finite-state machine controls the order and timing of actions;
- utility scoring compares tactical locations;
- A* finds a reachable route;
- perception memory stores limited visual and sound evidence;
- reservations stop agents from selecting the same hiding place.

This design was chosen because no single technique solves the complete gameplay problem.

A state machine is useful for clear behavior stages, but it cannot compare several hiding places. Utility scoring can compare locations, but it cannot control when the agent should wait, warn, attack, or relocate. A* can find a route, but it cannot decide whether the destination is tactically useful. Combining them produces behavior that is understandable, testable, and responsive.

The code also uses small navigation and world interfaces. This keeps the decision logic independent from Unity and allows the same brain to run in unit tests, a deterministic simulation, or the optional Unity bridge.

## 3. Decision states

The main behavior is implemented in `PythonAI/ambusher/brain.py`.

| State | Purpose |
|---|---|
| Observe | Look and listen for reliable evidence. |
| Evaluate | Compare reachable tactical positions. |
| Move | Follow the selected route. |
| Wait | Hide and watch for an opportunity. |
| Warn | Give the player a short reaction window. |
| Strike | Attack or close in on the last known position. |
| Reposition | Abandon a failed or exposed location and try another. |

These states make the behavior easy to follow while still allowing the agent to react to changes during play.

## 4. Tactical scoring

Every valid cover or ambush point receives a utility score:

```text
U = 0.30F + 0.25C + 0.15T + 0.15I + 0.15P
```

Where:

- `F` = flank quality;
- `C` = concealment;
- `T` = travel efficiency;
- `I` = interception quality;
- `P` = preferred-distance quality.

The values add up to `1.0`, so each factor has a clear contribution.

| Factor | Value | Reason |
|---|---:|---|
| Flanking | 0.30 | Approaching from the side or rear defines the Ambusher. |
| Concealment | 0.25 | A good hiding position should reduce exposure. |
| Travel | 0.15 | The route should be practical, but the nearest point should not always win. |
| Interception | 0.15 | The enemy should react to where the player is moving. |
| Proximity | 0.15 | The position should support the intended attack distance. |

Flanking and concealment receive the largest values because they create the Ambusher's identity. The remaining factors prevent long detours and poor attack positions.

### Flanking logic

```text
F = (1 - dot(playerForward, directionToPoint)) / 2
```

This produces an approximate value of:

- `0.0` for a point in front of the player;
- `0.5` for a point at the side;
- `1.0` for a point behind the player.

An open point is rejected when its flank value is below `0.65`. This prevents the Ambusher from choosing an exposed location directly in front of the player.

### Player prediction

```text
predictedPosition = playerPosition + playerVelocity * 1.5 seconds
```

The Ambusher uses this short prediction when evaluating interception positions. It therefore reacts to the player's movement instead of only following an old position.

### Distance calculations

```text
T = 1 - clamp(pathCost / 40, 0, 1)
I = 1 - clamp(distance(point, predictedPosition) / 30, 0, 1)
P = 1 - clamp(abs(distance(point, player) - 8) / 25, 0, 1)
```

These formulas convert travel and distance measurements into comparable values. Eight world units is the preferred tactical distance.

If two points have equal scores, the Ambusher selects the one with the lower route cost and then uses point ID as a stable final tie-breaker. This keeps tests and demonstrations repeatable.

## 5. Pathfinding

The navigation graph contains:

- nodes representing useful city positions;
- edges representing connections and travel costs;
- blocked edges representing temporary obstacles;
- danger costs representing risky routes.

A* uses:

```text
f(n) = g(n) + h(n)
```

`g(n)` is the route cost already travelled. `h(n)` is the straight-line distance to the destination.

Breadth-first search was not suitable because city routes do not all have the same cost. Dijkstra's algorithm supports different costs but has no directional guidance. A* uses both real route cost and a goal-directed heuristic, making it suitable for responsive game navigation.

When an edge becomes blocked or dangerous, the graph changes and the next route request can select another path. Unreachable tactical points are removed before scoring.

## 6. Inventive and thematic behavior

The Ambusher changes its tactics according to the player's actions:

- Sight provides position, facing direction, velocity, and reload information.
- Sound provides a location clue without revealing hidden direction, velocity, or reload information.
- If the player looks toward the hiding place, the Ambusher relocates.
- If the player looks away or reloads within range, the Ambusher prepares to strike.
- A short warning occurs before the attack, making the encounter tense but fair.
- After attacking or losing a route, the Ambusher chooses a different position.

This behavior feels intelligent without giving the enemy unfair knowledge. The player can influence the Ambusher by changing direction, exposing its cover, making noise, or creating a route obstruction.

## 7. Advanced edge-case handling

| Problem | Response |
|---|---|
| No evidence is available | Remain in Observe and continue scanning. |
| Visual evidence becomes old | Forget it instead of tracking the player forever. |
| Only a sound is detected | Investigate the sound without receiving hidden player data. |
| A tactical point is unreachable | Remove it before scoring. |
| An open point is not a safe flank | Reject it. |
| Another agent owns the point | Choose another available point. |
| A reservation expires | Make the point available again. |
| A route fails or times out | Release the point, reject it temporarily, and replan. |
| A failed point could be repeatedly selected | Apply a five-second cooldown. |
| Replanning repeatedly fails | Stop after the configured retry limit. |
| The player discovers the hiding place | Enter Reposition and choose another location. |
| The attack is unavailable or out of range | Continue moving or investigating. |
| The agent shuts down | Release reservations and clear memory. |

These responses prevent infinite loops, invalid attacks, stale reservations, unfair tracking, and repeated selection of bad positions.

## 8. Main files

| File | Responsibility |
|---|---|
| `domain.py` | Observations, states, commands, vectors, tactical points, and diagnostics. |
| `config.py` | Scoring values, distances, durations, and retry limits. |
| `ports.py` | Navigation and tactical-world interfaces. |
| `perception.py` | Limited visual and sound memory. |
| `dynamic_astar.py` | Navigation graph, A*, blocking, danger costs, and route reconstruction. |
| `tactical_search.py` | Prediction, scoring, filtering, and tactical-point selection. |
| `reservations.py` | Exclusive point ownership, expiry, and cleanup. |
| `brain.py` | State transitions and final commands. |
| `scenario_io.py` | Scenario loading and test-world support. |
| `simulation.py` | Reproducible decision timeline and metrics. |
| `service.py` | Communication with the optional Unity bridge. |

Separating these responsibilities keeps the code understandable and allows each part to be tested independently.

## 9. Live code navigation

Open these files in this order during the technical defense:

1. `PythonAI/ambusher/brain.py` - show the state sequence and reactions.
2. `PythonAI/ambusher/tactical_search.py` - show filtering and the utility formulas.
3. `PythonAI/ambusher/config.py` - show the scoring values and timing settings.
4. `PythonAI/ambusher/dynamic_astar.py` - show the frontier, heuristic, danger, and blocked edges.
5. `PythonAI/ambusher/perception.py` - show limited sight and sound memory.
6. `PythonAI/ambusher/reservations.py` - show ownership, expiry, and release.
7. `PythonAI/tests/test_edge_cases.py` - show failure recovery and cleanup.
8. `PythonAI/tests/test_simulation.py` - show deterministic end-to-end behavior.

Important functions to remember:

- `AmbusherBrain.decide()` - complete decision process;
- `TacticalSearch.choose()` - candidate filtering and selection;
- `score_flank_concealment_travel()` - primary tactical score;
- `score_interception_proximity()` - prediction and distance score;
- `DynamicAStar._explore()` - A* search loop;
- `PerceptionMemory.update()` - evidence and memory rules.

## 10. Final defense

The Ambusher clearly surpasses basic search because it does more than find a path. It observes limited evidence, predicts movement, compares tactical locations mathematically, verifies reachability, reserves its choice, reacts to player behavior, handles route failures, and relocates when exposed.

The result is a thematic enemy that is challenging but fair. Its behavior is explainable, its calculations are justified, its failure cases are controlled, and its architecture can be demonstrated quickly in code.
