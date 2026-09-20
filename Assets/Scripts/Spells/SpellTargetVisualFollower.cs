using UnityEngine;

/// <summary>Fait suivre une cible à un VFX sans hériter de sa rotation ni de son échelle.</summary>
public sealed class SpellTargetVisualFollower : MonoBehaviour
{
    private Transform target;

    /// <summary>Configure la cible dont la position doit être suivie.</summary>
    public void Initialize(Transform targetTransform)
    {
        target = targetTransform;
        FollowTarget();
    }

    private void LateUpdate()
    {
        FollowTarget();
    }

    private void FollowTarget()
    {
        if (target != null)
        {
            transform.position = target.position;
        }
    }
}
