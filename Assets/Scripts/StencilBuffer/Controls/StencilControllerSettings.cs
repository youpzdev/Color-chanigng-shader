using UnityEngine;

[CreateAssetMenu(menuName = "Stencil Buffer/Controller Settings")]
public class StencilControllerSettings : ScriptableObject
{
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 7;
    [SerializeField] private float acceleration = 24;
    [SerializeField] private float braking = 30;
    [SerializeField] private float turnSpeed = 720;
    [SerializeField] private float jumpHeight = 1.35f;
    [SerializeField] private float gravity = -24;
    [SerializeField] private float groundSnapSpeed = 2;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBuffer = 0.12f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float thirdPersonPitch = 40;
    [SerializeField] private float orbitMinPitch = 15;
    [SerializeField] private float orbitMaxPitch = 75;
    [SerializeField] private float firstPersonMaxPitch = 80;
    [SerializeField] private float cameraDistance = 9;
    [SerializeField] private float cameraFocusHeight = 1;
    [SerializeField] private float eyeHeight = 1.65f;
    [SerializeField] private float followSharpness = 12;
    [SerializeField] private float fallResetHeight = -15;

    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public float Acceleration => acceleration;
    public float Braking => braking;
    public float TurnSpeed => turnSpeed;
    public float JumpHeight => jumpHeight;
    public float Gravity => gravity;
    public float GroundSnapSpeed => groundSnapSpeed;
    public float CoyoteTime => coyoteTime;
    public float JumpBuffer => jumpBuffer;
    public float MouseSensitivity => mouseSensitivity;
    public float ThirdPersonPitch => thirdPersonPitch;
    public float OrbitMinPitch => orbitMinPitch;
    public float OrbitMaxPitch => orbitMaxPitch;
    public float FirstPersonMaxPitch => firstPersonMaxPitch;
    public float CameraDistance => cameraDistance;
    public float CameraFocusHeight => cameraFocusHeight;
    public float EyeHeight => eyeHeight;
    public float FollowSharpness => followSharpness;
    public float FallResetHeight => fallResetHeight;

    public bool IsValid => walkSpeed > 0 && sprintSpeed >= walkSpeed && acceleration > 0 && braking > 0
        && turnSpeed > 0 && jumpHeight > 0 && gravity < 0 && groundSnapSpeed > 0
        && coyoteTime >= 0 && jumpBuffer >= 0 && mouseSensitivity > 0 && cameraDistance > 0
        && eyeHeight > 0 && followSharpness > 0 && orbitMinPitch < orbitMaxPitch
        && firstPersonMaxPitch > 0 && firstPersonMaxPitch < 90;
}
