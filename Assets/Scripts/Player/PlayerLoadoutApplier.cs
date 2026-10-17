using UnityEngine;

[DefaultExecutionOrder(-150)]
public sealed class PlayerLoadoutApplier : MonoBehaviour
{
    [SerializeField]
    private LoadoutCatalog catalog;
    private void Awake()
    {
        var session = LoadoutSession.Ensure(catalog);
        if (!session.IsValid)
        {
            Debug.LogError("Assign a valid LoadoutCatalog before entering ZombieMap.", this);
            return;
        }

        GetComponent<PlayerWeaponController>().ConfigureLoadout(new[] { catalog.primaries[session.Primary], catalog.pistol });
        GetComponent<PlayerEquipment>().Configure(catalog.equipment[session.Equipment]);
    }
}
