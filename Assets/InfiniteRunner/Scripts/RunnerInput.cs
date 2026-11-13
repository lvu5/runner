using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace InfiniteRunner
{
    internal readonly struct RunnerInputFrame
    {
        public RunnerInputFrame(bool runHeld, int laneChange, int targetLane, bool jumpPressed, bool crouchHeld)
        {
            RunHeld = runHeld;
            LaneChange = laneChange;
            TargetLane = targetLane;
            JumpPressed = jumpPressed;
            CrouchHeld = crouchHeld;
        }

        public bool RunHeld { get; }
        public int LaneChange { get; }
        public int TargetLane { get; }
        public bool JumpPressed { get; }
        public bool CrouchHeld { get; }
    }

    /// <summary>
    /// Combines keyboard and body tracking into one gameplay input frame. The
    /// keyboard remains available as a fallback in Web builds and while tuning
    /// pose recognition in the Editor.
    /// </summary>
    internal static class RunnerInput
    {
        public static RunnerInputFrame ReadFrame()
        {
            MediaPipeBridge bridge = MediaPipeBridge.Instance;
            bool acceptsKeyboard = bridge == null || bridge.AcceptsKeyboardInput;

            int laneChange = acceptsKeyboard ? ReadKeyboardLaneChange() : 0;
            int targetLane = -1;
            bool jumpPressed = acceptsKeyboard && ReadKeyboardJumpPressed();
            bool runHeld = acceptsKeyboard && ReadKeyboardRunHeld();
            bool crouchHeld = acceptsKeyboard && ReadKeyboardCrouchHeld();

            if (bridge != null && bridge.AcceptsKeyboardInput)
            {
                laneChange = Mathf.Clamp(
                    laneChange + bridge.ConsumeBrowserLaneChange(),
                    -1,
                    1);
                jumpPressed |= bridge.ConsumeBrowserJump();
                runHeld |= bridge.BrowserRunHeld;
                crouchHeld |= bridge.BrowserCrouchHeld;
            }

            if (bridge != null && bridge.AcceptsPoseInput)
            {
                targetLane = bridge.PoseTargetLane;
                jumpPressed |= bridge.ConsumeJump();
                runHeld |= bridge.RunHeld;
                crouchHeld |= bridge.CrouchHeld;
            }

            return new RunnerInputFrame(runHeld, laneChange, targetLane, jumpPressed, crouchHeld);
        }

        private static int ReadKeyboardLaneChange()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0;
            }

            bool left = keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame;
            bool right = keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;
#else
            bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
#endif

            if (left == right)
            {
                return 0;
            }

            return left ? -1 : 1;
        }

        private static bool ReadKeyboardRunHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.wKey.isPressed;
#else
            return Input.GetKey(KeyCode.W);
#endif
        }

        private static bool ReadKeyboardJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.spaceKey.wasPressedThisFrame ||
                    keyboard.upArrowKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) ||
                   Input.GetKeyDown(KeyCode.UpArrow);
#endif
        }

        private static bool ReadKeyboardCrouchHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed);
#else
            return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
#endif
        }

        public static bool RecalibratePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.cKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.C);
#endif
        }

        public static bool RestartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            bool keyboardRestart = keyboard != null &&
                                   (keyboard.rKey.wasPressedThisFrame ||
                                    keyboard.enterKey.wasPressedThisFrame ||
                                    keyboard.numpadEnterKey.wasPressedThisFrame);
#else
            bool keyboardRestart = Input.GetKeyDown(KeyCode.R) ||
                                   Input.GetKeyDown(KeyCode.Return) ||
                                   Input.GetKeyDown(KeyCode.KeypadEnter);
#endif

            MediaPipeBridge bridge = MediaPipeBridge.Instance;
            bool browserRestart = bridge != null && bridge.ConsumeBrowserRestart();
            return keyboardRestart || browserRestart;
        }
    }
}
