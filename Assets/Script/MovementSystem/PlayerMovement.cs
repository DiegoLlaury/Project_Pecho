using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerMovement : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 90f;
    public float defaultHeight = 2f;

    private Vector3 moveDirection = Vector3.zero;
    private CharacterController characterController;
    private IPlayerInput input;

    private bool canMove = true;
    private bool jumpQueued;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputHandler>();
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
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        bool isRunning = input.IsRunning;
        Vector2 move = input.MoveInput;

        float curSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * move.y : 0;
        float curSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * move.x : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (jumpQueued && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }
        jumpQueued = false;

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }


        characterController.Move(moveDirection * Time.deltaTime);
    }
}