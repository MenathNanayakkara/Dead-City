using UnityEngine;

/// <summary>Contract for future world objects the player can interact with.</summary>
public interface IInteractable
{
    string InteractionPrompt { get; }

    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
