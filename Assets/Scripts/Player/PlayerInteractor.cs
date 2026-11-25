using System;
using UnityEngine;

public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField]
    private Camera viewCamera;
    [SerializeField]
    private PlayerHealth health;
    [SerializeField]
    private float reach = 3.2f;
    private IInteractable current;
    private string prompt = "";
    private float nextScan;
    public string Prompt => prompt;

    public event Action<string> PromptChanged;
    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (health == null || viewCamera == null)
        {
            Debug.LogError("PlayerInteractor requires PlayerHealth and the gameplay camera.", this);
            enabled = false;
        }
    }
    private void Update()
    {
        if (Time.timeScale <= 0 || health.IsDead || Cursor.lockState != CursorLockMode.Locked)
        {
            SetPrompt("");
            return;
        }

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .1f;
            current = null;
            var ray = viewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            if (Physics.Raycast(ray, out var hit, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && Vector3.Distance(transform.position, hit.point) <= reach)
                current = hit.collider.GetComponentInParent<IInteractable>();
            SetPrompt(current != null ? current.InteractionPrompt : "");
        }

        if (Input.GetKeyDown(KeyCode.E) && current != null && current.CanInteract(gameObject))
        {
            current.Interact(gameObject);
            SetPrompt(current.InteractionPrompt);
        }
    }

    private void SetPrompt(string value)
    {
        if (prompt == value)
            return;
        prompt = value;
        PromptChanged?.Invoke(value);
    }

    private void OnDisable()
    {
        current = null;
        SetPrompt("");
    }
}
