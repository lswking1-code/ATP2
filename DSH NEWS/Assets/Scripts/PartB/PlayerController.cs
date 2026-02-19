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
    [SerializeField, Tooltip("准星组件")] private Crosshair crosshair;

    [Header("Footsteps")]
    [SerializeField, Tooltip("脚步音效（可配置多个用于随机播放）")]
    private AudioClip[] footstepClips;
    [SerializeField, Tooltip("每走多少米触发一次脚步声（世界单位）"), Min(0.05f)]
    private float stepDistance = 0.8f;
    [SerializeField, Range(0f, 1f), Tooltip("脚步音量（由 AudioManager 的 sfxVolume 再乘）")]
    private float stepVolume = 1f;
    [SerializeField, Tooltip("移动最小阈值（水平速度平方阈值），低于该值不播放脚步声")]
    private float minMoveSpeedSqr = 0.01f;

    private CharacterController controller;
    private Vector3 velocity;
    private float cameraPitch = 0f;

    // 脚步相关
    private Vector3 lastPosition;
    private float stepAccumulator = 0f;

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
            crosshair = Object.FindFirstObjectByType<Crosshair>();

        // 初始化脚步位置记录
        lastPosition = transform.position;

        // 锁定并隐藏光标（运行时）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
        HandleInteract();
        HandleFootsteps();
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

    // 根据水平位移累积距离并在达到设定步距时播放脚步声
    private void HandleFootsteps()
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            // 未配置脚步音频则不处理，但仍需更新 lastPosition
            lastPosition = transform.position;
            stepAccumulator = 0f;
            return;
        }

        // 仅在地面上才触发脚步逻辑
        if (!controller.isGrounded)
        {
            lastPosition = transform.position;
            return;
        }

        // 计算水平位移（忽略 y）
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        float dist = delta.magnitude;

        // 使用位移 / deltaTime 计算水平速度（比依赖 controller.velocity 更可靠）
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);
        float horizSpeed = dist / dt;
        float horizSpeedSqr = horizSpeed * horizSpeed;

        // 如果速度太低则不累积步距（避免抖动触发）
        if (horizSpeedSqr < minMoveSpeedSqr)
        {
            lastPosition = transform.position;
            stepAccumulator = 0f;
            return;
        }

        stepAccumulator += dist;

        // 调试：运行时查看输出确认逻辑生效（确认后可删除）
        Debug.Log($"Footstep: moved {dist:F3}m, speed {horizSpeed:F3}m/s, accumulator={stepAccumulator:F3}/{stepDistance:F3}");

        if (stepAccumulator >= stepDistance)
        {
            PlayFootstep();
            stepAccumulator = 0f;
        }

        lastPosition = transform.position;
    }

    private void PlayFootstep()
    {
        if (AudioManager.Instance == null) return;
        Debug.Log("Playing footstep sound");
        if (footstepClips == null || footstepClips.Length == 0) return;

        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip == null) return;

        AudioManager.Instance.PlaySFX(clip, Mathf.Clamp01(stepVolume));
    }

    // 可在需要时解锁光标
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

