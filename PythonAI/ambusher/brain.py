from __future__ import annotations

from .config import AmbusherConfig
from .domain import AmbusherState, CommandKind, DecisionCommand, Diagnostics, Observation, Vec2
from .perception import PerceptionMemory
from .ports import NavigationPort, TacticalWorldPort
from .tactical_search import TacticalSearch


class AmbusherBrain:
    def __init__(self, owner_id: str, navigation: NavigationPort, world: TacticalWorldPort,
                 config: AmbusherConfig | None = None):
        self.owner_id = owner_id
        self.navigation = navigation
        self.world = world
        self.config = config or AmbusherConfig()
        self.memory = PerceptionMemory(self.config.memory_seconds)
        self.search = TacticalSearch(self.config)
        self.state = AmbusherState.OBSERVE
        self.state_until = 0.0
        self.selected_id: str | None = None
        self.selected_position: Vec2 | None = None
        self.rejected_until: dict[str, float] = {}
        self.replans = 0
        self.diagnostics = Diagnostics()

    def _transition(self, state: AmbusherState, now: float, duration: float, reason: str) -> None:
        if state != self.state:
            self.diagnostics.state_transitions += 1
        self.state = state
        self.state_until = now + duration
        self.diagnostics.record(f"{now:.2f}: {state.value} - {reason}")

    def decide(self, observation: Observation) -> DecisionCommand:
        now = observation.now
        memory = self.memory.update(observation)
        if memory is None:
            self._transition(AmbusherState.OBSERVE, now, 0.75, "no current evidence")
            return DecisionCommand(CommandKind.HOLD, self.state, reason="listen and scan")

        if self.state == AmbusherState.OBSERVE and now >= self.state_until:
            self._transition(AmbusherState.EVALUATE, now, 0.0, "evidence ready")

        if self.state == AmbusherState.EVALUATE:
            scored = self.search.choose(
                self.owner_id, observation.agent_position, memory.position, memory.forward,
                memory.velocity, now, self.navigation, self.world, set(self.rejected_until),
            )
            if scored is None or not self.world.reserve(
                scored.point.point_id, self.owner_id, now,
                self.config.move_timeout + self.config.wait_seconds,
            ):
                self._transition(AmbusherState.STRIKE, now, 4.0, "no safe tactical point")
            else:
                self.selected_id = scored.point.point_id
                self.selected_position = scored.point.position
                self._transition(AmbusherState.MOVE, now, self.config.move_timeout,
                                 "best tactical point reserved")
                route = self.navigation.route(observation.agent_position, scored.point.position) or ()
                return DecisionCommand(CommandKind.MOVE, self.state, scored.point.position,
                                       "weighted tactical selection", scored.score, tuple(route))

        return DecisionCommand(CommandKind.HOLD, self.state, reason="decision settled")

    def shutdown(self) -> None:
        self.world.release(self.owner_id)
        self.memory.clear()
