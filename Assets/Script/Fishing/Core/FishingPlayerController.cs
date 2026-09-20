using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves the angler across the shore while a fishing encounter is active.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class FishingPlayerController : MonoBehaviour
{
    private const float GroundedVerticalVelocity = -2f;

    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private BoxCollider shoreBounds;
    [SerializeField] private Transform fishTransform;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
    [SerializeField, Min(0f)] private float jumpSpeed = 6f;
    [SerializeField, Min(0f)] private float gravity = 20f;
    [SerializeField, Min(0f)] private float edgePadding = 0.45f;

    [Header("Fishing Line Limit")]
    [SerializeField, Min(0f)] private float lineSoftLimitDistance = 3f;
    [SerializeField, Min(0f)] private float lineHardLimitDistance = 6f;
    [SerializeField, Range(0.05f, 1f)] private float minimumSpeedAtLineLimit = 0.2f;

    private float verticalVelocity;

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || characterController == null)
        {
            return;
        }

        UpdateVerticalVelocity(keyboard);
        FaceFish();
        MoveOnShore(ReadMovementInput(keyboard));
    }

    private Vector2 ReadMovementInput(Keyboard keyboard)
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            vertical += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            vertical -= 1f;
        }


        return new Vector2(horizontal, vertical).normalized;
    }

    private void UpdateVerticalVelocity(Keyboard keyboard)
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = GroundedVerticalVelocity;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = jumpSpeed;
            }
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }
    }

    private void MoveOnShore(Vector2 input)
    {
        Vector3 cameraForward = gameplayCamera != null
            ? gameplayCamera.transform.forward
            : transform.forward;

        Vector3 cameraRight = gameplayCamera != null
            ? gameplayCamera.transform.right
            : transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 horizontalDisplacement =
            (cameraForward * input.y + cameraRight * input.x) *
            moveSpeed *
            GetLineSpeedMultiplier() *
            Time.deltaTime;

        horizontalDisplacement = PreventLineOverextension(
            horizontalDisplacement);
        horizontalDisplacement = ClampToShore(horizontalDisplacement);

        Vector3 displacement = horizontalDisplacement;
        displacement.y = verticalVelocity * Time.deltaTime;
        characterController.Move(displacement);
    }

    private float GetLineSpeedMultiplier()
    {
        float linePressure = GetLinePressure();

        return Mathf.Lerp(
            1f,
            minimumSpeedAtLineLimit,
            linePressure);
    }

    private Vector3 PreventLineOverextension(
        Vector3 horizontalDisplacement)
    {
        if (fishTransform == null)
        {
            return horizontalDisplacement;
        }

        Vector3 playerToFish = fishTransform.position - transform.position;
        playerToFish.y = 0f;

        if (playerToFish.sqrMagnitude <= 0.001f)
        {
            return horizontalDisplacement;
        }

        Vector3 directionToFish = playerToFish.normalized;
        float outwardMovement = Vector3.Dot(
            horizontalDisplacement,
            -directionToFish);

        if (GetHorizontalDistanceToFish() >= lineHardLimitDistance &&
            outwardMovement > 0f)
        {
            horizontalDisplacement -=
                -directionToFish *
                outwardMovement;
        }

        return horizontalDisplacement;
    }

    private float GetLinePressure()
    {
        if (fishTransform == null ||
            lineHardLimitDistance <= lineSoftLimitDistance)
        {
            return 0f;
        }

        return Mathf.InverseLerp(
            lineSoftLimitDistance,
            lineHardLimitDistance,
            GetHorizontalDistanceToFish());
    }

    private float GetHorizontalDistanceToFish()
    {
        Vector3 fishPosition = fishTransform.position;
        Vector3 playerPosition = transform.position;
        fishPosition.y = 0f;
        playerPosition.y = 0f;

        return Vector3.Distance(playerPosition, fishPosition);
    }

    private void FaceFish()
    {
        if (fishTransform == null)
        {
            return;
        }

        Vector3 directionToFish = fishTransform.position - transform.position;
        directionToFish.y = 0f;

        if (directionToFish.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(
                directionToFish.normalized,
                Vector3.up);
        }
    }

    private Vector3 ClampToShore(Vector3 horizontalDisplacement)
    {
        if (shoreBounds == null)
        {
            return horizontalDisplacement;
        }

        Bounds bounds = shoreBounds.bounds;
        Vector3 targetPosition = transform.position + horizontalDisplacement;

        float minimumX = bounds.min.x + edgePadding;
        float maximumX = bounds.max.x - edgePadding;
        float minimumZ = bounds.min.z + edgePadding;
        float maximumZ = bounds.max.z - edgePadding;

        targetPosition.x = Mathf.Clamp(
            targetPosition.x,
            minimumX,
            maximumX);
        targetPosition.z = Mathf.Clamp(
            targetPosition.z,
            minimumZ,
            maximumZ);

        return new Vector3(
            targetPosition.x - transform.position.x,
            0f,
            targetPosition.z - transform.position.z);
    }
}
