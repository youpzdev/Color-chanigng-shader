using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TopDownPlayer : MonoBehaviour
{
    [SerializeField] private PlayerSettings settings;
    [SerializeField] private Transform view;
    [SerializeField] private Transform visual;
    private CharacterController controller;
    private float verticalSpeed;
    private Vector3 spawn;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        spawn = transform.position;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var input = Vector2.zero;
        if (keyboard != null && Application.isFocused)
        {
            input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
        }

        Move(input, keyboard != null && Application.isFocused && keyboard.spaceKey.wasPressedThisFrame, Time.deltaTime);
    }

    public void Move(Vector2 input, bool jump, float deltaTime)
    {
        var forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, forward);
        var direction = right * input.x + forward * input.y;
        direction = Vector3.ClampMagnitude(direction, 1);
        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        if (controller.isGrounded && jump) verticalSpeed = Mathf.Sqrt(-2 * settings.Gravity * settings.JumpHeight);
        verticalSpeed += settings.Gravity * deltaTime;
        controller.Move((direction * settings.Speed + Vector3.up * verticalSpeed) * deltaTime);
        if (direction.sqrMagnitude > 0.001f)
            visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(direction), settings.TurnSpeed * deltaTime);
        if (transform.position.y < -15) Respawn();
    }

    private void Respawn()
    {
        controller.enabled = false;
        transform.position = spawn;
        verticalSpeed = 0;
        controller.enabled = true;
    }
}
