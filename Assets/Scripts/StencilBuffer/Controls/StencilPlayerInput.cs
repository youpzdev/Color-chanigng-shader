using UnityEngine;

public enum StencilViewMode
{
    ThirdPerson,
    FirstPerson
}

public struct StencilPlayerInput
{
    public Vector2 Move;
    public Vector2 Look;
    public bool Sprint;
    public bool JumpPressed;
}
