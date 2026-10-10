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

}
