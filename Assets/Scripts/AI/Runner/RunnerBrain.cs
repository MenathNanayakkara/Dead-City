using UnityEngine;

public enum RunnerAction { Chase, Attack, Caution, Flank, Disengage }

/// <summary>Member 2: utility scores plus bounded action commitments and rush recovery.</summary>
public sealed class RunnerBrain : MonoBehaviour
{
    [SerializeField] private float chaseSpeed = 4.8f;
    [SerializeField] private float cautionSpeed = 2.5f;
    [SerializeField] private float aggressionWeight = 1;
    [SerializeField] private float survivalWeight = 1;
    [SerializeField] private float tacticalWeight = 1;
    [SerializeField, Range(0, .3f)] private float hysteresis = .08f;
    [SerializeField, Min(.2f)] private float commitmentSeconds = .8f;
    [SerializeField, Min(.5f)] private float rushSeconds = 2.4f;
    [SerializeField, Min(.3f)] private float recoverySeconds = 1.2f;
    private EnemyActor actor;
    private float nextThink, changedAt, actionUntil, recoverUntil, nextRetreat, rushUntil;
    private Vector3 tacticalGoal;
    private bool hasTacticalGoal;
    public RunnerAction Action { get; private set; }
    public float[] Scores { get; } = new float[5];
    public bool PathAvailable { get; private set; }
    public float ShotgunThreat { get; private set; }

    private void Awake()
    {
        actor = GetComponent<EnemyActor>();
        nextThink = Random.value * .3f;
    }
    private void OnEnable() => GetComponent<EnemyCombat>().AttackStarted += OnAttack;
    private void OnDisable() => GetComponent<EnemyCombat>().AttackStarted -= OnAttack;
    private void OnAttack(bool heavy, float duration)
    {
        recoverUntil = Time.time + duration + recoverySeconds;
        rushUntil = 0;
    }

    
}
