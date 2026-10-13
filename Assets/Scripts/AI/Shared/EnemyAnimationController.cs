using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement), typeof(EnemyCombat))]
public sealed class EnemyAnimationController : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [Header("Measured foot travel at clip playback speed 1")]
    [SerializeField, Min(.01f)] private float walkStrideSpeed = 1.675673f;
    [SerializeField, Min(.01f)] private float chaseStrideSpeed = 4.528451f;
    [SerializeField, Min(.01f)] private float visualStrideScale = 1f;
    [Header("Locomotion variation")]
    [SerializeField] private bool randomizeLocomotionPhase = true;
    [SerializeField, Min(0)] private float speedDampTime = .04f;
    [Header("Contact-timed action clips")]
    [SerializeField] private AnimationClip attackClip;
    [SerializeField] private AnimationClip heavyAttackClip;
    private EnemyHealth health;
    private EnemyMovement movement;
    private EnemyCombat combat;
    private bool eventsSubscribed;
    private static readonly int Speed = Animator.StringToHash("Speed"), Attack = Animator.StringToHash("Attack"), Heavy = Animator.StringToHash("Heavy"), Hit = Animator.StringToHash("Hit"), Dead = Animator.StringToHash("Dead"), AttackSpeed = Animator.StringToHash("AttackSpeed");
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        combat = GetComponent<EnemyCombat>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (health == null || movement == null || combat == null || animator == null)
        {
            Debug.LogError("Enemy animation requires health, movement, combat and an Animator.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        // Offset only the initial movement cycle so a spawned group does not march
        // in lockstep. Attacks, hits and death always keep their authored timing.
        if (randomizeLocomotionPhase && animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.avatar != null &&
            animator.avatar.isValid && animator.HasState(0, Locomotion) &&
            !health.IsDead && !combat.IsAttacking)
            animator.Play(Locomotion, 0, Random.value);
    }

    private void OnEnable()
    {
        if (eventsSubscribed || !enabled || health == null || combat == null) return;
        health.Hit += OnHit;
        health.Died += OnDeath;
        combat.AttackStarted += OnAttack;
        eventsSubscribed = true;
    }

    private void OnDisable()
    {
        if (!eventsSubscribed) return;
        if (health != null)
        {
            health.Hit -= OnHit;
            health.Died -= OnDeath;
        }
        if (combat != null) combat.AttackStarted -= OnAttack;
        eventsSubscribed = false;
    }

    private void Update()
    {
        if (animator != null && !health.IsDead)
        {
            float speed = Vector3.ProjectOnPlane(movement.Velocity, Vector3.up).magnitude;
            float damping = float.IsNaN(speedDampTime) || float.IsInfinity(speedDampTime)
                ? .04f : Mathf.Max(0, speedDampTime);
            animator.SetFloat(Speed, speed < .05f ? 0f : speed, speed < .05f ? 0f : damping, Time.deltaTime);
            float reference = animator.GetCurrentAnimatorStateInfo(0).IsName("Chase") ? chaseStrideSpeed : walkStrideSpeed;
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                float nextReference = next.IsName("Chase") ? chaseStrideSpeed : walkStrideSpeed;
                reference = Mathf.Lerp(reference, nextReference, Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
            }
            // Only locomotion states consume this parameter; attack/death timing is separate.
            float cadence = speed < .05f ? 1f : Mathf.Clamp(speed / (reference * visualStrideScale), .08f, 3f);
            animator.SetFloat("GaitScale", cadence);
        }
    }

    private void OnHit(float amount)
    {
        if (animator != null && !combat.IsAttacking)
            animator.SetTrigger(Hit);
    }

    private void OnDeath(EnemyHealth value)
    {
        if (animator != null)
        {
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Heavy);
            animator.ResetTrigger(Hit);
            animator.SetBool(Dead, true);
        }
    }

    private void OnAttack(bool heavy, float duration)
    {
        if (animator != null)
        {
            var clip = heavy ? heavyAttackClip : attackClip;
            // Clip lengths are not necessarily one second. Match the actual action
            // to the combat clock so the visible strike and damage share a contact time.
            float length = clip != null ? clip.length : 1f;
            animator.SetFloat(AttackSpeed, length / Mathf.Max(.05f, duration));
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Heavy);
            animator.ResetTrigger(Hit);
            animator.CrossFadeInFixedTime(heavy ? "Base Layer.Heavy" : "Base Layer.Attack", .06f, 0, 0f);
        }
    }
}
