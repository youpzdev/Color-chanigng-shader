using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class StencilMovementVerification
{
    public static async Task<string> Run()
    {
        if (!Application.isPlaying) throw new Exception("Movement verification requires Play Mode");
        if (!Application.isFocused) throw new Exception("Focus Game View before keyboard verification");
        var stencilController = UnityEngine.Object.FindFirstObjectByType<StencilPlayerController>();
        MonoBehaviour player = stencilController;
        if (!player) player = UnityEngine.Object.FindFirstObjectByType<TopDownPlayer>();
        if (!player) throw new Exception("No player controller in active scene");
        bool pointerWasCaptured = stencilController && stencilController.PointerCaptured;
        if (stencilController) stencilController.CapturePointer();
        var visual = (Transform)player.GetType().GetField("visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
        var facing = visual.rotation;
        var originalKeyboard = Keyboard.current;
        var originalMouse = Mouse.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            await Task.Delay(200);
            int firstFrame = Time.frameCount;
            var start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            await Task.Delay(350);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(50);
            float distance = Vector3.Distance(start, player.transform.position);
            if (Time.frameCount <= firstFrame) throw new Exception("Frames did not advance");
            if (distance < 0.3f) throw new Exception("D key did not move player: " + distance);
            float ground = player.transform.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            await Task.Delay(25);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            float peak = ground;
            for (int i = 0; i < 45; i++)
            {
                await Task.Delay(20);
                peak = Mathf.Max(peak, player.transform.position.y);
            }
            if (peak - ground < 0.8f) throw new Exception("Space did not produce a jump: " + (peak - ground));
            await Task.Delay(250);
            if (Mathf.Abs(player.transform.position.y - ground) > 0.15f)
                throw new Exception("Player did not land: " + player.transform.position.y + ", expected " + ground);
            if (stencilController)
            {
                float yaw = stencilController.Yaw;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(80, -20) });
                await Task.Delay(60);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, stencilController.Yaw)) < 0.5f)
                    throw new Exception("Mouse delta did not rotate the camera");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                await Task.Delay(60);
                if (stencilController.PointerCaptured || !Cursor.visible) throw new Exception("Escape did not release cursor");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                await Task.Delay(60);
                if (!stencilController.PointerCaptured || Cursor.visible) throw new Exception("Left click did not recapture cursor");
            }

            return "PASS: frames advancing; keyboard D moved " + distance.ToString("F2") + "m; Space jump " + (peak - ground).ToString("F2") + "m; landed; mouse look and Escape/click capture where supported";
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            if (originalMouse != null) originalMouse.MakeCurrent();
            player.GetType().GetMethod("Respawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(player, null);
            visual.rotation = facing;
            if (stencilController && !pointerWasCaptured) stencilController.ReleasePointer();
        }
    }
}
