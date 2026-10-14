using UnityEngine;

public enum RunnerAction { Chase, Attack, Caution, Flank, Disengage }

/// <summary>Member 2: utility scores plus bounded action commitments and rush recovery.</summary>
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(EnemyCombat))]
    public sealed class RunnerBrain : MonoBehaviour {

    private const float ShotgunThreatRange = 12f;
    private const float RushTriggerRange = 12f;
    private const float ReloadOpportunityBonus = 0.65f;
    private const float AllyEncouragementBonus = 0.25f;
    private const float TacticalActionDuration = 2.5f;
    private const float RetreatCooldownDuration = 5f;
    [SerializeField] private float chaseSpeed = 4.8f;
    [SerializeField] private float cautionSpeed = 2.5f;
    [SerializeField] private float aggressionWeight = 1;
    [SerializeField] private float survivalWeight = 1;
    [SerializeField] private float tacticalWeight = 1;
    [SerializeField, Range(0, .3f)] private float hysteresis = .08f;
    [SerializeField, Min(.2f)] private float commitmentSeconds = .8f;
    [SerializeField, Min(.5f)] private float rushSeconds = 2.4f;
    [SerializeField, Min(.3f)] private float recoverySeconds = 1.2f;
    private float nextThink, changedAt, actionUntil, recoverUntil, nextRetreat, rushUntil;
    private Vector3 tacticalGoal;
    private bool hasTacticalGoal;
    public RunnerAction Action { get; private set; }
    public float[] Scores { get; } = new float[5];
    public bool PathAvailable { get; private set; }
    public float ShotgunThreat { get; private set; }

    public float CommitmentRemaining =>
    Mathf.Max(0f, actionUntil - Time.time);

    public float RushRemaining =>
    Mathf.Max(0f, rushUntil - Time.time);

    public float RecoveryRemaining =>
    Mathf.Max(0f, recoverUntil - Time.time);
    
    private EnemyActor actor;
    private EnemyCombat combat;


private void OnValidate()
{
    chaseSpeed = Mathf.Max(0f, chaseSpeed);
    cautionSpeed = Mathf.Max(0f, cautionSpeed);

    aggressionWeight = Mathf.Max(0f, aggressionWeight);
    survivalWeight = Mathf.Max(0f, survivalWeight);
    tacticalWeight = Mathf.Max(0f, tacticalWeight);

    hysteresis = Mathf.Clamp(hysteresis, 0f, 0.3f);
    commitmentSeconds = Mathf.Max(0.2f, commitmentSeconds);
    rushSeconds = Mathf.Max(0.5f, rushSeconds);
    recoverySeconds = Mathf.Max(0.3f, recoverySeconds);
}
    private void Awake()
    {
    actor = GetComponent<EnemyActor>();
    combat = GetComponent<EnemyCombat>();
    nextThink = Random.value * .3f;
    }


    private void OnEnable()
    {
    if (combat == null)
        combat = GetComponent<EnemyCombat>();

    combat.AttackStarted += OnAttack;
    }

    private void OnDisable(){
    if (combat != null)
        combat.AttackStarted -= OnAttack;
        }
    private void OnAttack(bool heavy, float duration)
    {
        recoverUntil = Time.time + duration + recoverySeconds;
        rushUntil = 0;
    }

    public static void Score(float distance, float ownHealth, float weakness, bool shotgun, bool firing,
        bool visible, bool reachable, float[] scores, float attackRange = 1.8f, bool attackReady = true)
    {
        float threat = shotgun
         ? Mathf.Clamp01(1 - distance / ShotgunThreatRange)
         : 0;
        scores[0] = Mathf.Clamp01((.38f + .48f * weakness + .2f * Mathf.Clamp01(distance / 20)) * (reachable ? 1 : 0) - .3f * threat);
        scores[1] = visible && attackReady && distance <= attackRange ? 1.5f : 0;
        scores[2] = Mathf.Clamp01(.9f * threat * (1 - .7f * weakness));
        scores[3] = Mathf.Clamp01(.2f + .4f * threat + (firing ? .15f : 0) + (reachable ? 0 : .35f));
        scores[4] = Mathf.Clamp01((1 - ownHealth) * .8f + threat * .2f - .25f * weakness);
    }

    private void Update()
    {
        int best = FindBestAction();
        if (!actor.TickDue(ref nextThink, .25f) || actor.Combat.IsAttacking) return;
        var s = actor.Perception;
        if (Time.time < recoverUntil)
        {
            actor.Decision = "Runner / recover from rush";
            actor.Movement.Stop();
            return;
        }
        if (!s.Visible)
        {
            rushUntil = 0;
            hasTacticalGoal = false;
            actor.Investigate(cautionSpeed);
            return;
        }
        if (rushUntil > 0 && Time.time >= rushUntil)
        {
            rushUntil = 0;
            recoverUntil = Time.time + recoverySeconds;
            actor.Movement.Stop();
            actor.Decision = "Runner / missed rush recovery";
            return;
        }
        PathAvailable = actor.Movement.Reachable(s.TargetPosition);
        ShotgunThreat = s.Weapon == WeaponCategory.Shotgun
        ? Mathf.Clamp01(1 - s.Distance / ShotgunThreatRange)
        : 0;
        Score(s.Distance, actor.Health.Fraction, s.PlayerWeakness, s.Weapon == WeaponCategory.Shotgun,
            s.PlayerFiring, s.Visible, PathAvailable, Scores, actor.Combat.Range, actor.Combat.CanStartAttack);
        Scores[0] =
        (Scores[0]
        + (s.PlayerReloading ? ReloadOpportunityBonus : 0)
        + (actor.IsEncouraged ? AllyEncouragementBonus : 0))
        * aggressionWeight;
        Scores[2] *= survivalWeight;
        Scores[3] *= tacticalWeight;
        Scores[4] = Time.time >= nextRetreat ? Scores[4] * survivalWeight : 0;
        int best = 0;
        for (int i = 1; i < Scores.Length; i++) if (Scores[i] > Scores[best]) best = i;
        bool expired = Time.time >= actionUntil || actor.Movement.Failed ||
            (hasTacticalGoal && actor.Movement.Arrived);
        bool invalidAttack = Action == RunnerAction.Attack && !actor.Combat.CanStartAttack;
        if (expired || invalidAttack || best == 1 ||
            (Time.time - changedAt >= commitmentSeconds && Scores[best] > Scores[(int)Action] + hysteresis))
        {
            // Retreats and circling must yield an approach opportunity, even at low health.
            if (expired && (Action == RunnerAction.Caution || Action == RunnerAction.Disengage || Action == RunnerAction.Flank))
                best = actor.Combat.CanStartAttack ? 1 : 0;
            Select((RunnerAction)best);
        }
        actor.Decision = "Utility: " + Action + (s.PlayerReloading ? " / observed reload" : "");
        if (Action == RunnerAction.Attack)
        {
            if (!actor.Combat.TryAttack()) actor.Approach(cautionSpeed);
            return;
        }
        if (Action == RunnerAction.Chase)
        {
        if (s.Distance < RushTriggerRange && rushUntil <= 0) rushUntil = Time.time + rushSeconds;
            actor.Approach(chaseSpeed);
            return;
        }
        if (!hasTacticalGoal || !actor.Movement.MoveTo(tacticalGoal, Action == RunnerAction.Caution ? cautionSpeed : chaseSpeed, .6f))
        {
            hasTacticalGoal = false;
            actionUntil = 0;
            actor.Approach(cautionSpeed);
        }
    }

    private int FindBestAction()
    {
    int best = 0;

    for (int i = 1; i < Scores.Length; i++)
    {
        if (Scores[i] > Scores[best])
            best = i;
    }

    return best;
}
    private void Select(RunnerAction next)
    {
        bool changed = Action != next;
        Action = next;
        changedAt = Time.time;
        actionUntil = Time.time
        + (next == RunnerAction.Chase
        ? rushSeconds
        : TacticalActionDuration);
        if (next != RunnerAction.Chase) rushUntil = 0;
        hasTacticalGoal = false;
        if (next == RunnerAction.Disengage) 
        nextRetreat = actionUntil + RetreatCooldownDuration;
        if (next == RunnerAction.Flank || next == RunnerAction.Caution || next == RunnerAction.Disengage)
            hasTacticalGoal = ChooseRoute(next, out tacticalGoal);
        if (changed && next == RunnerAction.Chase) rushUntil = 0;
    }

    private bool ChooseRoute(RunnerAction kind, out Vector3 goal)
    {
        var s = actor.Perception;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - s.TargetPosition, Vector3.up).normalized;
        if (away.sqrMagnitude < .1f) away = -transform.forward;
        goal = transform.position;
        float best = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            float angle = kind == RunnerAction.Flank ? (i % 2 == 0 ? 1 : -1) * (60 + i / 2 * 35) : (i - 1.5f) * 25;
            float radius = kind == RunnerAction.Disengage ? 10 : kind == RunnerAction.Flank ? 5 : 7;
            Vector3 candidate = s.TargetPosition + Quaternion.Euler(0, angle, 0) * away * radius;
            if (!actor.Movement.TryPathLength(candidate, out float cost)) continue;
            float side = 1 - Vector3.Dot(s.ObservedForward, (candidate - s.TargetPosition).normalized);
            float score = side * 3 - cost * .2f;
            if (score <= best) continue;
            best = score;
            goal = candidate;
        }
        return best > float.NegativeInfinity;
    }
}
