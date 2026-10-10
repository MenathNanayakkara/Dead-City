using UnityEngine;

/// <summary>
/// Optional handoff for original team models. Empty fields keep the playable greybox in place.
/// This asset records references only; imported or generated art must keep its real authorship.
/// </summary>
[CreateAssetMenu(menuName = "Dead city/Team Art Catalog")]
public sealed class TeamArtCatalog : ScriptableObject
{
    [Header("Original humanoid model")]
    public GameObject zombieVisualPrefab;
    [Header("Original defensive barricade")]
    public GameObject barricadeVisualPrefab;
    public GameObject brokenDebrisPrefab;
    [Header("Source and contribution record")]
    public string authorOrTeam = "Unassigned";
    public string sourceProjectPath = "Assets/Models/Team/";
    public bool modelsAuthoredByTeam;
}
