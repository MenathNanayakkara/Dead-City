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
        if state not in {AmbusherState.MOVE, AmbusherState.WAIT}:
            self.world.release(self.owner_id)
            self.selected_id = None
            self.selected_position = None
        if state != self.state:
            self.diagnostics.state_transitions += 1
        self.state = state
        self.state_until = now + duration
        self.diagnostics.record(f"{now:.2f}: {state.value} - {reason}")

    def _reject_selected(self, now: float) -> None:
        if self.selected_id is not None:
            self.rejected_until[self.selected_id] = now + self.config.rejection_seconds
        self.world.release(self.owner_id)
        self.selected_id = None
        self.selected_position = None

    def decide(self, observation: Observation) -> DecisionCommand:
        now = observation.now
        memory = self.memory.update(observation)
        self.rejected_until = {
            key: expiry for key, expiry in self.rejected_until.items() if expiry > now
        }
        if memory is None:
            self._transition(AmbusherState.OBSERVE, now, 0.75, "no current evidence")
            return DecisionCommand(CommandKind.HOLD, self.state, reason="listen and scan")

        if self.state == AmbusherState.OBSERVE and now >= self.state_until:
            self._transition(AmbusherState.EVALUATE, now, 0.0, "evidence ready")

        if self.state in {AmbusherState.EVALUATE, AmbusherState.REPOSITION}:
            evaluating_fresh = self.state == AmbusherState.EVALUATE
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
                if evaluating_fresh:
                    self.replans = 0
                self._transition(AmbusherState.MOVE, now, self.config.move_timeout,
                                 "best tactical point reserved")
                route = self.navigation.route(observation.agent_position, scored.point.position) or ()
                return DecisionCommand(CommandKind.MOVE, self.state, scored.point.position,
                                       "weighted tactical selection", scored.score, tuple(route))

        if self.state == AmbusherState.MOVE:
            if self.selected_position is None:
                self._transition(AmbusherState.REPOSITION, now, 0.0, "reservation lost")
            elif observation.route_failed or now >= self.state_until:
                self.replans += 1
                self.diagnostics.replans += 1
                self._reject_selected(now)
                if self.replans > self.config.maximum_replans:
                    self._transition(AmbusherState.STRIKE, now, 4.0, "replan budget exhausted")
                else:
                    self._transition(AmbusherState.REPOSITION, now, 0.0, "route invalidated")
            elif observation.agent_position.distance_to(
                self.selected_position
            ) <= self.config.arrival_distance:
                self._transition(AmbusherState.WAIT, now, self.config.wait_seconds,
                                 "hiding position reached")
                return DecisionCommand(CommandKind.HOLD, self.state, self.selected_position,
                                       "wait for opportunity")
            else:
                route = self.navigation.route(observation.agent_position, self.selected_position)
                if route is None:
                    return self.decide(Observation(**{**observation.__dict__, "route_failed": True}))
                return DecisionCommand(CommandKind.MOVE, self.state, self.selected_position,
                                       "follow dynamic A* route", path=tuple(route))

        if self.state == AmbusherState.WAIT:
            to_agent = (observation.agent_position - memory.position).normalized()
            player_looking = (
                memory.visible
                and memory.forward.dot(to_agent) > self.config.look_threshold
            )
            distance = observation.agent_position.distance_to(memory.position)
            opportunity = (
                memory.visible
                and distance <= self.config.opportunity_range
                and (not player_looking or memory.reloading)
            )
            if opportunity:
                self._transition(AmbusherState.WARN, now, self.config.warning_seconds,
                                 "player exposed or reloading")
                return DecisionCommand(CommandKind.WARN, self.state, memory.position,
                                       "telegraphed ambush")
            if player_looking or now >= self.state_until:
                self._reject_selected(now)
                self._transition(AmbusherState.REPOSITION, now, 0.0,
                                 "position exposed or expired")
            else:
                return DecisionCommand(CommandKind.FACE, self.state, memory.position,
                                       "track from concealment")

        if self.state == AmbusherState.WARN:
            if now < self.state_until:
                return DecisionCommand(CommandKind.WARN, self.state, memory.position,
                                       "warning window")
            self._transition(AmbusherState.STRIKE, now, 4.0, "warning completed")

        if self.state == AmbusherState.STRIKE:
            in_range = observation.agent_position.distance_to(memory.position) <= 2.3
            if memory.visible and observation.attack_ready and in_range:
                self._transition(AmbusherState.REPOSITION, now, 0.0, "strike committed")
                return DecisionCommand(CommandKind.ATTACK, AmbusherState.STRIKE,
                                       memory.position, "valid close strike")
            if now >= self.state_until:
                self._transition(AmbusherState.REPOSITION, now, 0.0,
                                 "strike window expired")
            command = CommandKind.INVESTIGATE if not memory.visible else CommandKind.MOVE
            return DecisionCommand(command, self.state, memory.position,
                                   "close on last evidence")

        return DecisionCommand(CommandKind.HOLD, self.state, reason="decision settled")

    def shutdown(self) -> None:
        self.world.release(self.owner_id)
        self.memory.clear()
