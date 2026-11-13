using UnityEngine;

namespace InfiniteRunner
{
    /// <summary>
    /// Receives compact pose frames from the custom Unity Web page. The object is
    /// persistent so restarting the game does not interrupt the browser camera or
    /// require the player to calibrate again.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class MediaPipeBridge : MonoBehaviour
    {
        [System.Serializable]
        private sealed class PoseSensitivitySettings
        {
            public float run = 0.65f;
            public float jump = 0.65f;
            public float crouch = 0.65f;
        }

        [System.Serializable]
        private sealed class PoseControlFeedback
        {
            public bool tracking;
            public bool calibrated;
            public float progress;
            public string instruction;
            public string action;
            public int lane;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void RunnerPoseFeedback(string json);
#endif
        private readonly PoseControlFeedback feedback = new PoseControlFeedback();
        private float nextFeedbackAt;
        private string lastFeedback;

        public const string BridgeObjectName = "MediaPipeBridge";

        private const float MinimumConfidence = 0.48f;
        private const float TrackingStaleSeconds = 0.55f;
        private const float InvalidFrameGraceSeconds = 0.35f;

        public static MediaPipeBridge Instance { get; private set; }

        private readonly PoseGestureRecognizer recognizer = new PoseGestureRecognizer();
        private int lastSequence = -1;
        private float lastPoseReceivedAt = -100f;
        private bool isTracking;
        private string browserStatus;
        private bool browserRunHeld;
        private bool browserCrouchHeld;
        private int browserLaneChange;
        private bool browserJumpQueued;
        private bool browserRestartQueued;
        private ControlMode controlMode;

        private enum ControlMode
        {
            Menu,
            Keyboard,
            Camera
        }

        public bool IsTracking => isTracking && IsPoseFresh;
        public bool IsCalibrated => recognizer.IsCalibrated;
        public float CalibrationProgress01 => recognizer.CalibrationProgress01;
        public bool RunHeld => IsPoseFresh && recognizer.IsRunHeld(Time.realtimeSinceStartup);
        public bool CrouchHeld => IsPoseFresh && recognizer.CrouchHeld;
        public bool BrowserRunHeld => browserRunHeld;
        public bool BrowserCrouchHeld => browserCrouchHeld;
        public int PoseTargetLane => IsTracking ? recognizer.TargetLane : -1;
        public bool AcceptsKeyboardInput => !IsWebBuild || controlMode == ControlMode.Keyboard;
        public bool AcceptsPoseInput => IsWebBuild && controlMode == ControlMode.Camera;
        public string LastGesture => recognizer.LastGesture;

        public string StatusText
        {
            get
            {
                if (!IsWebBuild)
                {
                    return "BODY INPUT  Build for Web to use the camera";
                }

                if (controlMode == ControlMode.Menu)
                {
                    return "CHOOSE CONTROLS  Keyboard or camera";
                }

                if (controlMode == ControlMode.Keyboard)
                {
                    return "KEYBOARD MODE  Hold W to run";
                }

                if (!string.IsNullOrEmpty(browserStatus) && !IsTracking)
                {
                    return browserStatus;
                }

                if (!IsTracking)
                {
                    return "BODY INPUT  Enable the camera beside the game";
                }

                if (!IsCalibrated)
                {
                    if (!recognizer.IsCenteredForCalibration)
                    {
                        return "CALIBRATING  Move your body into the CENTER section";
                    }

                    return $"CALIBRATING  {LastGesture}  {CalibrationProgress01 * 100f:0}%";
                }

                return $"BODY READY  {recognizer.CurrentAction(Time.realtimeSinceStartup)}";
            }
        }

        private bool IsPoseFresh => Time.realtimeSinceStartup - lastPoseReceivedAt <= TrackingStaleSeconds;

        private static bool IsWebBuild
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameObject.name = BridgeObjectName;
            DontDestroyOnLoad(gameObject);
            browserStatus = IsWebBuild
                ? "CHOOSE CONTROLS  Keyboard or camera"
                : "BODY INPUT  Build for Web to use the camera";
            controlMode = IsWebBuild ? ControlMode.Menu : ControlMode.Keyboard;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            recognizer.Tick(now);

            if (isTracking && !IsPoseFresh)
            {
                LoseTracking("BODY INPUT  Pose lost — step back so your torso is visible");
            }

            if (RunnerInput.RecalibratePressed())
            {
                RequestCalibration(string.Empty);
            }
            PublishFeedback(now);
        }

        private void PublishFeedback(float now)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (now < nextFeedbackAt) return;
            nextFeedbackAt = now + 0.1f;
            feedback.tracking = IsTracking && AcceptsPoseInput;
            feedback.calibrated = IsCalibrated;
            feedback.progress = Mathf.Round(CalibrationProgress01 * 100f) / 100f;
            feedback.instruction = !IsTracking ? "Show at least your shoulders and hips" : LastGesture;
            feedback.action = recognizer.CurrentAction(now);
            feedback.lane = PoseTargetLane;
            string json = JsonUtility.ToJson(feedback);
            if (json == lastFeedback) return;
            lastFeedback = json;
            RunnerPoseFeedback(json);
#endif
        }

        /// <summary>Called by unityInstance.SendMessage from pose-bridge.js.</summary>
        public void ReceivePoseFrame(string json)
        {
            if (!AcceptsPoseInput || string.IsNullOrEmpty(json))
            {
                return;
            }

            PoseFrameData frame;
            try
            {
                frame = JsonUtility.FromJson<PoseFrameData>(json);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Ignoring malformed MediaPipe pose frame: {exception.Message}");
                return;
            }

            if (frame == null || frame.sequence <= lastSequence)
            {
                return;
            }

            lastSequence = frame.sequence;
            float now = Time.realtimeSinceStartup;

            if (!frame.tracking || frame.confidence < MinimumConfidence || !frame.HasFiniteCoordinates())
            {
                // Sideways movement can briefly occlude one ankle or knee. Keep
                // the last good pose through a short dropout rather than stopping
                // the runner because of one weak MediaPipe frame.
                if (isTracking && now - lastPoseReceivedAt <= InvalidFrameGraceSeconds)
                {
                    return;
                }

                LoseTracking("BODY INPUT  Pose lost — step back so your torso is visible");
                return;
            }

            lastPoseReceivedAt = now;
            isTracking = true;
            browserStatus = string.Empty;
            recognizer.Process(BodyPoseSnapshot.FromFrame(frame), now);
        }

        /// <summary>Called by the Web page for camera/model lifecycle messages.</summary>
        public void ReceivePoseStatus(string status)
        {
            string normalizedStatus = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
            browserStatus = normalizedStatus;
            if (normalizedStatus == "BODY INPUT  Camera stopped" ||
                normalizedStatus.StartsWith("BODY ERROR"))
            {
                LoseTracking(normalizedStatus);
            }
        }

        /// <summary>Called by the page's Recalibrate button or the keyboard C key.</summary>
        public void RequestCalibration(string unused)
        {
            recognizer.ResetCalibration();
            isTracking = false;
            lastPoseReceivedAt = -100f;
            browserStatus = "CALIBRATING  Move your body into the CENTER section";

            GameManager.Instance?.Player?.ReturnToCenterLane();
        }

        /// <summary>Called by the Web page whenever a sensitivity slider changes.</summary>
        public void SetPoseSensitivity(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                PoseSensitivitySettings settings = JsonUtility.FromJson<PoseSensitivitySettings>(json);
                if (settings != null)
                {
                    recognizer.SetSensitivities(settings.run, settings.jump, settings.crouch);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Ignoring malformed pose sensitivity settings: {exception.Message}");
            }
        }

        public bool ConsumeJump()
        {
            // A body jump arriving just before the avatar lands gets a short grace window.
            // The recognizer expires it, so it cannot fire later after tracking loss or a pause.
            return IsTracking && (GameManager.Instance?.Player == null || GameManager.Instance.Player.IsGrounded) &&
                   recognizer.ConsumeJump(Time.realtimeSinceStartup);
        }

        /// <summary>
        /// Browser key events are forwarded explicitly because native Web keyboard
        /// focus differs between browsers and when the camera controls are clicked.
        /// </summary>
        public void ReceiveBrowserKeyDown(string code)
        {
            if (!AcceptsKeyboardInput)
            {
                return;
            }

            switch (code)
            {
                case "KeyW":
                    browserRunHeld = true;
                    break;
                case "KeyS":
                case "ArrowDown":
                    browserCrouchHeld = true;
                    break;
                case "KeyA":
                case "ArrowLeft":
                    browserLaneChange = -1;
                    break;
                case "KeyD":
                case "ArrowRight":
                    browserLaneChange = 1;
                    break;
                case "Space":
                case "ArrowUp":
                    browserJumpQueued = true;
                    break;
                case "KeyC":
                    RequestCalibration(string.Empty);
                    break;
                case "KeyR":
                case "Enter":
                case "NumpadEnter":
                    browserRestartQueued = true;
                    break;
            }
        }

        public void ReceiveBrowserKeyUp(string code)
        {
            switch (code)
            {
                case "KeyW":
                    browserRunHeld = false;
                    break;
                case "KeyS":
                case "ArrowDown":
                    browserCrouchHeld = false;
                    break;
            }
        }

        /// <summary>Called by the Web menu when the player chooses an input mode.</summary>
        public void SetControlMode(string mode)
        {
            ResetBrowserKeyboard(string.Empty);
            recognizer.ClearBufferedActions(Time.realtimeSinceStartup);

            switch (mode)
            {
                case "camera":
                    controlMode = ControlMode.Camera;
                    browserStatus = "CAMERA MODE  Waiting for camera";
                    break;
                case "keyboard":
                    controlMode = ControlMode.Keyboard;
                    LoseTracking("KEYBOARD MODE  Hold W to run");
                    break;
                default:
                    controlMode = ControlMode.Menu;
                    LoseTracking("CHOOSE CONTROLS  Keyboard or camera");
                    break;
            }
        }

        public void ResetBrowserKeyboard(string unused)
        {
            browserRunHeld = false;
            browserCrouchHeld = false;
            browserLaneChange = 0;
            browserJumpQueued = false;
            browserRestartQueued = false;
        }

        public int ConsumeBrowserLaneChange()
        {
            int laneChange = browserLaneChange;
            browserLaneChange = 0;
            return laneChange;
        }

        public bool ConsumeBrowserJump()
        {
            bool jump = browserJumpQueued;
            browserJumpQueued = false;
            return jump;
        }

        public bool ConsumeBrowserRestart()
        {
            bool restart = browserRestartQueued;
            browserRestartQueued = false;
            return restart;
        }

        public void PrepareForRun()
        {
            recognizer.ClearBufferedActions(Time.realtimeSinceStartup);
            browserLaneChange = 0;
            browserJumpQueued = false;
            browserRestartQueued = false;
        }

        private void LoseTracking(string status)
        {
            isTracking = false;
            browserStatus = status;
            recognizer.LoseTracking();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetBrowserKeyboard(string.Empty);
                LoseTracking("BODY INPUT  Game is not focused");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
