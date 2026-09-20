using UnityEngine;

/// <summary>
/// Maintains the fish endurance gauge above the fish and facing the active camera.
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class FishEnduranceWorldUi : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform fishTransform;
    [SerializeField] private Camera targetCamera;

    [Header("Presentation")]
    [SerializeField, Min(0f)] private float verticalOffset = 1.1f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (fishTransform == null || targetCamera == null)
        {
            return;
        }

        transform.position =
            fishTransform.position + Vector3.up * verticalOffset;

        Vector3 directionToCamera =
            targetCamera.transform.position - transform.position;

        if (directionToCamera.sqrMagnitude > 0f)
        {
            transform.rotation = Quaternion.LookRotation(
                directionToCamera,
                targetCamera.transform.up);
        }
    }

    private void ResolveReferences()
    {
        if (fishTransform == null)
        {
            fishTransform = transform.parent;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }
}
