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

    [Header("UI")]
    [SerializeField, Tooltip("准星组件（可留空自动查找）")] private Crosshair crosshair;

    private CharacterController controller;
    private Vector3 velocity;
    private float cameraPitch = 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        // 回退到主摄像机，避免未赋值导致无法俯仰
        if (playerCamera == null)
            playerCamera = Camera.main;

        // 尝试自动查找准星
        if (crosshair == null)
            crosshair = FindObjectOfType<Crosshair>();

        // 锁定并隐藏光标（运行时）
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

        // 横向由角色转动（Yaw）
        transform.Rotate(Vector3.up * mouseX);

        // 俯仰（Pitch），在摄像机本地空间中应用
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxPitch, maxPitch);

        if (playerCamera != null)
        {
            // 使用 Quaternion 更可靠地设置局部俯仰
            playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void HandleMove()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        Vector3 horizontalVelocity = move * walkSpeed;

        // 应用水平移动
        controller.Move(horizontalVelocity * Time.deltaTime);

        // 简单重力
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f; // 保持贴地

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleInteract()
    {
        if (playerCamera == null)
        {
            if (crosshair != null) crosshair.SetHighlighted(false);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hitInteractable = false;

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            // 尝试获取交互接口
            var interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                hitInteractable = true;

                // 可在此处显示交互提示（TODO）

                if (Input.GetKeyDown(KeyCode.E))
                {
                    interactable.OnInteract();
                }
            }
        }

        if (crosshair != null)
            crosshair.SetHighlighted(hitInteractable);
    }

    // 可在需要时解锁光标
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

