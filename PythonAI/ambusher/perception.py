from __future__ import annotations

from dataclasses import dataclass

from .domain import Observation, Vec2


@dataclass(frozen=True)
class Memory:
    position: Vec2
    observed_at: float
    forward: Vec2
    velocity: Vec2
    reloading: bool
    visible: bool


class PerceptionMemory:
    def __init__(self, memory_seconds: float):
        if memory_seconds <= 0:
            raise ValueError("memory_seconds must be positive")
        self.memory_seconds = memory_seconds
        self._memory: Memory | None = None

    def update(self, observation: Observation) -> Memory | None:
        if observation.player_visible and observation.player_position is not None:
            self._memory = Memory(observation.player_position, observation.now,
                                  observation.player_forward.normalized(), observation.player_velocity,
                                  observation.player_reloading, True)
        elif observation.sound_position is not None:
            self._memory = Memory(observation.sound_position, observation.now,
                                  Vec2(0.0, 0.0), Vec2(0.0, 0.0), False, False)
        elif self._memory is not None and observation.now - self._memory.observed_at > self.memory_seconds:
            self._memory = None
        elif self._memory is not None:
            self._memory = Memory(self._memory.position, self._memory.observed_at,
                                  self._memory.forward, self._memory.velocity, False, False)
        return self._memory

    def clear(self) -> None:
        self._memory = None