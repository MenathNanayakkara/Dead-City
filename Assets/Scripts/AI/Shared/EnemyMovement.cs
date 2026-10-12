using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyMovement : MonoBehaviour
{
    [SerializeField, Min(.5f)] private float stuckSeconds = 2;
    [SerializeField, Min(0)] private int localRetryLimit = 2;
    [SerializeField, Min(0)] private float turnSpeed = 360;
    private NavMeshAgent agent;
    private NavMeshPath probe;
    private float nextDestination, progressAt, retryUntil, blockedUntil;
    private Vector3 requested, progressPosition, blockedGoal;
    private bool moving, failed;
    private int retries;
    public Vector3 Velocity => Ready ? agent.velocity : Vector3.zero;
    private bool Ready => agent != null && agent.enabled && agent.isOnNavMesh;
    public bool Arrived => Ready && moving && !agent.pathPending && !failed &&
        agent.remainingDistance <= agent.stoppingDistance + .25f;
    public bool HasPath => Ready && agent.hasPath && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete;
    public bool Failed => failed;
    public string Status { get; private set; } = "Stopped";
    public int RetryCount => retries;
    public void ClearFailure() => failed = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        probe = new NavMeshPath();
    }

    private void OnDisable() => Stop();

    public bool TryPathLength(Vector3 point, out float length)
    {
        length = 0;
        if (!IsFinite(point) || !Ready || !NavMesh.SamplePosition(point, out var hit, 2, agent.areaMask) ||
            !agent.CalculatePath(hit.position, probe) || probe.status != NavMeshPathStatus.PathComplete) return false;
        var corners = probe.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return true;
    }

    public bool Reachable(Vector3 point) => TryPathLength(point, out _);

    public bool MoveTo(Vector3 destination, float speed, float stoppingDistance = 1.2f)
    {
        if (!IsFinite(destination)) return RejectInvalidInput("Invalid destination");
        if (!IsFinite(speed) || speed < 0) return RejectInvalidInput("Invalid movement speed");
        if (!IsFinite(stoppingDistance) || stoppingDistance < 0) return RejectInvalidInput("Invalid stopping distance");
        if (!Ready)
        {
            requested = destination;
            Fail("Off navigation mesh");
            return false;
        }
        if (Time.time < blockedUntil && (destination - blockedGoal).sqrMagnitude < 4) return false;
        if (moving && Time.time < retryUntil) return true;
        bool newJourney = !moving || (requested - destination).sqrMagnitude > 4;
        agent.speed = speed;
        agent.stoppingDistance = Mathf.Max(.1f, stoppingDistance);
        if (moving && Time.time < nextDestination && (requested - destination).sqrMagnitude < 1)
            return !failed; // A pending path is not a failed path.
        nextDestination = Time.time + .3f;
        requested = destination;
        if (!NavMesh.SamplePosition(destination, out var hit, 2, agent.areaMask))
        { Fail("Destination off mesh"); return false; }
        if (newJourney)
        {
            retries = 0;
            progressAt = Time.time;
            progressPosition = transform.position;
        }
        failed = false;
        moving = true;
        agent.isStopped = false;
        Status = "Moving";
        if (!agent.SetDestination(hit.position)) { Fail("Path rejected"); return false; }
        return true;
    }

    private void Update()
    {
        if (!Ready || !moving || agent.isStopped || Time.timeScale <= 0 || agent.pathPending) return;
        if (agent.pathStatus != NavMeshPathStatus.PathComplete) { Fail("Incomplete path"); return; }
        if (Arrived) { Status = "Arrived"; progressAt = Time.time; return; }
        if ((transform.position - progressPosition).sqrMagnitude > .12f)
        {
            progressPosition = transform.position;
            progressAt = Time.time;
        }
        if (Time.time - progressAt < stuckSeconds) return;
        if (retries++ >= localRetryLimit) { Fail("Stuck / retry budget exhausted"); return; }
        OpenNearbyDoor();
        // A short reachable sidestep can clear a crowded lane, then the brain resumes its goal.
        Vector3 side = transform.right * (retries % 2 == 0 ? -1 : 1) * 1.5f;
        if (NavMesh.SamplePosition(transform.position + side, out var hit, 1, agent.areaMask) && Reachable(hit.position))
        {
            if (!agent.SetDestination(hit.position)) { Fail("Recovery path rejected"); return; }
            retryUntil = Time.time + 1;
        }
        else
        {
            if (!Reachable(requested)) { Fail("Recovery route unavailable"); return; }
            if (!agent.SetDestination(requested)) { Fail("Recovery path rejected"); return; }
        }
        progressAt = Time.time;
        Status = "Retrying blocked route";
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private bool RejectInvalidInput(string reason)
    {
        Stop();
        failed = true;
        Status = reason;
        // Invalid caller input must not be cached as an unreachable world position.
        return false;
    }

    private void Fail(string reason)
    {
        blockedGoal = requested;
        blockedUntil = Time.time + 3;
        Stop();
        failed = true;
        Status = reason;
    }

    public void Stop()
    {
        if (Ready) { agent.isStopped = true; agent.ResetPath(); }
        moving = false;
        retryUntil = 0;
        nextDestination = 0;
        progressAt = Time.time;
        progressPosition = transform.position;
        // Preserve failed-goal cooldown and retry diagnostics for the brain.
        Status = "Stopped";
    }

    public void Face(Vector3 target)
    {
        var d = target - transform.position;
        d.y = 0;
        if (d.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), Mathf.Max(0, turnSpeed) * Time.deltaTime);
    }

    public void OpenNearbyDoor()
    {
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out var hit, 1.6f, 1 << 8, QueryTriggerInteraction.Ignore))
        {
            var door = hit.collider.GetComponentInParent<InteractiveDoor>();
            if (door != null) door.OpenForAgent();
        }
    }
}
