using System.Collections.Generic;
using UnityEngine;

public sealed class HunterBrain : MonoBehaviour
{
    [SerializeField] private float speed = 2.25f;
    [SerializeField, Min(.5f)] private float replanInterval = 1.5f;
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
        var sensed = actor.Perception;
        Vector3 goal;
        if (sensed.Visible) goal = actor.World.ApproachPosition(actor);
        else if (!actor.InvestigationGoal(out goal))
        { actor.Movement.Stop(); path.Clear(); return; }
        actor.Movement.Stop();
    }
}
