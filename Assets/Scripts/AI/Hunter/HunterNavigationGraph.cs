using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Member 1: explicit A* with f=g+h over a NavMesh-sampled grid. Euclidean h is admissible.</summary>
[DefaultExecutionOrder(-50)]
public sealed class HunterNavigationGraph : MonoBehaviour
{
    [SerializeField]
    private Vector3 center;
    [SerializeField]
    private Vector2 size = new Vector2(72, 72);
    [SerializeField, Min(1)]
    private float spacing = 2;
    [SerializeField, Min(64)]
    private int expansionBudget = 1600;
    private Vector3[] nodes;
    private bool[] walkable, closed, opened;
    private float[] g;
    private int[] parent;
    private int columns, rows;
    private readonly List<int> open = new List<int>(256);
    public int LastExpanded { get; private set; }
    public float LastPathCost { get; private set; }

    private void Awake() => Build();
    public void Build()
    {
        columns = Mathf.CeilToInt(size.x / spacing) + 1;
        rows = Mathf.CeilToInt(size.y / spacing) + 1;
        int count = columns * rows;
        nodes = new Vector3[count];
        walkable = new bool[count];
        closed = new bool[count];
        opened = new bool[count];
        g = new float[count];
        parent = new int[count];
        for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                int id = z * columns + x;
                var p = center + new Vector3(x * spacing - size.x / 2, 0, z * spacing - size.y / 2);
                walkable[id] = NavMesh.SamplePosition(p, out var hit, spacing * .42f, NavMesh.AllAreas);
                nodes[id] = walkable[id] ? hit.position : p;
            }
    }

    public bool FindPath(Vector3 start, Vector3 goal, List<Vector3> result)
    {
        result.Clear();
        if (nodes == null)
            Build();
        int first = Nearest(start), last = Nearest(goal);
        LastExpanded = 0;
        LastPathCost = 0;
        if (first < 0 || last < 0)
            return false;
        if (NavMesh.Raycast(start, nodes[first], out _, NavMesh.AllAreas))
            return false;
        System.Array.Clear(closed, 0, closed.Length);
        System.Array.Clear(opened, 0, opened.Length);
        open.Clear();
        for (int i = 0; i < g.Length; i++)
        {
            g[i] = float.PositiveInfinity;
            parent[i] = -1;
        }

        g[first] = 0;
        open.Add(first);
        opened[first] = true;
        while (open.Count > 0 && LastExpanded < expansionBudget)
        {
            int best = 0;
            float score = float.PositiveInfinity;
            for (int i = 0; i < open.Count; i++)
            {
                int id = open[i];
                float f = g[id] + Vector3.Distance(nodes[id], nodes[last]);
                if (f < score)
                {
                    score = f;
                    best = i;
                }
            }

            int current = open[best];
            open.RemoveAt(best);
            opened[current] = false;
            closed[current] = true;
            LastExpanded++;
            if (current == last)
            {
                if (NavMesh.Raycast(nodes[last], goal, out _, NavMesh.AllAreas))
                    return false;
                LastPathCost = g[last];
                for (int at = last; at >= 0; at = parent[at])
                    result.Add(nodes[at]);
                result.Reverse();
                result.Add(goal);
                return true;
            }

            int cx = current % columns, cz = current / columns;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (dx == 0 && dz == 0 || x < 0 || z < 0 || x >= columns || z >= rows)
                        continue;
                    int next = z * columns + x;
                    if (!walkable[next] || closed[next])
                        continue;
                    float candidate = g[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (candidate >= g[next])
                        continue;
                    // Dynamic carving can invalidate a previously walkable edge; query it at expansion time.
                    if (NavMesh.Raycast(nodes[current], nodes[next], out _, NavMesh.AllAreas))
                        continue;
                    g[next] = candidate;
                    parent[next] = current;
                    if (!opened[next])
                    {
                        opened[next] = true;
                        open.Add(next);
                    }
                }
        }

        return false;
    }

    private int Nearest(Vector3 p)
    {
        int best = -1;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (!walkable[i])
                continue;
            float d = (nodes[i] - p).sqrMagnitude;
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }

        return best;
    }
}
