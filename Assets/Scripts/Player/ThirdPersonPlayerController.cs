using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform cameraTransform;
    [SerializeField]
    private Transform groundCheck;
    [SerializeField]
    private ThirdPersonCameraController cameraRig;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private PlayerHealth health;
    [Header("Speeds in metres per second")]
    [SerializeField, Min(0.01f)]
    private float walkSpeed = 2.2f;
    [SerializeField, Min(0.01f)]
    private float runSpeed = 4.2f;
    [SerializeField, Min(0.01f)]
    private float sprintSpeed = 6.5f;
    [SerializeField, Min(0f)]
    private float rotationDegreesPerSecond = 720f;
    [Header("Movement smoothing")]
    [SerializeField, Min(1f)] private float acceleration = 14f;
    [SerializeField, Min(1f)] private float braking = 20f;
    [SerializeField, Min(0.01f)] private float turnSmoothTime = 0.09f;
    private Vector3 smoothedVelocity;
    private float turnVelocity;
    [Header("Jump and gravity")]
    [SerializeField, Min(0f)]
    private float jumpHeight = 1.25f;
    [SerializeField]
    private float gravity = -20f;
    [SerializeField]
    private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField]
    private KeyCode sprintKey = KeyCode.LeftControl;
    [SerializeField]
    private KeyCode jumpKey = KeyCode.Space;
    [Header("Ground probe")]
    [SerializeField, Min(0.01f)]
    private float groundProbeRadius = 0.2f;
    [SerializeField]
    private LayerMask groundLayers = Physics.DefaultRaycastLayers;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private CharacterController characterController;
    private PlayerStanceController stance;
    private float verticalVelocity;
    public Vector2 MoveInput { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    public float VerticalVelocity => verticalVelocity;
    public float PlanarSpeed { get; private set; }
    public Vector3 PlanarVelocity { get; private set; }
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float SprintSpeed => sprintSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        stance = GetComponent<PlayerStanceController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraRig == null && cameraTransform != null)
            cameraRig = cameraTransform.GetComponent<ThirdPersonCameraController>();
        if (aim == null)
            aim = GetComponent<PlayerAimController>();
        if (health == null)
            health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || !characterController.enabled)
            return;
        bool alive = health == null || !health.IsDead;
        bool inputAllowed = alive && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        bool aiming = aim != null && aim.IsAiming && alive;
        bool firstPerson = cameraRig != null && cameraRig.IsFirstPerson;
        float x = inputAllowed ? (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f) : 0f;
        float z = inputAllowed ? (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f) : 0f;
        MoveInput = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
        // Never let a ground probe re-ground an ascending jump.
        IsGrounded = verticalVelocity <= 0f && (characterController.isGrounded || ProbeGround());
        if (IsGrounded)
            verticalVelocity = -2f;
        Vector3 forward = cameraRig != null ? cameraRig.PlanarForward : Vector3.ProjectOnPlane(cameraTransform != null ? cameraTransform.forward : transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 direction = Vector3.ClampMagnitude(forward * MoveInput.y + right * MoveInput.x, 1f);
        if (alive && firstPerson)
        {
            // Mouse yaw always defines forward; A/D strafe and S backs up.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            turnVelocity = 0f;
        }
        else if (alive && (aiming || direction.sqrMagnitude > 0.0001f))
        {
            // In aim mode S walks backward and A/D strafe without turning the whole body.
            Vector3 facing = aiming ? forward : direction;
            float targetYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVelocity,
                aiming ? turnSmoothTime * 0.65f : turnSmoothTime, rotationDegreesPerSecond, dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        bool crouching = stance != null && stance.IsCrouching;
        IsSprinting = inputAllowed && !aiming && !crouching && MoveInput.sqrMagnitude > 0.001f &&
            (!firstPerson || MoveInput.y > .1f) && Input.GetKey(sprintKey);
        float speed = aiming ? aim.AimMoveSpeed : IsSprinting ? sprintSpeed : inputAllowed && Input.GetKey(runKey) ? runSpeed : walkSpeed;
        if(crouching)speed=aiming?stance.CrouchedAimSpeed:stance.CrouchedSpeed;
        // Turn into a new direction before accelerating, rather than sliding sideways at full speed.
        if(!firstPerson&&!aiming&&direction.sqrMagnitude>.001f)
            speed*=Mathf.Lerp(.15f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.2f,.95f,Vector3.Dot(transform.forward,direction))));
        Vector3 targetVelocity = direction * speed;
        float rate = targetVelocity.sqrMagnitude < smoothedVelocity.sqrMagnitude ||
            Vector3.Dot(targetVelocity, smoothedVelocity) < 0f ? braking : acceleration;
        smoothedVelocity = inputAllowed ? Vector3.MoveTowards(smoothedVelocity, targetVelocity, rate * dt) : Vector3.zero;
        if (inputAllowed && IsGrounded && !crouching && (stance==null||stance.Blend<.1f) && Input.GetKeyDown(jumpKey))
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            IsGrounded = false;
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -50f);
        Vector3 before = transform.position;
        CollisionFlags flags = characterController.Move((smoothedVelocity + Vector3.up * verticalVelocity) * dt);
        PlanarVelocity = Vector3.ProjectOnPlane((transform.position - before) / dt, Vector3.up);
        PlanarSpeed = PlanarVelocity.magnitude;
        // Feed collisions back into the motor so it does not retain speed against a wall.
        if ((flags & CollisionFlags.Sides) != 0)
            smoothedVelocity = PlanarVelocity;
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
        IsGrounded = verticalVelocity <= 0f && (((flags & CollisionFlags.Below) != 0) || ProbeGround());
        if (IsGrounded)
            verticalVelocity = -2f;
        IsSprinting &= IsGrounded && PlanarSpeed > walkSpeed;
    }

    private void OnDisable()
    {
        smoothedVelocity = PlanarVelocity = Vector3.zero;
        PlanarSpeed = turnVelocity = verticalVelocity = 0f;
        MoveInput = Vector2.zero;
        IsSprinting = false;
    }

    private bool ProbeGround()
    {
        Vector3 point = groundCheck != null ? groundCheck.position : transform.position + Vector3.up * 0.08f;
        // Start above the feet and inspect the contact normal: side walls are not ground.
        Vector3 origin = point + Vector3.up * (groundProbeRadius + 0.04f);
        int count = Physics.SphereCastNonAlloc(origin, groundProbeRadius, Vector3.down, groundHits, 0.16f, groundLayers, QueryTriggerInteraction.Ignore);
        float minimumNormalY = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i].collider;
            if (hit == null || hit == characterController || hit.transform.IsChildOf(transform))
                continue;
            if (groundHits[i].normal.y >= minimumNormalY)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0.01f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
        gravity = Mathf.Min(-0.1f, gravity);
        groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;
        Gizmos.color = IsGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundProbeRadius);
    }
}
