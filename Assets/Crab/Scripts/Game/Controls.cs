// The controls, built in code (keyboard and mouse, and a gamepad):
//   WASD / left stick — move, mouse / right stick — look, Space / A — jump, Shift / L3 — run,
//   LMB / RT — strike, throw or shoot, RMB / LT — aim, E / X — draw matter in, eat, use,
//   Tab / Y — create (matter into things), wheel, 1-9 / bumpers — weapon, Q / B — shield, F / R3 — portal,
//   G / H / J (d-pad) — orders to the squad: attack there, to me, hold there,
//   T — the truth lens, B / d-pad left — a bridge of matter, R / Select — the storm of matter,
//   Esc / Start — pause.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Crab.Game
{
    public static class Controls
    {
        static InputActionMap map;
        public static InputAction Move, Look, Jump, Sprint, Attack, Aim, Interact, Create, Next, Prev, Scroll, Pause, Shield, Portal, OrderAttack, OrderFollow, OrderHold, Truth, Bridge, Storm;
        public static readonly InputAction[] Slot = new InputAction[9];
        /// <summary>Mouse look speed (settings).</summary>
        public static float sensitivity = 1;
        public static bool invertY;

        public static void Ensure()
        {
            if (map != null) return;
            map = new InputActionMap("Crab");
            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");
            Look = map.AddAction("Look", InputActionType.Value);
            Look.AddBinding("<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=12,y=12)");
            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Attack = Button("Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Aim = Button("Aim", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Create = Button("Create", "<Keyboard>/tab", "<Gamepad>/buttonNorth");
            Next = Button("Next", "<Keyboard>/x", "<Gamepad>/rightShoulder");
            Prev = Button("Prev", "<Keyboard>/z", "<Gamepad>/leftShoulder");
            Scroll = map.AddAction("Scroll", InputActionType.Value);
            Scroll.AddBinding("<Mouse>/scroll/y");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            Shield = Button("Shield", "<Keyboard>/q", "<Gamepad>/buttonEast");
            Portal = Button("Portal", "<Keyboard>/f", "<Gamepad>/rightStickPress");
            OrderAttack = Button("OrderAttack", "<Keyboard>/g", "<Gamepad>/dpad/up");
            OrderFollow = Button("OrderFollow", "<Keyboard>/h", "<Gamepad>/dpad/down");
            OrderHold = Button("OrderHold", "<Keyboard>/j", "<Gamepad>/dpad/right");
            Truth = Button("Truth", "<Keyboard>/t", null);
            Bridge = Button("Bridge", "<Keyboard>/b", "<Gamepad>/dpad/left");
            Storm = Button("Storm", "<Keyboard>/r", "<Gamepad>/select");
            for (int i = 0; i < Slot.Length; i++) Slot[i] = Button($"Slot{i + 1}", $"<Keyboard>/{i + 1}", null);
            map.Enable();
        }

        static InputAction Button(string name, string key, string pad)
        {
            var a = map.AddAction(name, InputActionType.Button);
            a.AddBinding(key);
            if (pad != null) a.AddBinding(pad);
            return a;
        }

        /// <summary>Look delta in degrees for this frame (the mouse is already per frame).</summary>
        public static Vector2 LookDelta
        {
            get
            {
                Ensure();
                var v = Look.ReadValue<Vector2>();
                bool pad = Look.activeControl != null && Look.activeControl.device is Gamepad;
                if (invertY) v.y = -v.y;
                return (pad ? v * Time.unscaledDeltaTime * 10 : v * 0.12f) * sensitivity;
            }
        }
    }
}
