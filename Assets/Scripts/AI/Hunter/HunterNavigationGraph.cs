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
