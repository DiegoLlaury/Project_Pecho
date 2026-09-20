using UnityEngine;

/// <summary>
/// Keeps both hands attached to the fishing rod and closes the fingers around its grips.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class FishingRodHandIK : MonoBehaviour
{
    private const float MinimumIkWeight = 0f;
    private const float MaximumIkWeight = 1f;
    private const float DefaultFingerCurl = 55f;
    private const float DefaultThumbCurl = 35f;

    private static readonly HumanBodyBones[] FingerBones =
    {
        HumanBodyBones.LeftIndexProximal,
        HumanBodyBones.LeftIndexIntermediate,
        HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal,
        HumanBodyBones.LeftMiddleIntermediate,
        HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal,
        HumanBodyBones.LeftRingIntermediate,
        HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal,
        HumanBodyBones.LeftLittleIntermediate,
        HumanBodyBones.LeftLittleDistal,
        HumanBodyBones.RightIndexProximal,
        HumanBodyBones.RightIndexIntermediate,
        HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal,
        HumanBodyBones.RightMiddleIntermediate,
        HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal,
        HumanBodyBones.RightRingIntermediate,
        HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal,
        HumanBodyBones.RightLittleIntermediate,
        HumanBodyBones.RightLittleDistal
    };

    private static readonly HumanBodyBones[] ThumbBones =
    {
        HumanBodyBones.LeftThumbProximal,
        HumanBodyBones.LeftThumbIntermediate,
        HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.RightThumbProximal,
        HumanBodyBones.RightThumbIntermediate,
        HumanBodyBones.RightThumbDistal
    };

    [Header("Rod Grip Targets")]
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;

    [Header("IK Weights")]
    [SerializeField, Range(MinimumIkWeight, MaximumIkWeight)]
    private float positionWeight = MaximumIkWeight;
    [SerializeField, Range(MinimumIkWeight, MaximumIkWeight)]
    private float rotationWeight = MaximumIkWeight;

    [Header("Hand Grip")]
    [SerializeField, Range(MinimumIkWeight, MaximumIkWeight)]
    private float gripWeight = MaximumIkWeight;
    [SerializeField, Range(0f, 90f)] private float fingerCurl = DefaultFingerCurl;
    [SerializeField, Range(0f, 90f)] private float thumbCurl = DefaultThumbCurl;

    private Animator characterAnimator;
    private Transform[] fingerTransforms;
    private Transform[] thumbTransforms;

    private void Awake()
    {
        characterAnimator = GetComponent<Animator>();
        fingerTransforms = GetBoneTransforms(FingerBones);
        thumbTransforms = GetBoneTransforms(ThumbBones);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (characterAnimator == null)
        {
            return;
        }

        ApplyHandIK(AvatarIKGoal.LeftHand, leftHandTarget);
        ApplyHandIK(AvatarIKGoal.RightHand, rightHandTarget);
    }

    private void LateUpdate()
    {
        ApplyGrip(fingerTransforms, fingerCurl);
        ApplyGrip(thumbTransforms, thumbCurl);
    }

    private void ApplyHandIK(AvatarIKGoal hand, Transform target)
    {
        float activePositionWeight = target != null
            ? positionWeight
            : MinimumIkWeight;
        float activeRotationWeight = target != null
            ? rotationWeight
            : MinimumIkWeight;

        characterAnimator.SetIKPositionWeight(hand, activePositionWeight);
        characterAnimator.SetIKRotationWeight(hand, activeRotationWeight);

        if (target == null)
        {
            return;
        }

        characterAnimator.SetIKPosition(hand, target.position);
        characterAnimator.SetIKRotation(hand, target.rotation);
    }

    private Transform[] GetBoneTransforms(HumanBodyBones[] bones)
    {
        Transform[] boneTransforms = new Transform[bones.Length];
        for (int index = 0; index < bones.Length; index++)
        {
            boneTransforms[index] = characterAnimator.GetBoneTransform(bones[index]);
        }

        return boneTransforms;
    }

    private void ApplyGrip(Transform[] boneTransforms, float curlAngle)
    {
        if (boneTransforms == null)
        {
            return;
        }

        Quaternion curlRotation = Quaternion.Euler(curlAngle * gripWeight, 0f, 0f);
        foreach (Transform boneTransform in boneTransforms)
        {
            if (boneTransform != null)
            {
                boneTransform.localRotation *= curlRotation;
            }
        }
    }
}
