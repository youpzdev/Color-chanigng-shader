using UnityEngine;

[CreateAssetMenu(menuName = "Shader Test/Player Settings")]
public class PlayerSettings : ScriptableObject
{
    [SerializeField] private float speed = 5;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20;
    [SerializeField] private float turnSpeed = 720;

    public float Speed => speed;
    public float JumpHeight => jumpHeight;
    public float Gravity => gravity;
    public float TurnSpeed => turnSpeed;
}
