using UnityEngine;

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
    public Camera activeCam;
    public bool isRunning;
    [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

    [Tooltip("How fast the model turns to face the input direction, in seconds")]
    [Range(0f, 0.3f)]
    public float rotationSmoothTime = 0.12f;

    private Vector3 moveDirection = Vector3.zero;
    private CharacterController characterController;
    private IPlayerInput input;
    private Vector2 move;
    private Vector3 worldMove;
    private float movementDirectionY;

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
        
        if (move != input.MoveInput || isRunning != input.IsRunning)
        {
            isRunning = input.IsRunning;
            move = input.MoveInput;

            Vector3 forwardDirection = (new Vector3(activeCam.transform.forward.x, 0, activeCam.transform.forward.z)).normalized;
            Vector3 rightDirection = (new Vector3(activeCam.transform.right.x, 0, activeCam.transform.right.z)).normalized;

            float speed = isRunning ? runSpeed : walkSpeed;
            worldMove = canMove ? (forwardDirection * move.y + rightDirection * move.x) * speed : Vector3.zero;
        }

        moveDirection = worldMove;

        bool grounded = characterController.isGrounded;

        if (jumpQueued && canMove && grounded)
        {
            moveDirection.y = jumpPower;
        }
        else if (grounded)
        {
            moveDirection.y = movementDirectionY < 0f ? -2f : movementDirectionY;
        }
        else
        {
            moveDirection.y = movementDirectionY - gravity * Time.deltaTime;
        }

        grounded = characterController.isGrounded;

        movementDirectionY = moveDirection.y;

        characterController.Move(moveDirection * Time.deltaTime);


        if (move != Vector2.zero)
        {
            targetRotation = Quaternion.LookRotation(worldMove).eulerAngles.y;
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