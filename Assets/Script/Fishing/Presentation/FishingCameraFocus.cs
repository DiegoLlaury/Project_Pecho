using UnityEngine;

/// <summary>
/// Keeps the gameplay camera focused on the hooked fish as the angler moves.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class FishingCameraFocus : MonoBehaviour
{
    [SerializeField] private Transform fishTransform;
    [SerializeField, Min(0f)] private float fishFocusHeight = 0.4f;

    private void LateUpdate()
    {
        if (fishTransform == null)
        {
            return;
        }

        Vector3 focusPosition =
            fishTransform.position + Vector3.up * fishFocusHeight;

        Vector3 directionToFish = focusPosition - transform.position;

        if (directionToFish.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(
                directionToFish,
                Vector3.up);
        }
    }
}
