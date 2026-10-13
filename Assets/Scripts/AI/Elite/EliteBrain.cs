using UnityEngine;

public enum EliteAction { DirectAttack, HeavyAttack, Push, MaintainDistance, Reposition, Retreat }

/// <summary>Survival and action feasibility above committed utility decisions.</summary>
public sealed class EliteBrain : MonoBehaviour
{
    [SerializeField, Min(0)] private float advanceSpeed = 2.8f;
    [SerializeField, Min(0)] private float pushSpeed = 4;
    [SerializeField, Min(0)] private float heavyCooldown = 6;
    [SerializeField, Range(0, 1)] private float retreatHealth = .25f;
    [SerializeField, Min(0)] private float aggressionWeight = 1;
    [SerializeField, Min(0)] private float tacticalWeight = 1;
    [SerializeField, Min(.3f)] private float commitmentSeconds = 1.3f;
    private EnemyActor actor;
    private float nextThink, nextHeavy, nextRetreat, retreatUntil, committedUntil, actionUntil, advanceUntil;
    private float healthSnapshot = 1, damagedUntil;
    private MapMarker cover;
    public EliteAction Action { get; private set; }
    public float[] Scores { get; } = new float[5];
    public int NearbyAllies { get; private set; }
    public MapMarker Cover => cover;

    private void Awake()
    {
        ValidateSettings();
        actor = GetComponent<EnemyActor>();
        nextThink = Random.value * .3f;
    }

    private void OnValidate() => ValidateSettings();

    private void ValidateSettings()
    {
        advanceSpeed = ValidSetting(advanceSpeed, 0, 2.8f);
        pushSpeed = ValidSetting(pushSpeed, 0, 4);
        heavyCooldown = ValidSetting(heavyCooldown, 0, 6);
        retreatHealth = Mathf.Clamp01(ValidSetting(retreatHealth, 0, .25f));
        aggressionWeight = ValidSetting(aggressionWeight, 0, 1);
        tacticalWeight = ValidSetting(tacticalWeight, 0, 1);
        commitmentSeconds = ValidSetting(commitmentSeconds, .3f, 1.3f);
    }

    private static float ValidSetting(float value, float minimum, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);

    private void OnDisable() => ReleaseCover();

    public static void Score(float distance, float weakness, bool shotgun, bool firing, int allies,
        bool reachable, bool heavyReady, float range, float[] scores)
    {
        if (scores == null) throw new System.ArgumentNullException(nameof(scores));
        if (scores.Length < 5) throw new System.ArgumentException("Five action score slots are required.", nameof(scores));
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0 ||
            float.IsNaN(range) || float.IsInfinity(range) || range < 0)
        {
            System.Array.Clear(scores, 0, 5);
            return;
        }
        weakness = Mathf.Clamp01(ValidSetting(weakness, 0, 0));
        scores[0] = reachable ? Mathf.Clamp01(.45f + .3f * weakness + (distance <= range ? .2f : 0)) : 0;
        scores[1] = heavyReady && distance <= range ? 1.5f : 0;
        scores[2] = reachable ? Mathf.Clamp01(.2f + .35f * Mathf.Clamp01(allies / 4f) + .35f * weakness) : 0;
        scores[3] = shotgun ? Mathf.Clamp01((1 - distance / 12) * .95f * (1 - weakness * .6f)) : 0;
        scores[4] = Mathf.Clamp01(.2f + (reachable ? 0 : .55f) + (firing ? .25f : 0));
    }

    private void Update()
    {
        if (!actor.TickDue(ref nextThink, .3f) || actor.Combat.IsAttacking) return;
        var s = actor.Perception;
        if (actor.Health.Fraction < healthSnapshot) damagedUntil = Time.time + 3;
        healthSnapshot = actor.Health.Fraction;
        if (!s.Visible)
        {
            ReleaseCover();
            retreatUntil = 0;
            actor.Investigate(advanceSpeed);
            return;
        }
        if (actor.Health.Fraction < retreatHealth && Time.time < damagedUntil && Time.time >= nextRetreat)
        {
            retreatUntil = Time.time + 3;
            nextRetreat = Time.time + 12;
            Set(EliteAction.Retreat);
        }
        if (Time.time < retreatUntil)
        {
            actor.Decision = "Elite / retreat under pressure";
            if (!MoveToCover(true))
            {
                retreatUntil = 0;
                actor.Movement.Stop(); // A failed retreat must not become a blind charge.
            }
            return;
        }
        if (Action == EliteAction.Retreat)
        { ReleaseCover(); Set(EliteAction.DirectAttack); advanceUntil = Time.time + 2; }
        NearbyAllies = actor.World.AlliesNear(actor, 12);
        bool reachable = actor.Movement.Reachable(s.TargetPosition);
        bool canStrike = actor.Combat.CanStartAttack;
        Score(s.Distance, s.PlayerWeakness, s.Weapon == WeaponCategory.Shotgun, s.PlayerFiring,
            NearbyAllies, reachable, Time.time >= nextHeavy && canStrike, actor.Combat.Range, Scores);
        Scores[0] *= aggressionWeight;
        Scores[2] *= aggressionWeight;
        Scores[3] *= tacticalWeight;
        Scores[4] *= tacticalWeight;
        if (Time.time < advanceUntil) Scores[3] = Scores[4] = 0;
        int best = 0;
        for (int i = 1; i < Scores.Length; i++) if (Scores[i] > Scores[best]) best = i;
        bool expired = Time.time >= actionUntil;
        bool invalid = Action == EliteAction.HeavyAttack && !canStrike || actor.Movement.Failed;
        if (expired && (Action == EliteAction.Reposition || Action == EliteAction.MaintainDistance))
        {
            advanceUntil = Time.time + 2;
            best = 0;
        }
        if (expired || invalid || best == 1 ||
            (Time.time >= committedUntil && Scores[best] > Scores[(int)Action] + .1f))
            Set((EliteAction)best);
        actor.Decision = "Hierarchy / utility: " + Action;
        if (Action == EliteAction.HeavyAttack)
        {
            if (actor.Combat.TryAttack(true))
            { nextHeavy = Time.time + heavyCooldown; actionUntil = 0; }
            else { Set(EliteAction.DirectAttack); actor.Approach(advanceSpeed); }
            return;
        }
        if (Action == EliteAction.Reposition)
        {
            if (!MoveToCover(false)) { Set(EliteAction.DirectAttack); actor.Approach(advanceSpeed); }
            return;
        }
        if (Action == EliteAction.MaintainDistance)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - s.TargetPosition, Vector3.up).normalized;
            if (!actor.Movement.MoveTo(s.TargetPosition + away * 7, advanceSpeed))
            { Set(EliteAction.DirectAttack); actor.Approach(advanceSpeed); }
            return;
        }
        if (Action == EliteAction.Push) actor.World.EncourageAdvance(actor, 12, 1.5f);
        if (actor.Combat.TryAttack()) { actionUntil = 0; return; }
        actor.Approach(Action == EliteAction.Push ? pushSpeed : advanceSpeed);
    }

    private void Set(EliteAction value)
    {
        if (Action != value) ReleaseCover();
        Action = value;
        committedUntil = Time.time + commitmentSeconds;
        actionUntil = Time.time + 3;
        actor.Movement.ClearFailure();
        actor.Decision = "Hierarchy / utility: " + value;
    }

    private void ReleaseCover()
    {
        if (actor != null && actor.World != null) actor.World.Release(actor);
        cover = null;
    }

    private bool MoveToCover(bool retreat)
    {
        if (cover == null)
        {
            float highest = float.NegativeInfinity;
            var points = actor.World.tacticalPoints;
            if (points == null) return false;
            foreach (var p in points)
            {
                if (p == null || (p.kind != MapMarkerKind.Cover && p.kind != MapMarkerKind.Ambush) ||
                    !actor.World.Available(p, actor) || !actor.Movement.TryPathLength(p.transform.position, out float length)) continue;
                float distance = Vector3.Distance(p.transform.position, actor.Perception.LastKnownPosition);
                bool concealed = Physics.Linecast(actor.Perception.LastKnownPosition + Vector3.up,
                    p.transform.position + Vector3.up, actor.World.obstructionMask, QueryTriggerInteraction.Ignore);
                if (retreat && !concealed && distance < actor.Perception.Distance + 2) continue;
                float score = (retreat ? Mathf.Min(distance, 18) : -Mathf.Abs(distance - 7)) - .4f * length + (concealed ? 4 : 0);
                if (score > highest) { highest = score; cover = p; }
            }
            if (cover == null || !actor.World.Reserve(cover, actor)) { cover = null; return false; }
        }
        if (!actor.Movement.MoveTo(cover.transform.position, retreat ? pushSpeed : advanceSpeed, .6f))
        { ReleaseCover(); return false; }
        if (actor.Movement.Arrived) actor.Movement.Stop();
        return true;
    }
}
