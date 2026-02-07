using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField, Range(1f, 89f)] private float maxPitch = 85f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Interact")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayer = ~0;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    private CharacterController controller;
    private Vector3 velocity;
    private float cameraPitch = 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        // »ØÍËµ½Ö÷ÉãÏñ»ú£¬±ÜÃâÎ´¸³Öµµ¼ÖÂÎÞ·¨¸©Ñö
        if (playerCamera == null)
            playerCamera = Camera.main;

        // Ëø¶¨²¢Òþ²Ø¹â±ê£¨ÔËÐÐÊ±£©
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
        HandleInteract();
    }

    private void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // ºáÏòÓÉ½ÇÉ«×ª¶¯£¨Yaw£©
        transform.Rotate(Vector3.up * mouseX);

        // ¸©Ñö£¨Pitch£©£¬ÔÚÉãÏñ»ú±¾µØ¿Õ¼äÖÐÓ¦ÓÃ
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxPitch, maxPitch);

        if (playerCamera != null)
        {
            // Ê¹ÓÃ Quaternion ¸ü¿É¿¿µØÉèÖÃ¾Ö²¿¸©Ñö
            playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void HandleMove()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        Vector3 horizontalVelocity = move * walkSpeed;

        // Ó¦ÓÃË®Æ½ÒÆ¶¯
        controller.Move(horizontalVelocity * Time.deltaTime);

        // ¼òµ¥ÖØÁ¦
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f; // ±£³ÖÌùµØ

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleInteract()
    {
        if (playerCamera == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            // ³¢ÊÔ»ñÈ¡½»»¥½Ó¿Ú
            var interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                // ¿ÉÔÚ´Ë´¦ÏÔÊ¾½»»¥ÌáÊ¾£¨TODO£©

                if (Input.GetKeyDown(KeyCode.E))
                {
                    interactable.OnInteract();
                }
            }
        }
    }

    // ¿ÉÔÚÐèÒªÊ±½âËø¹â±ê
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}