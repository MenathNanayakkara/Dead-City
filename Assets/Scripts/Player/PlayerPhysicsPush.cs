using UnityEngine;

public sealed class PlayerPhysicsPush : MonoBehaviour
{
    [SerializeField, Min(0)]
    private float acceleration = 15f;
    [SerializeField, Min(0)]
    private float maximumSpeed = 2f;
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var body = hit.collider.attachedRigidbody;
        if (Time.timeScale <= 0 || body == null || body.isKinematic || hit.moveDirection.y < -.3f || body.linearVelocity.magnitude > maximumSpeed)
            return;
        var horizontal = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.AddForce(horizontal * acceleration, ForceMode.Acceleration);
    }
}
