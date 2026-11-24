# Ambusher AI - Technical Defense

## 1. What the Ambusher does

The Ambusher is designed to create tension, not simply chase the player by the shortest route.

Its gameplay problem is:

> How can an enemy reach a useful hiding place, stay hidden, wait for a fair opportunity, attack, and then relocate without cheating or becoming stuck?

The solution combines:

- a finite-state decision loop;
- utility-based tactical scoring;
- dynamic A* pathfinding over weighted traversal costs;
- limited visual and sound memory;
- player-position prediction;
- cover and flanking evaluation;
- tactical-point reservations;
- bounded replanning and failed-position cooldowns.

Unity handles animation, collision, rendering, and physical movement. The Python package owns the Ambusher's decisions and returns commands such as `move`, `face`, `warn`, `attack`, or `investigate`.

## 2. Why this architecture was chosen

No single AI technique solves the whole gameplay problem. The Ambusher therefore uses a hybrid architecture.

### Why not a simple state machine by itself?

A state machine gives a clear sequence such as Observe, Move, Wait, and Strike, but it cannot intelligently compare several hiding places. A fixed state machine would repeatedly choose a hard-coded location.

The Ambusher uses states to control *when* it acts and utility scoring to decide *where* it should go.

### Why not utility scoring by itself?

Utility scoring can rank choices, but it does not manage timing. The Ambusher needs to wait, warn the player, abandon exposed cover, and stop retrying failed routes. Explicit states make those transitions understandable, testable, and safe.

### Why A* instead of breadth-first search or Dijkstra's algorithm?

Breadth-first search assumes equal edge costs, but city routes have different travel and danger costs. Dijkstra's algorithm supports different costs but searches without guidance toward the destination. A* adds a distance heuristic, so it remains cost-aware while normally expanding fewer irrelevant nodes.

### Why ports and adapters?

The decision code depends on small navigation and world interfaces rather than directly depending on Unity. This makes the same brain deterministic in unit tests, the simulation, and the optional Unity bridge.

## 3. Decision process

The main state loop is implemented in `PythonAI/ambusher/brain.py`.

1. **Observe** - look and listen for reliable player evidence.
2. **Evaluate** - score reachable cover and ambush points.
3. **Move** - follow the selected dynamic A* route.
4. **Wait** - remain concealed and watch for an opportunity.
5. **Warn** - provide a short, fair warning before the attack.
6. **Strike** - attack only when the target is visible, close enough, and the attack is ready.
7. **Reposition** - abandon an exposed or failed location and choose another one.

This produces readable behavior while still allowing decisions to change when the game state changes.

## 4. Utility-based tactical scoring

Every valid tactical point receives a utility score between roughly zero and one. A larger score means the location is more useful for an ambush.

The implemented score is:

```text
U = 0.30F + 0.25C + 0.15T + 0.15I + 0.15P
```

Where:

- `F` is flank quality;
- `C` is concealment;
- `T` is affordable travel cost;
- `I` is interception quality;
- `P` is preferred-distance quality.

The weights sum to exactly `1.0`, which keeps the score interpretable and allows configuration validation.

### Why these weights?

| Factor | Weight | Gameplay reason |
|---|---:|---|
| Flanking | 0.30 | The defining Ambusher behavior is approaching from behind or from the side. |
| Concealment | 0.25 | A hidden location is valuable, but concealment alone must not create passive camping. |
| Travel | 0.15 | Shorter routes matter, but the nearest point should not automatically win. |
| Interception | 0.15 | The agent should meet where the player is going, not only where the player was. |
| Proximity | 0.15 | The point should support the intended attack distance without forcing an immediate rush. |

Flanking and concealment receive 55% of the total score because they create the enemy's identity. Travel, interception, and proximity balance tactical quality against practical movement.

### Flank calculation

```text
F = (1 - dot(playerForward, directionToPoint)) / 2
```

The dot product is near `1` in front of the player, `0` at the side, and `-1` behind the player. Converting it with the formula above gives approximately:

- front: `0.0`;
- side: `0.5`;
- rear: `1.0`.

An exposed point is rejected when its flank value is below `0.65`. Therefore, open positions must be meaningfully behind or beside the player.

### Prediction and interception

The short-term predicted position is:

```text
predictedPosition = playerPosition + playerVelocity * 1.5 seconds
```

This makes the Ambusher select a point based on the player's movement rather than blindly following the player's current position.

### Travel, interception, and proximity normalization

```text
T = 1 - clamp(pathCost / 40, 0, 1)
I = 1 - clamp(distance(point, predictedPosition) / 30, 0, 1)
P = 1 - clamp(abs(distance(point, player) - 8) / 25, 0, 1)
```

These formulas turn different measurements into comparable values. The preferred distance is eight world units, encouraging a useful ambush range rather than selecting the closest possible position.

If two candidates have equal scores, the system uses lower path cost and then stable point ID. This deterministic tie-breaking makes repeated simulations and tests produce the same result.

## 5. Pathfinding and dynamic search

The navigation graph contains:

- nodes representing meaningful city positions;
- weighted edges representing travel cost;
- blocked edges representing temporary obstacles;
- danger values representing risky routes;
- a graph version that changes whenever navigation conditions change.

The effective edge cost is:

```text
edgeCost = baseTravelCost + dangerCost
```

The A* priority is:

```text
f(n) = g(n) + h(n)
```

Where `g(n)` is the accumulated weighted route cost and `h(n)` is Euclidean distance to the goal. A blocked edge is not expanded at all. When an edge becomes blocked or dangerous, the next route request uses the new graph state and can select a different path.

### Important terminology

This implementation is **dynamic A* over weighted traversal costs**. Its graph edges are weighted, but its heuristic multiplier is `1.0`. Therefore, it should not be described as formal Weighted A*, which normally uses `f(n) = g(n) + w*h(n)` with `w > 1`.

## 6. Inventive and thematic behavior

The Ambusher changes tactics according to player behavior:

- If it sees the player, it remembers position, facing direction, velocity, and reload state.
- If it only hears the player, it receives a location clue but does not magically learn direction, velocity, or reload state.
- If the player looks toward its hiding place, it abandons that position and relocates.
- If the player looks away or reloads within range, it starts the ambush.
- Before striking, it enters a short warning state so the encounter remains tense but fair.
- After attacking, being exposed, or losing a route, it repositions instead of repeating the same action.

The sound behavior is intentionally non-omniscient. Sound creates evidence to investigate; it does not reveal hidden player information. This makes the agent feel intelligent without cheating.

## 7. Advanced edge-case handling

| Situation | Response |
|---|---|
| No current visual or sound evidence | Hold position and continue observing. |
| Old evidence exceeds the memory duration | Forget it rather than tracking the unseen player. |
| Tactical point is unreachable | Exclude it before utility ranking. |
| Open point is not a strong flank | Reject it as unsafe. |
| Another agent owns the point | Select a different available point. |
| Reservation expires | Automatically make the point available again. |
| Route fails or times out | Release the point, temporarily reject it, and replan. |
| Same bad point could be chosen repeatedly | Apply a five-second rejection cooldown. |
| Replanning repeatedly fails | Stop after the configured retry budget and enter Strike behavior. |
| Player exposes the hiding position | Reposition dynamically. |
| Attack is not ready or target is too far away | Continue closing or investigating instead of issuing an invalid attack. |
| Agent shuts down or dies | Release its reservation and clear perception memory. |
| Candidate scores are tied | Use path cost and point ID for a deterministic result. |

These cases prevent infinite loops, stale reservations, omniscient tracking, invalid attacks, and nondeterministic demonstrations.

## 8. Clean architecture and file responsibilities

| File | Responsibility |
|---|---|
| `domain.py` | Immutable observations, commands, states, vectors, tactical points, and diagnostics. |
| `config.py` | Validated tactical weights, distances, durations, and retry limits. |
| `ports.py` | Navigation and tactical-world interfaces. |
| `perception.py` | Bounded visual and sound evidence. |
| `dynamic_astar.py` | Dynamic graph, A* search, blocked edges, danger costs, and route reconstruction. |
| `tactical_search.py` | Prediction, utility formulas, candidate filtering, and deterministic ranking. |
| `reservations.py` | Exclusive tactical-point ownership, expiry, reassignment, and cleanup. |
| `brain.py` | State transitions and final gameplay commands. |
| `scenario_io.py` | Validated scenario loading and the test-world adapter. |
| `simulation.py` | Reproducible command timeline and diagnostic report. |
| `service.py` | JSON-lines interface used by the optional Unity bridge. |

This separation allows each algorithm to be tested without requiring a running Unity scene.

## 9. Live code navigation guide

During the technical defense, open these files in this order:

1. `PythonAI/ambusher/brain.py` - show the complete decision loop and state transitions.
2. `PythonAI/ambusher/tactical_search.py` - show the tactical filters and utility calculation.
3. `PythonAI/ambusher/config.py` - show and justify the five weights and timing values.
4. `PythonAI/ambusher/dynamic_astar.py` - show `g + h`, danger costs, blocking, and reconstruction.
5. `PythonAI/ambusher/perception.py` - show that unseen information is not updated.
6. `PythonAI/ambusher/reservations.py` - show exclusivity, expiry, and release.
7. `PythonAI/tests/test_edge_cases.py` - show route recovery, bounded retries, and shutdown cleanup.
8. `PythonAI/tests/test_simulation.py` - show deterministic end-to-end behavior.

Useful locations to remember:

- utility formula: `tactical_search.py`, `score_flank_concealment_travel()` and `score_interception_proximity()`;
- candidate filtering: `tactical_search.py`, `TacticalSearch.choose()`;
- A* frontier: `dynamic_astar.py`, `DynamicAStar._explore()`;
- state transitions: `brain.py`, `AmbusherBrain.decide()`;
- perception limits: `perception.py`, `PerceptionMemory.update()`;
- retry protection: `brain.py`, the `MOVE` and `REPOSITION` branches.

## 10. Demonstration and evidence

Run all automated tests:

```powershell
python -m pytest
```

Run the deterministic city scenario:

```powershell
python -m PythonAI.ambusher.simulation PythonAI/scenarios/ambusher_city.json --output Documentation/AMBUSHER_SIMULATION_REPORT.json
```

The tests cover:

- vector, configuration, and domain validation;
- A* route construction;
- blocked and dangerous route changes;
- unreachable destinations;
- visual and sound memory limits;
- tactical ranking and deterministic ties;
- reservation exclusivity, expiry, and cleanup;
- the full Observe-to-Strike state sequence;
- route-failure recovery and bounded retries;
- deterministic end-to-end simulation.

The simulation report records commands, reasons, scores, replans, state-transition counts, and diagnostic events. This explains *why* the Ambusher made each decision instead of presenting it as a black box.

## 11. Likely defense questions

### Why is this more advanced than basic search?

Basic search only finds a path to a fixed goal. This agent first predicts the player, filters unsafe and unavailable points, calculates tactical utility, confirms reachability with A*, reserves its choice, reacts to changing routes and player behavior, and then controls the attack through explicit states.

### Why is flanking weighted more heavily than travel distance?

Choosing the nearest point would produce a normal chasing enemy. A larger flank weight makes the selected position match the Ambusher theme, while the travel term prevents unreasonable detours.

### How do you prevent the AI from cheating?

Only visible observations contain direction, velocity, and reload information. Sound supplies a location clue only. Evidence expires after a fixed duration, so the agent cannot track unseen movement forever.

### What happens if the environment changes?

Blocked edges and danger costs modify the graph. The next navigation request evaluates the changed costs. A failed current route releases and rejects the tactical point before the brain enters Reposition.

### How do you prevent infinite loops?

Failed points receive a cooldown, movement has a timeout, and replanning has a maximum attempt count. Reservations also expire automatically.

### Why include a warning before the attack?

The warning makes the encounter readable and fair. The player receives a short reaction window, while the Ambusher still benefits from creating a strong tactical position.

### Is this formal Weighted A*?

No. It is A* on a dynamically weighted graph. The edge costs include travel and danger, while the heuristic weight remains `1.0`. This distinction is important during a technical defense.

## 12. Final defense statement

The Ambusher is not a shortest-path zombie. It is a tactical enemy that observes limited evidence, predicts movement, compares hiding places mathematically, confirms that its choice is reachable, waits for player vulnerability, warns before attacking, and relocates when its plan fails.

The architecture was selected for gameplay reasons: states provide readable timing, utility scoring creates tactical choice, A* provides efficient cost-aware routing, perception memory prevents cheating, and reservations plus bounded retries handle multiplayer-style contention and dynamic failures. Together these systems create an enemy that is thematic, explainable, testable, and significantly more advanced than basic uninformed search.
