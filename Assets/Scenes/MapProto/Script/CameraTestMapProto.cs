using UnityEngine;
using UnityEngine.InputSystem;

public class PrototypeCamera : MonoBehaviour
{
    [Header("TARGET")]
    [SerializeField] private Transform target;

    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);

    [Header("CAMERA")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float pitch = 42f;
    [SerializeField] private float yaw = 45f;

    [Header("FOLLOW")]
    [SerializeField] private float followSmoothTime = 0.10f;
    [SerializeField] private float rotationSmoothSpeed = 10f;

    [Header("ROTATION")]
    [SerializeField] private bool allowRotation = true;
    [SerializeField] private float rotationSpeed = 0.15f;

    [Header("ZOOM")]
    [SerializeField] private bool allowZoom = true;
    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 8f;

    [Header("COLLISION")]
    [SerializeField] private bool cameraCollision = true;
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float collisionOffset = 0.15f;

    private float targetDistance;
    private Vector3 velocity;

    private void Start()
    {
        targetDistance = distance;

        pitch = Mathf.Clamp(pitch, 20f, 65f);

        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        HandleRotation();
        HandleZoom();
        UpdateCamera();
    }

    // ============================================================
    // ROTATION
    // ============================================================

    private void HandleRotation()
    {
        if (!allowRotation)
            return;

        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        if (mouseDelta.sqrMagnitude < 0.01f)
            return;

        yaw += mouseDelta.x * rotationSpeed;

        pitch -= mouseDelta.y * rotationSpeed;

        pitch = Mathf.Clamp(pitch, 20f, 65f);
    }

    // ============================================================
    // ZOOM
    // ============================================================

    private void HandleZoom()
    {
        if (!allowZoom)
            return;

        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        targetDistance -= scroll * zoomSpeed;

        targetDistance = Mathf.Clamp(
            targetDistance,
            minDistance,
            maxDistance
        );
    }

    // ============================================================
    // CAMERA
    // ============================================================

    private void UpdateCamera()
    {
        Vector3 lookTarget = target.position + targetOffset;

        Quaternion rotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        Vector3 direction = rotation * Vector3.back;

        float finalDistance = targetDistance;

        // --------------------------------------------------------
        // COLLISION
        // --------------------------------------------------------

        if (cameraCollision)
        {
            RaycastHit hit;

            if (Physics.SphereCast(
                lookTarget,
                collisionRadius,
                direction,
                out hit,
                targetDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
            {
                finalDistance = Mathf.Max(
                    0.2f,
                    hit.distance - collisionOffset
                );
            }
        }

        Vector3 desiredPosition =
            lookTarget + direction * finalDistance;

        // --------------------------------------------------------
        // POSITION
        // --------------------------------------------------------

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            followSmoothTime
        );

        // --------------------------------------------------------
        // LOOK AT TARGET
        // --------------------------------------------------------

        Quaternion desiredRotation = Quaternion.LookRotation(
            lookTarget - transform.position,
            Vector3.up
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    // ============================================================
    // SNAP
    // ============================================================

    private void SnapToTarget()
    {
        if (target == null)
            return;

        Vector3 lookTarget = target.position + targetOffset;

        Quaternion rotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        Vector3 direction = rotation * Vector3.back;

        transform.position =
            lookTarget + direction * distance;

        transform.LookAt(lookTarget);
    }
}