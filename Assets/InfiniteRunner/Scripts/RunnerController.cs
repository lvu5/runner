using UnityEngine;

namespace InfiniteRunner
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RunnerController : MonoBehaviour
    {
        [Header("Lane movement")]
        [SerializeField] private float laneWidth = 2.75f;
        [SerializeField] private float laneChangeSmoothTime = 0.12f;
        [SerializeField] private float maximumLaneChangeSpeed = 20f;

        [Header("Forward movement")]
        [SerializeField] private float forwardAcceleration = 42f;
        [SerializeField] private float forwardDeceleration = 60f;

        [Header("Jump and slide")]
        [SerializeField] private float jumpHeight = 2.15f;
        [SerializeField] private float gravityMagnitude = 26f;

        private CharacterController characterController;
        private GameManager gameManager;
        private Transform visualRoot;
        private Vector3 standingVisualScale;
        private float standingHeight;
        private Vector3 standingCenter;
        private float verticalVelocity;
        private float lateralVelocity;
        private int targetLane = 1;
        private bool isSliding;
        private readonly CameraRunContinuity cameraRunContinuity = new CameraRunContinuity();
        private readonly Collider[] standingClearanceHits = new Collider[12];

        public bool IsSliding => isSliding;
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsMovingForward { get; private set; }
        public float CurrentForwardSpeed { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            standingHeight = characterController.height;
            standingCenter = characterController.center;
        }

        public void Configure(GameManager manager, Transform runnerVisual)
        {
            gameManager = manager;
            visualRoot = runnerVisual;
            standingVisualScale = runnerVisual.localScale;
        }

        /// <summary>Returns the runner to the middle lane using normal lane movement.</summary>
        public void ReturnToCenterLane()
        {
            SetTargetLane(1);
        }

        private void Update()
        {
            if (gameManager == null || !gameManager.IsRunning)
            {
                IsMovingForward = false;
                CurrentForwardSpeed = 0f;
                cameraRunContinuity.Reset();
                return;
            }

            RunnerInputFrame input = RunnerInput.ReadFrame();
            bool grounded = characterController.isGrounded;
            bool runHeld = input.RunHeld;
            MediaPipeBridge bridge = MediaPipeBridge.Instance;
            if (bridge != null && bridge.AcceptsPoseInput)
            {
                runHeld = cameraRunContinuity.Resolve(input.RunHeld, grounded && input.JumpPressed,
                    grounded, bridge.IsTracking && bridge.IsCalibrated, Time.realtimeSinceStartup);
            }
            else cameraRunContinuity.Reset();
            float desiredForwardSpeed = runHeld ? gameManager.TargetRunSpeed : 0f;
            float forwardChangeRate = desiredForwardSpeed > CurrentForwardSpeed
                ? forwardAcceleration
                : forwardDeceleration;
            CurrentForwardSpeed = Mathf.MoveTowards(
                CurrentForwardSpeed,
                desiredForwardSpeed,
                forwardChangeRate * Time.deltaTime);

            if (!runHeld && CurrentForwardSpeed < 0.05f)
            {
                CurrentForwardSpeed = 0f;
            }

            IsMovingForward = CurrentForwardSpeed > 0f;

            if (input.TargetLane >= 0)
            {
                SetTargetLane(input.TargetLane);
            }
            else if (input.LaneChange != 0)
            {
                SetTargetLane(targetLane + input.LaneChange);
            }

            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (grounded && input.JumpPressed)
            {
                ForceEndSlide();
                verticalVelocity = Mathf.Sqrt(2f * gravityMagnitude * jumpHeight);
            }
            else if (grounded && input.CrouchHeld)
            {
                BeginSlide();
            }
            else if (isSliding && !input.CrouchHeld)
            {
                TryEndSlide();
            }

            verticalVelocity -= gravityMagnitude * Time.deltaTime;

            float targetX = (targetLane - 1) * laneWidth;
            float nextX = Mathf.SmoothDamp(
                transform.position.x,
                targetX,
                ref lateralVelocity,
                laneChangeSmoothTime,
                maximumLaneChangeSpeed,
                Time.deltaTime);

            if (Mathf.Abs(targetX - nextX) < 0.001f && Mathf.Abs(lateralVelocity) < 0.02f)
            {
                nextX = targetX;
                lateralVelocity = 0f;
            }

            float lateralDelta = nextX - transform.position.x;

            Vector3 displacement = new Vector3(
                lateralDelta,
                verticalVelocity * Time.deltaTime,
                CurrentForwardSpeed * Time.deltaTime);

            CollisionFlags collisionFlags = characterController.Move(displacement);
            bool supported = (collisionFlags & CollisionFlags.Below) != 0;
            if (grounded && verticalVelocity <= 0f)
            {
                float maximumDrop = characterController.stepOffset + CurrentForwardSpeed * Time.deltaTime;
                supported |= RunnerGrounding.TrySnapToSurface(characterController, maximumDrop);
            }
            if (supported && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
        }

        private void SetTargetLane(int lane)
        {
            int clampedLane = Mathf.Clamp(lane, 0, 2);
            if (clampedLane == targetLane)
            {
                return;
            }

            targetLane = clampedLane;
        }

        private void BeginSlide()
        {
            if (isSliding)
            {
                return;
            }

            isSliding = true;
            characterController.height = standingHeight * 0.5f;
            characterController.center = new Vector3(
                standingCenter.x,
                standingCenter.y * 0.5f,
                standingCenter.z);

            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(
                    standingVisualScale.x,
                    standingVisualScale.y * 0.5f,
                    standingVisualScale.z);
            }
        }

        private void TryEndSlide()
        {
            if (!isSliding || !HasStandingClearance())
            {
                return;
            }

            ForceEndSlide();
        }

        private void ForceEndSlide()
        {
            if (!isSliding)
            {
                return;
            }

            isSliding = false;
            characterController.height = standingHeight;
            characterController.center = standingCenter;

            if (visualRoot != null)
            {
                visualRoot.localScale = standingVisualScale;
            }
        }

        private bool HasStandingClearance()
        {
            float radius = characterController.radius * 0.9f;
            float halfSegment = Mathf.Max(0f, standingHeight * 0.5f - radius);
            Vector3 worldCenter = transform.TransformPoint(standingCenter);
            Vector3 up = transform.up;
            Vector3 bottom = worldCenter - up * halfSegment + up * 0.035f;
            Vector3 top = worldCenter + up * halfSegment - up * 0.035f;

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                standingClearanceHits,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = standingClearanceHits[i];
                standingClearanceHits[i] = null;
                if (hit == null || hit == characterController || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (gameManager == null || !gameManager.IsRunning)
            {
                return;
            }

            ObstacleMarker obstacle = hit.collider.GetComponent<ObstacleMarker>();
            if (obstacle != null)
            {
                gameManager.EndRun();
            }
        }
    }
}
