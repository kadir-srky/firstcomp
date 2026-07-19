using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5.0f;
    public float gravity = -9.81f;

    [Header("Camera & Look Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 2.0f;
    public float verticalLookLimit = 80.0f; // Limits vertical head tilt to prevent flipping over
    [Tooltip("Prevents nearby walls from being clipped away by the camera.")]
    [Range(0.01f, 0.2f)] public float cameraNearClipPlane = 0.03f;

    private CharacterController _characterController;
    private float _verticalRotation = 0f;
    private Vector3 _moveDirection;

    void Start()
    {
        _characterController = GetComponent<CharacterController>();

        Camera sceneCamera = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        if (sceneCamera != null)
            sceneCamera.nearClipPlane = cameraNearClipPlane;

        // Lock and hide the cursor for smooth first-person controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        RotateCharacter();
        MoveCharacter();
    }

    void RotateCharacter()
    {
        // Get mouse movement input (X for horizontal, Y for vertical)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Calculate and clamp vertical camera rotation (look up/down)
        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -verticalLookLimit, verticalLookLimit);
        playerCamera.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);

        // Rotate the entire character body horizontally (look left/right)
        transform.Rotate(Vector3.up * mouseX);
    }

    void MoveCharacter()
    {
        // Get WASD or Arrow Keys input (values range from -1 to 1)
        float moveForwardBack = Input.GetAxis("Vertical");
        float moveLeftRight = Input.GetAxis("Horizontal");

        // Calculate movement vector relative to the direction the character is facing
        Vector3 direction = transform.forward * moveForwardBack + transform.right * moveLeftRight;

        // Apply gravity to keep the character grounded
        if (_characterController.isGrounded)
        {
            _moveDirection.y = -0.5f; // Small negative value to ensure the character snaps to the ground
        }
        else
        {
            _moveDirection.y += gravity * Time.deltaTime;
        }

        // Apply movement speed and frame rate independence (Time.deltaTime)
        _moveDirection.x = direction.x * walkSpeed;
        _moveDirection.z = direction.z * walkSpeed;

        _characterController.Move(_moveDirection * Time.deltaTime);
    }
}
