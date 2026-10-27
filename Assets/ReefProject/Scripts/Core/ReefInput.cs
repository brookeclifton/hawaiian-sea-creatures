using UnityEngine;
using UnityEngine.InputSystem;

namespace Reef
{
    /// <summary>
    /// One place that reads the Xbox controller (plus a keyboard fallback for developing
    /// on a laptop without a controller). Uses the new Input System directly, the same way
    /// the CyberCANOE scripts do, so no Input Actions asset needs to be set up.
    ///
    /// Controller                     Keyboard fallback
    /// Left stick   swim              W A S D
    /// Right stick  look              Arrow keys
    /// RT / LT      rise / sink       Space / C
    /// L3 (click)   hold to boost     Left Shift
    /// A            examine target    E or Enter
    /// B            close panel       Q or Backspace
    /// X            collect           F
    /// D-pad L/R    cycle targets     Tab / Shift+Tab
    /// View (hold)  return to start   Hold R
    /// Menu (Start) recipe menu       M
    /// Menus: D-pad / left stick to choose (or arrow keys / A D), A to confirm (E or Enter)
    /// </summary>
    public static class ReefInput
    {
        public static bool KeyboardEnabled = true;

        static Gamepad Pad => Gamepad.current;
        static Keyboard Keys => KeyboardEnabled ? Keyboard.current : null;

        public static bool HasGamepad => Gamepad.current != null;

        /// <summary>x = strafe (right +), y = forward (+).</summary>
        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (Pad != null) v += Pad.leftStick.ReadValue();
                var k = Keys;
                if (k != null)
                {
                    if (k.wKey.isPressed) v.y += 1f;
                    if (k.sKey.isPressed) v.y -= 1f;
                    if (k.dKey.isPressed) v.x += 1f;
                    if (k.aKey.isPressed) v.x -= 1f;
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>x = yaw (right +), y = pitch (up +).</summary>
        public static Vector2 Look
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (Pad != null) v += Pad.rightStick.ReadValue();
                var k = Keys;
                if (k != null)
                {
                    if (k.rightArrowKey.isPressed) v.x += 1f;
                    if (k.leftArrowKey.isPressed) v.x -= 1f;
                    if (k.upArrowKey.isPressed) v.y += 1f;
                    if (k.downArrowKey.isPressed) v.y -= 1f;
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>+1 = rise, -1 = sink.</summary>
        public static float Vertical
        {
            get
            {
                float v = 0f;
                if (Pad != null) v += Pad.rightTrigger.ReadValue() - Pad.leftTrigger.ReadValue();
                var k = Keys;
                if (k != null)
                {
                    if (k.spaceKey.isPressed) v += 1f;
                    if (k.cKey.isPressed) v -= 1f;
                }
                return Mathf.Clamp(v, -1f, 1f);
            }
        }

        public static bool Boost =>
            (Pad != null && Pad.leftStickButton.isPressed) ||
            (Keys != null && Keys.leftShiftKey.isPressed);

        public static bool ExaminePressed =>
            (Pad != null && Pad.buttonSouth.wasPressedThisFrame) ||
            (Keys != null && (Keys.eKey.wasPressedThisFrame || Keys.enterKey.wasPressedThisFrame));

        public static bool ClosePressed =>
            (Pad != null && Pad.buttonEast.wasPressedThisFrame) ||
            (Keys != null && (Keys.qKey.wasPressedThisFrame || Keys.backspaceKey.wasPressedThisFrame));

        public static bool CollectPressed =>
            (Pad != null && Pad.buttonWest.wasPressedThisFrame) ||
            (Keys != null && Keys.fKey.wasPressedThisFrame);

        public static bool CycleNextPressed =>
            (Pad != null && Pad.dpad.right.wasPressedThisFrame) ||
            (Keys != null && Keys.tabKey.wasPressedThisFrame && !Keys.leftShiftKey.isPressed);

        public static bool CyclePrevPressed =>
            (Pad != null && Pad.dpad.left.wasPressedThisFrame) ||
            (Keys != null && Keys.tabKey.wasPressedThisFrame && Keys.leftShiftKey.isPressed);

        public static bool MenuLeftPressed =>
            (Pad != null && Pad.dpad.left.wasPressedThisFrame) ||
            (Keys != null && (Keys.leftArrowKey.wasPressedThisFrame || Keys.aKey.wasPressedThisFrame));

        public static bool MenuRightPressed =>
            (Pad != null && Pad.dpad.right.wasPressedThisFrame) ||
            (Keys != null && (Keys.rightArrowKey.wasPressedThisFrame || Keys.dKey.wasPressedThisFrame));

        /// <summary>Left stick x only (for menus; the stick also swims).</summary>
        public static float MenuStickX => Pad != null ? Pad.leftStick.ReadValue().x : 0f;

        public static bool ConfirmPressed => ExaminePressed;

        public static bool BackToMenuPressed =>
            (Pad != null && Pad.startButton.wasPressedThisFrame) ||
            (Keys != null && Keys.mKey.wasPressedThisFrame);

        public static bool ResetHeld =>
            (Pad != null && Pad.selectButton.isPressed) ||
            (Keys != null && Keys.rKey.isPressed);
    }
}
