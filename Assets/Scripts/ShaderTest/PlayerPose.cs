using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerPose : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private Transform leftBoot;
    [SerializeField] private Transform rightBoot;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    private CharacterController controller;
    private float phase;
    private float stride;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        var velocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
        float amount = controller.isGrounded ? Mathf.Clamp01(velocity.magnitude / 5) : 0;
        stride = Mathf.MoveTowards(stride, amount, Time.deltaTime * 8);
        phase += velocity.magnitude * Time.deltaTime * 3;
        float swing = Mathf.Sin(phase) * 28 * stride;
        leftBoot.localRotation = Quaternion.Euler(swing, 0, 0);
        rightBoot.localRotation = Quaternion.Euler(-swing, 0, 0);
        leftArm.localRotation = Quaternion.Euler(-swing * 0.7f, 0, 0);
        rightArm.localRotation = Quaternion.Euler(swing * 0.7f, 0, 0);
        visual.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * 0.035f * stride);
    }
}
