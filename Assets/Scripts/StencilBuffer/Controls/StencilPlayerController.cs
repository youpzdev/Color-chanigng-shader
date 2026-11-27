using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class StencilPlayerController : MonoBehaviour
{
    [SerializeField] private StencilControllerSettings settings;
    [SerializeField] private Camera view;
    [SerializeField] private Transform visual;
    [SerializeField] private StencilViewMode mode;
    [SerializeField] private float initialYaw;

    private CharacterController controller;
    private Vector3 spawn;
    private Quaternion spawnFacing;
    private Vector3 horizontalVelocity;
    private Vector3 cameraTarget;
    private float verticalSpeed;
    private float clock;
    private float lastGrounded = float.NegativeInfinity;
    private float jumpUntil = float.NegativeInfinity;
    private float yaw;
    private float pitch;
    private bool jumpConsumed;
    private bool pointerCaptured;
    private bool discardLook;
    private bool initialized;

    public float VerticalSpeed => verticalSpeed;
    public float HorizontalSpeed => new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
    public float Yaw => yaw;
    public float Pitch => pitch;
    public bool PointerCaptured => pointerCaptured;
    public bool Grounded => controller.isGrounded;
    public StencilViewMode Mode => mode;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!settings || !settings.IsValid || !view || !visual)
        {
            Debug.LogError("Контроллер " + name + ": проверь settings, view и visual", this);
            enabled = false;
            return;
        }
        spawn = transform.position;
        spawnFacing = visual.rotation;
        yaw = initialYaw;
        pitch = mode == StencilViewMode.ThirdPerson ? settings.ThirdPersonPitch : 0;
        cameraTarget = transform.position + Vector3.up * settings.CameraFocusHeight;
        initialized = true;
    }

    private void Start()
    {
        UpdateView(0, true);
        if (Application.isFocused) CapturePointer();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (pointerCaptured && Cursor.lockState != CursorLockMode.Locked) ReleasePointer();
        if (!Application.isFocused)
        {
            ReleasePointer();
        }
        else if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ReleasePointer();
        }
        else if (!pointerCaptured && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            CapturePointer();
        }

        var input = new StencilPlayerInput();
        if (pointerCaptured && Application.isFocused)
        {
            if (keyboard != null)
            {
                input.Move.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.Move.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                input.Sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                input.JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
                if (keyboard.rKey.wasPressedThisFrame) Respawn();
            }
            if (mouse != null && !discardLook) input.Look = mouse.delta.ReadValue();
        }
        discardLook = false;
        Step(input, Time.deltaTime);
    }

    private void LateUpdate()
    {
        UpdateView(Time.deltaTime);
    }

    public void Step(StencilPlayerInput input, float deltaTime)
    {
        if (!initialized || deltaTime <= 0) return;
        clock += deltaTime;
        yaw = Mathf.Repeat(yaw + input.Look.x * settings.MouseSensitivity, 360);
        float minPitch = mode == StencilViewMode.ThirdPerson ? settings.OrbitMinPitch : -settings.FirstPersonMaxPitch;
        float maxPitch = mode == StencilViewMode.ThirdPerson ? settings.OrbitMaxPitch : settings.FirstPersonMaxPitch;
        pitch = Mathf.Clamp(pitch - input.Look.y * settings.MouseSensitivity, minPitch, maxPitch);

        bool grounded = controller.isGrounded && verticalSpeed <= 0;
        if (grounded)
        {
            lastGrounded = clock;
            jumpConsumed = false;
            verticalSpeed = -settings.GroundSnapSpeed;
        }
        if (input.JumpPressed) jumpUntil = clock + settings.JumpBuffer;
        if (!jumpConsumed && clock <= jumpUntil && clock - lastGrounded <= settings.CoyoteTime)
        {
            verticalSpeed = Mathf.Sqrt(-2 * settings.Gravity * settings.JumpHeight);
            jumpConsumed = true;
            jumpUntil = float.NegativeInfinity;
            lastGrounded = float.NegativeInfinity;
        }

        var move = Vector2.ClampMagnitude(input.Move, 1);
        var direction = Quaternion.Euler(0, yaw, 0) * new Vector3(move.x, 0, move.y);
        float speed = input.Sprint ? settings.SprintSpeed : settings.WalkSpeed;
        var targetVelocity = direction * speed;
        float change = move.sqrMagnitude > 0 ? settings.Acceleration : settings.Braking;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, change * deltaTime);
        verticalSpeed += settings.Gravity * deltaTime;
        var collisions = controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
        if (direction.sqrMagnitude > 0.001f)
            visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(direction), settings.TurnSpeed * deltaTime);
        if (transform.position.y < settings.FallResetHeight) Respawn();
    }

    public void UpdateView(float deltaTime, bool snap = false)
    {
        if (!initialized) return;
        var rotation = Quaternion.Euler(pitch, yaw, 0);
        if (mode == StencilViewMode.FirstPerson)
        {
            view.transform.SetPositionAndRotation(transform.position + Vector3.up * settings.EyeHeight, rotation);
            return;
        }
        var desiredTarget = transform.position + Vector3.up * settings.CameraFocusHeight;
        cameraTarget = snap ? desiredTarget : Vector3.Lerp(cameraTarget, desiredTarget, 1 - Mathf.Exp(-settings.FollowSharpness * deltaTime));
        view.transform.SetPositionAndRotation(cameraTarget + rotation * Vector3.back * settings.CameraDistance, rotation);
    }

    public void Respawn()
    {
        if (!initialized) return;
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = spawn;
        controller.enabled = wasEnabled;
        visual.rotation = spawnFacing;
        horizontalVelocity = Vector3.zero;
        verticalSpeed = 0;
        jumpUntil = float.NegativeInfinity;
        lastGrounded = float.NegativeInfinity;
        jumpConsumed = false;
        yaw = initialYaw;
        pitch = mode == StencilViewMode.ThirdPerson ? settings.ThirdPersonPitch : 0;
        UpdateView(0, true);
    }

    public void CapturePointer()
    {
        if (!initialized || !Application.isFocused || !isActiveAndEnabled) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        pointerCaptured = true;
        discardLook = true;
    }

    public void ReleasePointer()
    {
        pointerCaptured = false;
        horizontalVelocity = Vector3.zero;
        jumpUntil = float.NegativeInfinity;
        discardLook = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ReleasePointer();
    }

    private void OnDisable()
    {
        ReleasePointer();
    }
}
