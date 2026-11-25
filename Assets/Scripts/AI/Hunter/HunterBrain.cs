using System.Collections.Generic;
using UnityEngine;

public sealed class HunterBrain : MonoBehaviour
{
    [SerializeField] private float speed = 2.25f;
    [SerializeField, Min(.5f)] private float replanInterval = 1.5f;
    [SerializeField] private bool drawPath;
    private EnemyActor actor;
    private HunterNavigationGraph graph;
    private float nextThink, nextPlan, earliestPlan;
    private Vector3 plannedGoal;
    private bool plannedVisible;
    private readonly List<Vector3> path = new List<Vector3>();
    private int waypoint;
    public int ExpandedNodes { get; private set; }
    public float PathCost { get; private set; }

    private void Awake() { actor = GetComponent<EnemyActor>(); nextThink = Random.value * .3f; }

    private void Update()
    {
        if (!actor.TickDue(ref nextThink, .15f)) return;
        if (graph == null) graph = actor.World.GetComponent<HunterNavigationGraph>();
        if (actor.Combat.IsAttacking) { actor.Decision = "Hunter / committed strike"; return; }
        var sensed = actor.Perception;
        if (actor.Combat.TryAttack()) { path.Clear(); return; }
        Vector3 goal;
        if (sensed.Visible) goal = actor.World.ApproachPosition(actor);
        else if (!actor.InvestigationGoal(out goal))
        { actor.Movement.Stop(); path.Clear(); return; }
        bool changed = (goal - plannedGoal).sqrMagnitude > 4 || sensed.Visible != plannedVisible || actor.Movement.Failed;
        if (Time.time >= earliestPlan && (changed || Time.time >= nextPlan || path.Count == 0))
        {
            earliestPlan = Time.time + .45f;
            nextPlan = Time.time + replanInterval;
            plannedGoal = goal;
            plannedVisible = sensed.Visible;
            bool found = graph != null && graph.FindPath(transform.position, goal, path);
            waypoint = 0;
            ExpandedNodes = graph != null ? graph.LastExpanded : 0;
            PathCost = graph != null ? graph.LastPathCost : 0;
            if (!found)
            {
                path.Clear();
                actor.Decision = "Hunter / A* route unavailable";
                actor.Movement.Stop();
                return;
            }
            actor.Movement.ClearFailure();
        }
        while (waypoint < path.Count && Vector3.Distance(transform.position, path[waypoint]) < .6f) waypoint++;
        if (waypoint < path.Count)
        {
            actor.Decision = sensed.Visible ? "Hunter / track with A*" : "Hunter / investigate with A*";
            bool final = waypoint == path.Count - 1;
            if (!actor.Movement.MoveTo(path[waypoint], speed, final ? 1.2f : .2f)) nextPlan = 0;
            actor.Movement.OpenNearbyDoor();
        }
        else actor.Movement.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPath) return;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < path.Count; i++) Gizmos.DrawLine(path[i - 1] + Vector3.up * .1f, path[i] + Vector3.up * .1f);
    }
}
