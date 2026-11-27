using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class StencilControllerVerification
{
    public static string Run()
    {
        if (!Application.isPlaying) throw new Exception("Controller verification requires Play Mode");
        if (!Application.isFocused) throw new Exception("Focus Game View before pointer verification");
        var root = new GameObject("Controller verification");
        var settings = ScriptableObject.CreateInstance<StencilControllerSettings>();
        var previousLock = Cursor.lockState;
        bool previousVisibility = Cursor.visible;
        try
        {
            root.hideFlags = HideFlags.HideAndDontSave;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(400, -0.5f, 400);
            floor.transform.localScale = new Vector3(40, 1, 40);
            var player = new GameObject("Test player");
            player.SetActive(false);
            player.transform.SetParent(root.transform);
            player.transform.position = new Vector3(400, 0.05f, 400);
            var collision = player.AddComponent<CharacterController>();
            collision.height = 1.8f;
            collision.radius = 0.34f;
            collision.center = Vector3.up * 0.9f;
            collision.minMoveDistance = 0;
            collision.skinWidth = 0.04f;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            var view = new GameObject("Test camera").AddComponent<Camera>();
            view.transform.SetParent(root.transform, false);
            view.enabled = false;
            var controller = player.AddComponent<StencilPlayerController>();
            Assign(controller, "settings", settings);
            Assign(controller, "view", view);
            Assign(controller, "visual", visual);
            player.SetActive(true);
            controller.enabled = false;
            Physics.SyncTransforms();
            const float step = 1f / 120;
            Settle(controller, step);
            var start = player.transform.position;
            controller.Step(new StencilPlayerInput { Move = Vector2.right }, step);
            float firstDistance = Horizontal(player.transform.position - start);
            Require(firstDistance > 0 && firstDistance < settings.WalkSpeed * step * 0.5f, "Movement has no acceleration: " + firstDistance);
            for (int i = 1; i < 120; i++) controller.Step(new StencilPlayerInput { Move = Vector2.right }, step);
            float straight = Horizontal(player.transform.position - start);
            Reset(controller, step);
            start = player.transform.position;
            for (int i = 0; i < 120; i++) controller.Step(new StencilPlayerInput { Move = Vector2.one }, step);
            float diagonal = Horizontal(player.transform.position - start);
            Require(Mathf.Abs(straight - diagonal) < 0.015f, "Diagonal speed differs: " + diagonal + ", straight " + straight);
            Reset(controller, step);
            start = player.transform.position;
            for (int i = 0; i < 120; i++) controller.Step(new StencilPlayerInput { Move = Vector2.right, Sprint = true }, step);
            float sprint = Horizontal(player.transform.position - start);
            Require(sprint > straight * 1.25f, "Sprint is not faster: " + sprint + ", walk " + straight);
            for (int i = 0; i < 120; i++) controller.Step(default, step);
            Require(controller.HorizontalSpeed < 0.001f, "Player keeps sliding after braking: " + controller.HorizontalSpeed);

            Reset(controller, step);
            float ground = player.transform.position.y;
            controller.Step(new StencilPlayerInput { JumpPressed = true }, step);
            float peak = ground;
            for (int i = 0; i < 60; i++)
            {
                controller.Step(new StencilPlayerInput { JumpPressed = true }, step);
                peak = Mathf.Max(peak, player.transform.position.y);
            }
            Require(peak - ground > settings.JumpHeight - 0.08f && peak - ground < settings.JumpHeight + 0.03f,
                "Jump height or repeated-air-jump guard failed: " + (peak - ground));
            Settle(controller, step);
            Require(Mathf.Abs(player.transform.position.y - ground) < 0.04f, "Player did not land");

            Reset(controller, step);
            controller.Step(new StencilPlayerInput { JumpPressed = true }, step);
            bool buffered = false;
            bool requested = false;
            for (int i = 0; i < 150; i++)
            {
                bool request = !requested && controller.VerticalSpeed < 0 && player.transform.position.y < ground + 0.13f;
                if (request) requested = true;
                controller.Step(new StencilPlayerInput { JumpPressed = request }, step);
                if (requested && controller.VerticalSpeed > 0) { buffered = true; break; }
            }
            Require(buffered, "Jump pressed just before landing was lost");

            Reset(controller, step);
            Teleport(collision, new Vector3(419.7f, ground, 400));
            Settle(controller, step);
            int walkFrames = 0;
            while (controller.Grounded && walkFrames++ < 120)
                controller.Step(new StencilPlayerInput { Move = Vector2.right }, step);
            Require(!controller.Grounded, "Coyote fixture did not leave the ledge");
            controller.Step(new StencilPlayerInput { JumpPressed = true }, step);
            Require(controller.VerticalSpeed > 0, "Jump immediately after leaving ledge failed");

            Reset(controller, step);
            Teleport(collision, new Vector3(419.7f, ground, 400));
            Settle(controller, step);
            walkFrames = 0;
            while (controller.Grounded && walkFrames++ < 120)
                controller.Step(new StencilPlayerInput { Move = Vector2.right }, step);
            for (int i = 0; i < 30; i++) controller.Step(default, step);
            controller.Step(new StencilPlayerInput { JumpPressed = true }, step);
            Require(controller.VerticalSpeed < 0, "Coyote window never expires");

            Reset(controller, step);
            controller.Step(new StencilPlayerInput { Look = new Vector2(99999, -99999) }, step);
            Require(Mathf.Abs(controller.Pitch - settings.OrbitMaxPitch) < 0.001f, "Orbit upper pitch is not clamped");
            Require(controller.Yaw >= 0 && controller.Yaw < 360, "Yaw grows without wrapping");
            controller.Step(new StencilPlayerInput { Look = new Vector2(0, 99999) }, step);
            Require(Mathf.Abs(controller.Pitch - settings.OrbitMinPitch) < 0.001f, "Orbit lower pitch is not clamped");
            controller.UpdateView(0, true);
            float orbitDistance = Vector3.Distance(view.transform.position, player.transform.position + Vector3.up * settings.CameraFocusHeight);
            Require(Mathf.Abs(orbitDistance - settings.CameraDistance) < 0.002f, "Third-person camera distance differs: " + orbitDistance);
            Set(controller, "mode", StencilViewMode.FirstPerson);
            controller.Respawn();
            controller.UpdateView(0, true);
            Require(Vector3.Distance(view.transform.position, player.transform.position + Vector3.up * settings.EyeHeight) < 0.001f, "First-person eye position differs");
            controller.Step(new StencilPlayerInput { Look = new Vector2(0, 99999) }, step);
            Require(Mathf.Abs(controller.Pitch + settings.FirstPersonMaxPitch) < 0.001f, "First-person pitch is not clamped");
            controller.enabled = true;
            controller.CapturePointer();
            Require(controller.PointerCaptured && !Cursor.visible, "Mouse did not capture");
            typeof(StencilPlayerController).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { false });
            Require(!controller.PointerCaptured && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Focus loss did not release pointer");
            controller.CapturePointer();
            controller.enabled = false;
            Require(!controller.PointerCaptured && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Disabled controller retained pointer");

            return "PASS: acceleration, diagonal=" + diagonal.ToString("F2") + ", walk=" + straight.ToString("F2") + ", sprint=" + sprint.ToString("F2")
                + "; braking, jump=" + (peak - ground).ToString("F2") + ", air-jump guard, landing buffer, coyote/expiry, camera modes/pitch/yaw, focus/disable pointer";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(settings);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisibility;
        }
    }

    private static void Reset(StencilPlayerController controller, float step)
    {
        controller.Respawn();
        Physics.SyncTransforms();
        Settle(controller, step);
    }

    private static void Settle(StencilPlayerController controller, float step)
    {
        for (int i = 0; i < 180; i++) controller.Step(default, step);
    }

    private static void Teleport(CharacterController controller, Vector3 position)
    {
        controller.enabled = false;
        controller.transform.position = position;
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    private static float Horizontal(Vector3 delta) { return new Vector2(delta.x, delta.z).magnitude; }
    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}
