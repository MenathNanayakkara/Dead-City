using UnityEngine;

public enum AmbusherState { Observe, Evaluate, Move, Wait, Strike, Reposition, Warn }

/// <summary>Member 3: concealed positioning, observation-based interception and explicit state exits.</summary>
public sealed class AmbusherBrain : MonoBehaviour
{
    [SerializeField] private float travelSpeed = 3.1f;
    [SerializeField] private float strikeSpeed = 4.4f;
    [SerializeField] private float flankWeight = .3f;
    [SerializeField] private float concealmentWeight = .25f;
    [SerializeField] private float travelWeight = .15f;
    [SerializeField] private float interceptWeight = .15f;
    [SerializeField] private float proximityWeight = .15f;
    [SerializeField, Min(.2f)] private float warningSeconds = .6f;
    [SerializeField, Min(1)] private float opportunityRange = 7;
    private EnemyActor actor;
    private float nextThink, stateUntil, nextAmbush;
    private MapMarker selected, rejected;
    public AmbusherState State { get; private set; }
    public MapMarker Selected => selected;
    public float[] PointScores { get; private set; }

    private void Awake() { actor = GetComponent<EnemyActor>(); stateUntil = Time.time + .75f; }
    private void OnDisable() => ReleasePosition();

    private void Update()
    {
        if (actor.CanThink && !actor.Combat.IsAttacking && actor.Perception.HasMemory &&
            (State == AmbusherState.Wait || State == AmbusherState.Warn))
            actor.Movement.Face(actor.Perception.LastKnownPosition);
        if (!actor.TickDue(ref nextThink, .2f) || actor.Combat.IsAttacking) return;
        var s = actor.Perception;
        if (!s.HasMemory)
        {
            SetState(AmbusherState.Observe, .75f);
            actor.Investigate(travelSpeed);
            return;
        }
        if (State == AmbusherState.Observe && Time.time >= stateUntil)
            SetState(AmbusherState.Evaluate, 0);
        if (State == AmbusherState.Evaluate || State == AmbusherState.Reposition)
        {
            if (Time.time < nextAmbush) { SetState(AmbusherState.Strike, 4); }
            else
            {
                var candidate = ChoosePoint();
                if (candidate != null && actor.World.Reserve(candidate, actor))
                {
                    selected = candidate;
                    actor.Movement.ClearFailure();
                    SetState(AmbusherState.Move, 12);
                }
                else { nextAmbush = Time.time + 5; SetState(AmbusherState.Strike, 4); }
            }
        }
        if (State == AmbusherState.Move)
        {
            if (selected == null || Time.time >= stateUntil || actor.Movement.Failed)
            { rejected = selected; SetState(AmbusherState.Reposition, 0); return; }
            if (s.Visible && s.Distance <= actor.Combat.Range + .5f)
            { SetState(AmbusherState.Warn, warningSeconds); return; }
            if (Vector3.Distance(transform.position, selected.transform.position) < 1.3f)
            { actor.Movement.Stop(); SetState(AmbusherState.Wait, 8); }
            else if (!actor.Movement.MoveTo(selected.transform.position, travelSpeed, .6f))
            { rejected = selected; SetState(AmbusherState.Reposition, 0); }
            actor.Movement.OpenNearbyDoor();
        }
        if (State == AmbusherState.Wait)
        {
            bool playerLooking = s.Visible && Vector3.Dot(s.ObservedForward,
                (transform.position - s.TargetPosition).normalized) > .55f;
            bool opportunity = s.Visible && s.Distance <= opportunityRange && (!playerLooking || s.PlayerReloading);
            if (opportunity) SetState(AmbusherState.Warn, warningSeconds);
            else if (playerLooking || Time.time >= stateUntil)
            { rejected = selected; nextAmbush = Time.time + 1; SetState(AmbusherState.Reposition, 0); }
            // Waiting expires into relocation, never a blind strike.
        }
        if (State == AmbusherState.Warn)
        {
            actor.Movement.Stop();
            actor.Movement.Face(s.LastKnownPosition);
            if (Time.time >= stateUntil) SetState(AmbusherState.Strike, 4);
            return;
        }
        if (State == AmbusherState.Strike)
        {
            if (!s.Visible) { actor.Investigate(travelSpeed); if (Time.time >= stateUntil) SetState(AmbusherState.Reposition, 0); return; }
            if (actor.Combat.TryAttack())
            { nextAmbush = Time.time + actor.Combat.Duration + 2; SetState(AmbusherState.Reposition, 0); return; }
            if (Time.time >= stateUntil)
            { SetState(AmbusherState.Reposition, 0); actor.Movement.Stop(); return; }
            actor.Approach(strikeSpeed);
        }
    }

    private void SetState(AmbusherState next, float seconds)
    {
        if (next != AmbusherState.Move && next != AmbusherState.Wait) ReleasePosition();
        State = next;
        stateUntil = Time.time + seconds;
        actor.Decision = "Tactical: " + next;
    }

    private void ReleasePosition()
    {
        if (actor != null && actor.World != null) actor.World.Release(actor);
        selected = null;
    }

    public MapMarker ChoosePoint()
    {
        var world = actor.World;
        var points = world.tacticalPoints;
        if (points == null) return null;
        if (PointScores == null || PointScores.Length != points.Length) PointScores = new float[points.Length];
        MapMarker best = null;
        float bestScore = float.NegativeInfinity;
        var s = actor.Perception;
        Vector3 player = s.LastKnownPosition, predicted = player + s.ObservedVelocity * 1.5f;
        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            PointScores[i] = -1;
            if (point == null || point == rejected || (point.kind != MapMarkerKind.Ambush && point.kind != MapMarkerKind.Cover) ||
                !world.Available(point, actor) || !actor.Movement.TryPathLength(point.transform.position, out float pathLength)) continue;
            Vector3 p = point.transform.position, relative = p - player;
            bool concealed = Physics.Linecast(player + Vector3.up, p + Vector3.up, world.obstructionMask, QueryTriggerInteraction.Ignore);
            float flank = (1 - Vector3.Dot(s.ObservedForward, relative.normalized)) * .5f;
            if (!concealed && flank < .65f) continue;
            float travel = 1 - Mathf.Clamp01(pathLength / 40);
            float proximity = 1 - Mathf.Clamp01(Mathf.Abs(relative.magnitude - 8) / 25);
            float intercept = 1 - Mathf.Clamp01(Vector3.Distance(p, predicted) / 30);
            float score = flank * flankWeight + (concealed ? 1 : 0) * concealmentWeight + travel * travelWeight +
                intercept * interceptWeight + proximity * proximityWeight;
            PointScores[i] = score;
            if (score > bestScore) { bestScore = score; best = point; }
        }
        return best;
    }
}