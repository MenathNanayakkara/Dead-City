using UnityEngine;

/// <summary>Shared look/input camera with first-person and legacy shoulder views.</summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField]
    private Transform cameraTarget;
    [SerializeField]
    private Transform aimTarget;
    [SerializeField]
    private PlayerAimController aim;
    [Header("Virtual camera modes")]
    [SerializeField]
    private ThirdPersonCameraMode explorationCamera;
    [SerializeField]
    private ThirdPersonCameraMode aimCamera;
    [SerializeField, Min(0.01f)]
    private float modeBlendTime = 0.2f;
    [Header("Orbit and fallback exploration pose")]
    [SerializeField, Min(0.1f)]
    private float followDistance = 4.5f;
    [SerializeField]
    private float lookAtHeightOffset = 0.05f;
    [SerializeField]
    private float startingPitch = 12f;
    [SerializeField]
    private float minimumPitch = -25f;
    [SerializeField]
    private float maximumPitch = 65f;
    [SerializeField, Min(0f)]
    private float sensitivity = 2.5f;
    [SerializeField, Min(0f)]
    private float positionSmoothTime = 0.08f;
    [SerializeField, Min(1f)]
    private float aimTargetDistance = 100f;
    [Header("Collision")]
    [SerializeField, Min(0.01f)]
    private float collisionRadius = 0.2f;
    [SerializeField, Min(0f)]
    private float collisionPadding = 0.05f;
    [SerializeField]
    private LayerMask collisionLayers = Physics.DefaultRaycastLayers;
    [Header("Mouse")]
    [SerializeField]
    private bool lockCursorAtStart = true;
    [SerializeField]
    private bool invertMouseY;
    [Header("First person")]
    [SerializeField] private bool firstPerson;
    [SerializeField, Range(45f, 100f)] private float firstPersonFieldOfView = 75f;
    [SerializeField, Range(30f, 80f)] private float firstPersonAimFieldOfView = 55f;
    [SerializeField, Min(.05f)] private float eyeInset = .14f;
    private CharacterController playerCapsule;
    private readonly RaycastHit[] collisionHits = new RaycastHit[32];
    private Camera outputCamera;
    private Transform playerRoot;
    private PlayerStanceController stance;
    private float yaw;
    private float pitch;
    private float aimBlend;
    private float blendVelocity;
    private Vector3 pivot;
    private Vector3 pivotVelocity;
    private bool initialized;
    private bool cursorManagedByUI;
    private bool gameplayInputEnabled = true;
    public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;
    public float AimBlend => aimBlend;
    public int CursorLockFrame { get; private set; } = -1;
    public bool GameplayInputEnabled => gameplayInputEnabled;
    public bool IsFirstPerson => firstPerson;

    public void SetCursorManagedByUI(bool managed) => cursorManagedByUI = managed;
    public void SetGameplayInputEnabled(bool value)
    {
        gameplayInputEnabled = value;
        SetCursorLocked(value);
    }

    /// <summary>Permanent orbit kick in degrees. Positive pitch kicks the view upward.</summary>
    public void ApplyRecoil(float upwardDegrees, float sidewaysDegrees)
    {
        pitch = Mathf.Clamp(pitch - upwardDegrees, minimumPitch, maximumPitch);
        yaw = Mathf.Repeat(yaw + sidewaysDegrees, 360f);
    }

    public float FollowDistance
    {
        get => explorationCamera != null ? explorationCamera.Distance : followDistance;
        set
        {
            followDistance = Mathf.Max(0.3f, value);
            if (explorationCamera != null)
                explorationCamera.Distance = followDistance;
        }
    }

    public float Sensitivity { get => sensitivity; set => sensitivity = Mathf.Max(0f, value); }

    private void Awake()
    {
        outputCamera = GetComponent<Camera>();
        if (cameraTarget == null)
        {
            ThirdPersonPlayerController player = FindAnyObjectByType<ThirdPersonPlayerController>();
            if (player != null)
                cameraTarget = player.transform.Find("CameraTarget") ?? player.transform;
        }

        if (cameraTarget != null)
        {
            ThirdPersonPlayerController player = cameraTarget.GetComponentInParent<ThirdPersonPlayerController>();
            playerRoot = player != null ? player.transform : null;
            if (playerRoot != null) playerCapsule = playerRoot.GetComponent<CharacterController>();
            if(playerRoot!=null)stance=playerRoot.GetComponent<PlayerStanceController>();
            if (aim == null && playerRoot != null)
                aim = playerRoot.GetComponent<PlayerAimController>();
            yaw = cameraTarget.eulerAngles.y;
        }

        pitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);
    }

    private void OnEnable() => SetCursorLocked(gameplayInputEnabled && lockCursorAtStart);
    private void Update()
    {
        if (!gameplayInputEnabled || Time.timeScale <= 0f)
            return;
        if (!cursorManagedByUI)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLocked(false);
            else if (!CursorLocked && Application.isFocused && Input.GetMouseButtonDown(0))
                SetCursorLocked(true);
        }

        if (!CursorLocked || !Application.isFocused || Time.timeScale <= 0f)
            return;
        float multiplier = Mathf.Lerp(explorationCamera != null ? explorationCamera.SensitivityMultiplier : 1f, aimCamera != null ? aimCamera.SensitivityMultiplier : 0.65f, aimBlend);
        yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * sensitivity * multiplier, 360f);
        pitch = Mathf.Clamp(pitch + Input.GetAxisRaw("Mouse Y") * sensitivity * multiplier * (invertMouseY ? 1f : -1f), minimumPitch, maximumPitch);
    }

    private void LateUpdate()
    {
        if (cameraTarget == null || Time.deltaTime <= 0f)
            return;
        aimBlend = Mathf.SmoothDamp(aimBlend, aim != null && aim.IsAiming ? 1f : 0f, ref blendVelocity, modeBlendTime);
        if (firstPerson && playerRoot != null && playerCapsule != null)
        {
            // The capsule owns the eye position. Bone motion and follow damping
            // cannot introduce head bob, wall peeking or locomotion lag.
            Vector3 eye = playerCapsule.center + Vector3.up * (playerCapsule.height * .5f - eyeInset);
            transform.SetPositionAndRotation(playerRoot.TransformPoint(eye), Quaternion.Euler(pitch, yaw, 0f));
            outputCamera.fieldOfView = Mathf.Lerp(firstPersonFieldOfView, firstPersonAimFieldOfView, aimBlend);
            UpdateAimTarget();
            return;
        }
        Vector3 targetPivot = cameraTarget.position;
        if(stance!=null)targetPivot+=Vector3.down*(.38f*stance.Blend);
        if (!initialized)
        {
            pivot = targetPivot;
            initialized = true;
        }
        else
            pivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity, positionSmoothTime);
        float distance = Mathf.Lerp(explorationCamera != null ? explorationCamera.Distance : followDistance, aimCamera != null ? aimCamera.Distance : 2.25f, aimBlend);
        Vector2 offset = Vector2.Lerp(explorationCamera != null ? explorationCamera.ShoulderOffset : new Vector2(0.2f, lookAtHeightOffset), aimCamera != null ? aimCamera.ShoulderOffset : new Vector2(0.6f, 0.08f), aimBlend);
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = pivot + orbit * new Vector3(offset.x, offset.y, -distance);
        // Re-solve collision from the current player pivot after smoothing, including the shoulder offset.
        // This avoids interpolating the camera through a wall during a mode change.
        outputCamera.fieldOfView = Mathf.Lerp(explorationCamera != null ? explorationCamera.FieldOfView : 60f, aimCamera != null ? aimCamera.FieldOfView : 45f, aimBlend);
        float radius = Mathf.Max(collisionRadius, NearPlaneRadius());
        Vector3 safe = ConstrainCamera(targetPivot, desired, radius);
        transform.SetPositionAndRotation(safe, orbit);
        UpdateAimTarget();
    }

    private void UpdateAimTarget()
    {
        if (aimTarget != null)
        {
            Ray ray = outputCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float distanceToAim = NearestHit(ray.origin, 0f, ray.direction, aimTargetDistance);
            aimTarget.position = ray.GetPoint(distanceToAim);
        }
    }

    private Vector3 ConstrainCamera(Vector3 origin, Vector3 desired, float radius)
    {
        Vector3 delta = desired - origin;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return origin;
        float allowed = NearestHit(origin, radius, delta / distance, distance);
        // A ray still detects the wall if a large probe starts overlapping it.
        float rayDistance = NearestHit(origin, 0f, delta / distance, distance);
        if (rayDistance < distance)
            allowed = Mathf.Min(allowed, Mathf.Max(0f, rayDistance - radius));
        return origin + delta / distance * Mathf.Max(0f, allowed < distance ? allowed - collisionPadding : distance);
    }

    private float NearestHit(Vector3 origin, float radius, Vector3 direction, float distance)
    {
        int count = radius > 0f ? Physics.SphereCastNonAlloc(origin, radius, direction, collisionHits, distance, collisionLayers, QueryTriggerInteraction.Ignore) : Physics.RaycastNonAlloc(origin, direction, collisionHits, distance, collisionLayers, QueryTriggerInteraction.Ignore);
        float nearest = distance;
        for (int i = 0; i < count; i++)
        {
            Transform hit = collisionHits[i].transform;
            if (hit == null || (playerRoot != null && hit.IsChildOf(playerRoot)))
                continue;
            nearest = Mathf.Min(nearest, collisionHits[i].distance);
        }

        return nearest;
    }

    private float NearPlaneRadius()
    {
        float halfHeight = Mathf.Tan(outputCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * outputCamera.nearClipPlane;
        return Mathf.Sqrt(halfHeight * halfHeight * (1f + outputCamera.aspect * outputCamera.aspect));
    }

    private void SetCursorLocked(bool locked)
    {
        if (locked && !CursorLocked)
            CursorLockFrame = Time.frameCount;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            SetCursorLocked(false);
    }

    private void OnDisable() => SetCursorLocked(false);
}
