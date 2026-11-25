from dataclasses import dataclass


@dataclass(frozen=True)
class AmbusherConfig:
    flank_weight: float = 0.30
    concealment_weight: float = 0.25
    travel_weight: float = 0.15
    intercept_weight: float = 0.15
    proximity_weight: float = 0.15
    prediction_seconds: float = 1.5
    preferred_distance: float = 8.0
    opportunity_range: float = 7.0
    warning_seconds: float = 0.6
    wait_seconds: float = 8.0
    move_timeout: float = 12.0
    memory_seconds: float = 16.0
    rejection_seconds: float = 5.0
    arrival_distance: float = 1.3
    look_threshold: float = 0.55
    maximum_replans: int = 2

    def __post_init__(self) -> None:
        weights = (self.flank_weight, self.concealment_weight, self.travel_weight,
                   self.intercept_weight, self.proximity_weight)
        if any(weight < 0 for weight in weights):
            raise ValueError("Tactical weights cannot be negative")
        if abs(sum(weights) - 1.0) > 1e-6:
            raise ValueError("Tactical weights must sum to 1.0")
        if min(self.opportunity_range, self.warning_seconds, self.memory_seconds) <= 0:
            raise ValueError("Ranges and durations must be positive")
        if self.maximum_replans < 0:
            raise ValueError("maximum_replans cannot be negative")

