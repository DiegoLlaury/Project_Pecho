using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerMovement : MonoBehaviour
{
    public Transform meshTransform;
    public Animator animator;
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 90f;
    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
    [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

    [Tooltip("How fast the model turns to face the input direction, in seconds")]
    [Range(0f, 0.3f)]
    public float rotationSmoothTime = 0.12f;

    private Vector3 moveDirection = Vector3.zero;
    private CharacterController characterController;
    private IPlayerInput input;

    private float targetRotation;
    private float rotationVelocity;

    private bool canMove = true;
    private bool jumpQueued;

    // must match the parameters that exist on the Animator Controller (Idle Walk Run Blend etc.)
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimGrounded = Animator.StringToHash("Grounded");
    private static readonly int AnimJump = Animator.StringToHash("Jump");

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputHandler>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (meshTransform == null) meshTransform = animator != null ? animator.transform : transform;
    }

    void OnEnable()
    {
        input.JumpPressed += OnJumpPressed;
    }

    void OnDisable()
    {
        input.JumpPressed -= OnJumpPressed;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnJumpPressed()
    {
        jumpQueued = true;
    }

    void Update()
    {
        bool isRunning = input.IsRunning;
        Vector2 move = input.MoveInput; // x = A/D, y = W/S, world-space axes

        float speed = isRunning ? runSpeed : walkSpeed;
        float movementDirectionY = moveDirection.y;

        // MOVEMENT: always raw world-space direction — never depends on the mesh's
        // (possibly still-transitioning) rotation, so alternating inputs can't cause drift.
        Vector3 worldMove = canMove ? new Vector3(move.x, 0f, move.y) * speed : Vector3.zero;
        moveDirection = worldMove;
        moveDirection.y = movementDirectionY;

        bool grounded = characterController.isGrounded;

        if (jumpQueued && canMove && grounded)
        {
            moveDirection.y = jumpPower;
        }

        if (!grounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        characterController.Move(moveDirection * Time.deltaTime);

        // VISUAL ONLY: the mesh child rotates to face input direction, purely cosmetic
        if (move != Vector2.zero)
        {
            targetRotation = Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg;
            float smoothedAngle = Mathf.SmoothDampAngle(meshTransform.eulerAngles.y, targetRotation,
                ref rotationVelocity, rotationSmoothTime);
            meshTransform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
        }

        UpdateAnimator(move, grounded);

        jumpQueued = false;
    }
    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f && FootstepAudioClips.Length > 0)
        {
            var index = Random.Range(0, FootstepAudioClips.Length);
            AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(characterController.center), FootstepAudioVolume);
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f && LandingAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(characterController.center), FootstepAudioVolume);
        }
    }

    void UpdateAnimator(Vector2 move, bool grounded)
    {
        if (animator == null) return;

        animator.SetFloat(AnimSpeed, move.magnitude * (input.IsRunning ? runSpeed : walkSpeed));
        animator.SetBool(AnimGrounded, grounded);

        if (jumpQueued && grounded)
        {
            animator.SetTrigger(AnimJump);
        }
    }
}