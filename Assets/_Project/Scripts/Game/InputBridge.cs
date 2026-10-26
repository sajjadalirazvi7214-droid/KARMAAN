using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Karmaan.Game
{
    /// <summary>
    /// One thin door to input, because this project can legitimately end up in three states:
    /// legacy only, new Input System only, or both. AR Foundation drags the Input System package
    /// in as a dependency, so "new only" is a real possibility the moment someone clicks Yes on
    /// Unity's backend prompt.
    ///
    /// Rather than gamble on which one is active, every input read goes through here.
    /// </summary>
    public static class InputBridge
    {
        public static Vector3 PointerPosition
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                if (Mouse.current == null) return Vector3.zero;
                var p = Mouse.current.position.ReadValue();
                return new Vector3(p.x, p.y, 0f);
#endif
            }
        }

        public static bool TapDown
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(0);
#else
                if (Mouse.current != null) return Mouse.current.leftButton.wasPressedThisFrame;
                return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
#endif
            }
        }

        public static bool TapUp
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonUp(0);
#else
                if (Mouse.current != null) return Mouse.current.leftButton.wasReleasedThisFrame;
                return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
#endif
            }
        }

        public static bool CancelDown
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Escape);
#else
                return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
            }
        }

        public static bool ToggleLangDown
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.L);
#else
                return Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame;
#endif
            }
        }

        /// <summary>Mouse/touch delta for looking around. Already scaled to degrees-per-frame.</summary>
        public static Vector2 LookDelta
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
                if (Mouse.current == null) return Vector2.zero;
                var d = Mouse.current.delta.ReadValue();
                return new Vector2(d.x * 0.05f, d.y * 0.05f);
#endif
            }
        }

        /// <summary>True when a pointer exists and looking is meaningful (i.e. not a headless test).</summary>
        public static bool HasPointer
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return true;
#else
                return Mouse.current != null || Touchscreen.current != null;
#endif
            }
        }
    }
}
