using UnityEngine;

/// <summary>Hinged door shared by player interaction and nearby enemy traversal.</summary>
public sealed class InteractiveDoor : MonoBehaviour, IInteractable
{
    [SerializeField]
    private float openAngle = 100f;
    [SerializeField, Min(10)]
    private float degreesPerSecond = 140f;
    [SerializeField]
    private bool locked;
    private Quaternion closedRotation;
    public bool IsOpen { get; private set; }
    public bool IsLocked => locked;
    public string InteractionPrompt => locked ? "LOCKED" : IsOpen ? "E  CLOSE DOOR" : "E  OPEN DOOR";

    private void Awake() => closedRotation = transform.localRotation;
    public bool CanInteract(GameObject interactor) => !locked;
    public void Interact(GameObject interactor)
    {
        if (!locked)
            IsOpen = !IsOpen;
    }

    public void OpenForAgent()
    {
        if (!locked)
            IsOpen = true;
    }

    private void Update()
    {
        var target = closedRotation * Quaternion.Euler(0, IsOpen ? openAngle : 0, 0);
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, degreesPerSecond * Time.deltaTime);
    }
}
