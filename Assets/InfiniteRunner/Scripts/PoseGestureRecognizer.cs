using UnityEngine;

namespace InfiniteRunner
{
    /// <summary>
    /// Converts normalized body landmarks into stable runner commands. Gesture
    /// recognition stays in C# so Web and a future native iOS pose provider use
    /// identical calibration and tuning.
    ///
    /// Design notes:
    /// - Only shoulders and hips are required. Knees, ankles and wrists add
    ///   evidence when visible, so a camera that crops the feet still works.
    /// - "Standing levels" follow slow drift (stepping closer to the camera,
    ///   swaying, camera auto-exposure jitter) and freeze during gestures, so
    ///   calibration does not go stale after a few lane changes.
    /// - Every threshold is measured in torso lengths, which removes the effect
    ///   of how far the player stands from the camera.
    /// - Running uses Schmitt triggers on the left/right difference of knees,
    ///   ankles and wrists. Either alternating legs or pumping arms count.
    /// </summary>
    internal sealed class PoseGestureRecognizer
    {
        private const float CalibrationSeconds = 1.0f;
        private const int MinimumCalibrationSamples = 10;

        private const float MinimumJumpBodyRise = 0.10f;
        private const float MaximumJumpBodyRise = 0.26f;
        private const float MinimumJumpFeetRise = 0.035f;
        private const float MaximumJumpFeetRise = 0.12f;
        // Without visible feet the torso alone must rise further to be a jump.
        private const float NoFeetJumpMultiplier = 1.3f;

        private const float PoseFilterSeconds = 0.04f;
        private const float LaneFilterSeconds = 0.05f;
        private const float LevelFollowSeconds = 1.4f;
        private const float LevelFollowDeadband = 0.06f;
        // Outside the deadband (e.g. a lagging level after walking toward the
        // camera) keep following, just slower, so the reference can never get stuck.
        private const float LevelCatchUpSeconds = 3.5f;
        private const float AsymmetryFollowSeconds = 1.6f;
        private const float TrackingGapResetSeconds = 0.6f;

        private const float JumpBufferSeconds = 0.2f;
        private const float JumpRepeatSeconds = 0.28f;
        private const float LandingConfirmationSeconds = 0.08f;
        // Jumps and crouches fire on the second consecutive camera frame that agrees
        // (the first may be at a lower level). This rejects one-frame spikes without
        // adding a fixed time delay on top of the camera rate.
        private const float PreviousFrameFraction = 0.6f;

        private const float LeftZoneBoundary = 1f / 3f;
        private const float RightZoneBoundary = 2f / 3f;
        private const float ZoneHysteresis = 0.025f;
        private const int ZoneConfirmationFrames = 2;
        private const float LaneChangeRunGrace = 0.8f;

        // Left/right height difference (torso lengths) needed to count a step.
        private const float MinimumKneeStep = 0.05f;
        private const float MaximumKneeStep = 0.14f;
        private const float MinimumAnkleStep = 0.06f;
        private const float MaximumAnkleStep = 0.16f;
        private const float MinimumArmPump = 0.14f;
        private const float MaximumArmPump = 0.38f;
        private const float StepRearmFraction = 0.3f;
        private const float StepDebounceSeconds = 0.1f;
        private const float StepCadenceMaximum = 0.95f;
        // Running continues for a multiple of the player's own step interval, so a
        // fast runner stops promptly and a slow jogger does not stutter.
        private const float FirstStepRunGrace = 0.6f;
        private const float CadenceGraceMultiplier = 1.6f;
        private const float MinimumRunGrace = 0.38f;
        private const float MaximumRunGrace = 0.8f;
        private const float JumpRunGrace = 1.05f;
        private const float CrouchReleaseRunGrace = 1.0f;

        // Real jumps and crouches keep the apparent body width; walking toward
        // or away from the camera changes it.
        private const float MinimumWidthRatio = 0.8f;
        private const float MaximumWidthRatio = 1.25f;

        private struct StandingLevels
        {
            public float ShoulderY;
            public float HipY;
            public float LeftKneeY;
            public float RightKneeY;
            public float LeftAnkleY;
            public float RightAnkleY;
            public bool HasKnees;
            public bool HasAnkles;
            public float Torso;
            public float ShoulderWidth;
        }

        private sealed class StepChannel
        {
            public float Offset;
            public int Phase;

            public void Reset()
            {
                Phase = 0;
            }

            /// <summary>Returns true when the channel enters a new raised side.</summary>
            public bool Update(float difference, float threshold, float dt, bool active)
            {
                if (!active)
                {
                    Phase = 0;
                    return false;
                }

                float centered = difference - Offset;
                // A static asymmetry (one shoulder lower, camera roll) slowly
                // becomes the new center. Real steps are far outside the deadband.
                if (Mathf.Abs(centered) < threshold * StepRearmFraction)
                {
                    Offset = Mathf.Lerp(Offset, difference, 1f - Mathf.Exp(-dt / AsymmetryFollowSeconds));
                    Phase = 0;
                    return false;
                }

                int side = centered > threshold ? 1 : centered < -threshold ? -1 : Phase;
                if (side == Phase)
                {
                    return false;
                }

                Phase = side;
                return true;
            }
        }

        private BodyPoseSnapshot smoothed;
        private BodyPoseSnapshot calibrationSum;
        private BodyPoseSnapshot calibrationAnchor;
        private StandingLevels levels;
        private float lastPoseAt = -100f;
        private float jumpCandidateAt = -1f;
        private float landingCandidateAt = -1f;
        private float crouchCandidateAt = -1f;
                private float lastJumpAt = -100f;
        private float lastLandingAt = -100f;
        private float pendingJumpUntil = -100f;
        private bool jumpArmed;
        private bool hasSmoothedPose;
        private bool calibrationStarted;
        private float calibrationStartedAt;
        private int calibrationSamples;

        private bool jumpPoseActive;
        private bool crouchHeld;
        private bool crouchMaintainsRun;
        private int stableBodyLane = -1;
        private int candidateBodyLane = -1;
        private float candidateBodyLaneStartedAt;
        private readonly StepChannel kneeSteps = new StepChannel();
        private readonly StepChannel ankleSteps = new StepChannel();
        private readonly StepChannel armSteps = new StepChannel();
        private float lastStepAt = -100f;
        private float stepInterval = 0.4f;
        private float runUntil = -100f;
        private float previousHipRise;
        private float previousShoulderRise;
        private float previousFeetRise;
        private bool previousStand;
        private int candidateLaneFrames;
        private bool pendingJump;
        private string lastGesture = "Stand neutral";
        private float runSensitivity = 0.65f;
        private float jumpSensitivity = 0.65f;
        private float crouchSensitivity = 0.65f;

        public bool IsCalibrated { get; private set; }
        public bool IsCenteredForCalibration { get; private set; }
        public float CalibrationProgress01 { get; private set; }
        public bool CrouchHeld => IsCalibrated && crouchHeld;
        public int TargetLane { get; private set; } = -1;
        public string LastGesture => lastGesture;

        public bool IsRunHeld(float now)
        {
            return IsCalibrated && now <= runUntil;
        }

        public void SetSensitivities(float run, float jump, float crouch = 0.65f)
        {
            if (!float.IsNaN(run) && !float.IsInfinity(run)) runSensitivity = Mathf.Clamp01(run);
            if (!float.IsNaN(jump) && !float.IsInfinity(jump)) jumpSensitivity = Mathf.Clamp01(jump);
            if (!float.IsNaN(crouch) && !float.IsInfinity(crouch)) crouchSensitivity = Mathf.Clamp01(crouch);
        }

        public void Process(BodyPoseSnapshot pose, float now)
        {
            if (now <= lastPoseAt) return;
            float gap = now - lastPoseAt;
            float dt = Mathf.Clamp(gap, 1f / 120, 0.2f);
            if (hasSmoothedPose && gap > TrackingGapResetSeconds) LoseTracking();
            lastPoseAt = now;

            if (!hasSmoothedPose)
            {
                smoothed = pose;
                hasSmoothedPose = true;
            }
            else
            {
                bool kneesAppeared = pose.KneesVisible && !smoothed.KneesVisible;
                bool anklesAppeared = pose.AnklesVisible && !smoothed.AnklesVisible;
                bool wristsAppeared = pose.WristsVisible && !smoothed.WristsVisible;
                float previousCenter = smoothed.BodyCenterX;
                // Short dropouts in the camera feed should not look like teleports.
                float poseFilter = gap > 0.25f ? 1f : 1f - Mathf.Exp(-dt / PoseFilterSeconds);
                smoothed = BodyPoseSnapshot.Lerp(smoothed, pose, poseFilter);
                smoothed.BodyCenterX = Mathf.Lerp(previousCenter, pose.BodyCenterX, 1f - Mathf.Exp(-dt / LaneFilterSeconds));
                // Joints that just became visible have no meaningful history.
                if (kneesAppeared) { smoothed.LeftKnee = pose.LeftKnee; smoothed.RightKnee = pose.RightKnee; }
                if (anklesAppeared) { smoothed.LeftAnkle = pose.LeftAnkle; smoothed.RightAnkle = pose.RightAnkle; }
                if (wristsAppeared) { smoothed.LeftWrist = pose.LeftWrist; smoothed.RightWrist = pose.RightWrist; }
            }

            if (!IsCalibrated)
            {
                IsCenteredForCalibration = ZoneFromPosition(pose.BodyCenterX) == 1;
                CollectCalibrationSample(pose, now);
                return;
            }

            // Use the filtered torso position for lane selection so a single
            // noisy camera sample cannot whip the runner between lanes.
            bool laneChanged = UpdateBodyZone(smoothed.BodyCenterX, now);
            RecognizeGestures(pose, now, dt, laneChanged);
        }

        public void Tick(float now)
        {
            if (now > pendingJumpUntil) pendingJump = false;
            if (crouchHeld && crouchMaintainsRun)
            {
                runUntil = Mathf.Max(runUntil, now + 0.25f);
            }
        }

        public bool ConsumeJump(float now)
        {
            bool value = pendingJump && now <= pendingJumpUntil;
            pendingJump = false;
            return value;
        }

        public void ResetCalibration()
        {
            IsCalibrated = false;
            IsCenteredForCalibration = false;
            CalibrationProgress01 = 0f;
            calibrationStarted = false;
            calibrationSamples = 0;
            calibrationSum = default;
            hasSmoothedPose = false;
            ClearTransientState();
            ResetBodyZone();
            lastGesture = "Move into center";
        }

        public void LoseTracking()
        {
            hasSmoothedPose = false;
            if (!IsCalibrated)
            {
                ResetCalibrationCollection();
                IsCenteredForCalibration = false;
            }

            ClearTransientState();
            ResetBodyZone();
        }

        public void ClearBufferedActions(float now)
        {
            ClearTransientState();
        }

        public string CurrentAction(float now) => !IsCalibrated ? lastGesture :
            jumpPoseActive || now - lastJumpAt < 0.35f ? "Jump" :
            crouchHeld ? "Crouch" : IsRunHeld(now) ? "Run" : "Ready";

        private void CollectCalibrationSample(BodyPoseSnapshot pose, float now)
        {
            float torso = TorsoLength(smoothed);
            if (torso < 0.06f || smoothed.ShoulderWidth < 0.05f)
            {
                ResetCalibrationCollection();
                IsCenteredForCalibration = false;
                lastGesture = "Step closer so your torso is visible";
                return;
            }

            if (torso > 0.55f)
            {
                ResetCalibrationCollection();
                lastGesture = "Step back from the camera";
                return;
            }

            if (!IsCenteredForCalibration)
            {
                ResetCalibrationCollection();
                hasSmoothedPose = false;
                lastGesture = "Move into center";
                return;
            }

            // Knee angles only matter when the legs are actually visible.
            bool legsStraight = !pose.AnklesVisible ||
                                (KneeAngle(pose.LeftHip, pose.LeftKnee, pose.LeftAnkle, pose.Aspect) > 150f &&
                                 KneeAngle(pose.RightHip, pose.RightKnee, pose.RightAnkle, pose.Aspect) > 150f);
            bool upright = legsStraight && pose.HipCenter.y > pose.ShoulderCenter.y + 0.045f;
            float stillTolerance = Mathf.Max(0.008f, torso * 0.1f);
            bool moved = calibrationStarted &&
                (Vector2.Distance(pose.HipCenter, calibrationAnchor.HipCenter) > stillTolerance ||
                 Vector2.Distance(pose.ShoulderCenter, calibrationAnchor.ShoulderCenter) > stillTolerance);
            if (!upright || moved)
            {
                ResetCalibrationCollection();
                lastGesture = upright ? "Keep still" : "Stand upright";
                return;
            }

            if (!calibrationStarted)
            {
                calibrationStarted = true;
                calibrationStartedAt = now;
                calibrationAnchor = pose;
                calibrationSamples = 0;
                calibrationSum = default;
            }

            calibrationSum += pose;
            lastGesture = "Keep still";
            calibrationSamples++;
            CalibrationProgress01 = Mathf.Clamp01((now - calibrationStartedAt) / CalibrationSeconds);

            if (CalibrationProgress01 < 1f || calibrationSamples < MinimumCalibrationSamples)
            {
                return;
            }

            BodyPoseSnapshot baseline = calibrationSum / calibrationSamples;
            baseline.KneesVisible = pose.KneesVisible;
            baseline.AnklesVisible = pose.AnklesVisible;
            levels = new StandingLevels
            {
                ShoulderY = baseline.ShoulderCenter.y,
                HipY = baseline.HipCenter.y,
                LeftKneeY = baseline.LeftKnee.y,
                RightKneeY = baseline.RightKnee.y,
                LeftAnkleY = baseline.LeftAnkle.y,
                RightAnkleY = baseline.RightAnkle.y,
                HasKnees = baseline.KneesVisible,
                HasAnkles = baseline.AnklesVisible,
                Torso = TorsoLength(baseline),
                ShoulderWidth = Mathf.Max(0.02f, baseline.ShoulderWidth)
            };
            smoothed = pose;
            IsCalibrated = true;
            CalibrationProgress01 = 1f;
            lastGesture = "Ready";
            ClearTransientState();
            jumpArmed = true;
            stableBodyLane = 1;
            TargetLane = 1;
        }

        private void RecognizeGestures(BodyPoseSnapshot raw, float now, float dt, bool laneChanged)
        {
            float torso = Mathf.Max(0.04f, levels.Torso);
            bool feetVisible = smoothed.AnklesVisible && raw.AnklesVisible && levels.HasAnkles;
            float widthRatio = smoothed.ShoulderWidth / Mathf.Max(0.02f, levels.ShoulderWidth);
            bool sameDepth = widthRatio > MinimumWidthRatio && widthRatio < MaximumWidthRatio;

            float hipRise = (levels.HipY - smoothed.HipCenter.y) / torso;
            float shoulderRise = (levels.ShoulderY - smoothed.ShoulderCenter.y) / torso;
            float rawHipRise = (levels.HipY - raw.HipCenter.y) / torso;
            float rawShoulderRise = (levels.ShoulderY - raw.ShoulderCenter.y) / torso;
            float leftAnkleRise = feetVisible ? (levels.LeftAnkleY - smoothed.LeftAnkle.y) / torso : 0f;
            float rightAnkleRise = feetVisible ? (levels.RightAnkleY - smoothed.RightAnkle.y) / torso : 0f;
            float rawLeftFootRise = feetVisible ? (levels.LeftAnkleY - raw.LeftAnkle.y) / torso : 0f;
            float rawRightFootRise = feetVisible ? (levels.RightAnkleY - raw.RightAnkle.y) / torso : 0f;

            // A sideways step can briefly hide a leg from MediaPipe. Once the
            // player is already running, keep that run alive while the lane
            // change is confirmed instead of treating it as a stop.
            if ((laneChanged || candidateBodyLane >= 0) && IsRunHeld(now))
            {
                runUntil = Mathf.Max(runUntil, now + LaneChangeRunGrace);
            }

            float jumpBodyRise = Mathf.Lerp(MaximumJumpBodyRise, MinimumJumpBodyRise, jumpSensitivity);
            float jumpFeetRise = Mathf.Lerp(MaximumJumpFeetRise, MinimumJumpFeetRise, jumpSensitivity);
            if (!feetVisible) jumpBodyRise *= NoFeetJumpMultiplier;

            // Require agreement from hips and shoulders (and both feet when they
            // are visible), including the current unfiltered frame. This rejects
            // knee lifts, single-frame spikes and leaning toward the camera.
            float rawFeetRise = feetVisible ? Mathf.Min(rawLeftFootRise, rawRightFootRise) : 0f;
            bool torsoUp = rawHipRise > jumpBodyRise && rawShoulderRise > jumpBodyRise * 0.65f &&
                           hipRise > jumpBodyRise * 0.5f;
            bool feetUp = !feetVisible || rawFeetRise > jumpFeetRise;
            bool risingBefore = previousHipRise > jumpBodyRise * PreviousFrameFraction &&
                                previousShoulderRise > jumpBodyRise * 0.65f * PreviousFrameFraction &&
                                (!feetVisible || previousFeetRise > jumpFeetRise * PreviousFrameFraction);
            bool jumpNow = torsoUp && feetUp && risingBefore && sameDepth;
            previousHipRise = rawHipRise;
            previousShoulderRise = rawShoulderRise;
            previousFeetRise = rawFeetRise;
            if (jumpArmed && jumpNow && now - lastJumpAt > JumpRepeatSeconds)
            {
                bool wasRunning = IsRunHeld(now);
                pendingJump = true;
                pendingJumpUntil = now + JumpBufferSeconds;
                lastJumpAt = now;
                jumpArmed = false;
                jumpPoseActive = true;
                landingCandidateAt = -1;
                crouchHeld = false;
                crouchMaintainsRun = false;
                lastGesture = "Jump";
                if (wasRunning) runUntil = Mathf.Max(runUntil, now + JumpRunGrace);
            }

            bool landed = hipRise < jumpBodyRise * 0.45f &&
                          (!feetVisible ||
                           (Mathf.Max(leftAnkleRise, rightAnkleRise) < jumpFeetRise * 0.55f &&
                            Mathf.Max(rawLeftFootRise, rawRightFootRise) < jumpFeetRise * 0.55f));
            if (!jumpArmed && landed)
            {
                if (landingCandidateAt < 0) landingCandidateAt = now;
                if (now - landingCandidateAt >= LandingConfirmationSeconds)
                {
                    if (jumpPoseActive)
                    {
                        lastLandingAt = now;
                        // The next running step may use either foot.
                        ResetStepChannels();
                    }
                    jumpPoseActive = false;
                    jumpArmed = true;
                    jumpCandidateAt = -1;
                }
            }
            else landingCandidateAt = -1;

            float crouchHipEnter = Mathf.Lerp(0.28f, 0.10f, crouchSensitivity);
            float crouchShoulderEnter = Mathf.Lerp(0.18f, 0.07f, crouchSensitivity);
            float hipDrop = -hipRise;
            float shoulderDrop = -shoulderRise;
            // A front-facing squat can have nearly straight 2D knee angles. Use
            // the sustained lowering of hips AND shoulders, not an angle gate.
            bool loweredNow = -rawHipRise > crouchHipEnter && -rawShoulderRise > crouchShoulderEnter &&
                              hipDrop > crouchHipEnter * 0.5f && shoulderDrop > crouchShoulderEnter * 0.5f;
            bool loweredBefore = crouchCandidateAt >= 0;
            bool crouchEnter = !jumpPoseActive && now - lastLandingAt > 0.14f && sameDepth && loweredNow &&
                               Mathf.Max(leftAnkleRise, rightAnkleRise) < 0.14f;
            bool standingNow = -rawHipRise < crouchHipEnter * 0.5f && -rawShoulderRise < crouchShoulderEnter * 0.6f;
            bool crouchExit = standingNow && previousStand;
            previousStand = standingNow;
            if (!crouchHeld && crouchEnter && loweredBefore)
            {
                crouchMaintainsRun = IsRunHeld(now);
                crouchHeld = true;
                crouchCandidateAt = -1;
                lastGesture = "Crouch";
            }
            else if (!crouchHeld && !jumpPoseActive && sameDepth &&
                     -rawHipRise > crouchHipEnter * PreviousFrameFraction &&
                     -rawShoulderRise > crouchShoulderEnter * PreviousFrameFraction)
            {
                // Most of the way down: the next qualifying frame confirms the crouch.
                crouchCandidateAt = now;
            }
            else crouchCandidateAt = -1;

            if (crouchHeld && crouchExit)
            {
                crouchHeld = false;
                if (crouchMaintainsRun) runUntil = Mathf.Max(runUntil, now + CrouchReleaseRunGrace);
                crouchMaintainsRun = false;
                lastGesture = "Ready";
                ResetStepChannels();
            }

            if (!jumpPoseActive && !crouchHeld)
            {
                RecognizeRunStep(torso, dt, now);
            }

            bool settled = !jumpPoseActive && !crouchHeld && crouchCandidateAt < 0 &&
                           rawHipRise < jumpBodyRise * PreviousFrameFraction &&
                           now - lastLandingAt > 0.3f;
            if (settled)
            {
                bool nearLevel = Mathf.Abs(hipRise) < LevelFollowDeadband && Mathf.Abs(shoulderRise) < LevelFollowDeadband;
                FollowStandingLevels(dt, torso, nearLevel ? LevelFollowSeconds : LevelCatchUpSeconds);
            }
        }

        /// <summary>
        /// Slowly tracks the neutral posture so drifting closer to the camera or
        /// a sagging stance does not leave the player permanently "crouched".
        /// </summary>
        private void FollowStandingLevels(float dt, float torso, float seconds)
        {
            float follow = 1f - Mathf.Exp(-dt / seconds);
            float slowFollow = 1f - Mathf.Exp(-dt / LevelCatchUpSeconds);
            levels.ShoulderY = Mathf.Lerp(levels.ShoulderY, smoothed.ShoulderCenter.y, follow);
            levels.HipY = Mathf.Lerp(levels.HipY, smoothed.HipCenter.y, follow);
            levels.Torso = Mathf.Lerp(levels.Torso, Mathf.Max(0.04f, TorsoLength(smoothed)), follow);
            levels.ShoulderWidth = Mathf.Lerp(levels.ShoulderWidth, Mathf.Max(0.02f, smoothed.ShoulderWidth), follow);

            if (smoothed.KneesVisible)
            {
                if (!levels.HasKnees)
                {
                    levels.LeftKneeY = smoothed.LeftKnee.y;
                    levels.RightKneeY = smoothed.RightKnee.y;
                    levels.HasKnees = true;
                }
                else
                {
                    levels.LeftKneeY = Mathf.Lerp(levels.LeftKneeY, smoothed.LeftKnee.y,
                        Mathf.Abs(levels.LeftKneeY - smoothed.LeftKnee.y) < LevelFollowDeadband * torso ? follow : slowFollow * 0.25f);
                    levels.RightKneeY = Mathf.Lerp(levels.RightKneeY, smoothed.RightKnee.y,
                        Mathf.Abs(levels.RightKneeY - smoothed.RightKnee.y) < LevelFollowDeadband * torso ? follow : slowFollow * 0.25f);
                }
            }

            if (smoothed.AnklesVisible)
            {
                if (!levels.HasAnkles)
                {
                    // Feet that appear mid-game must be on the ground to become a reference.
                    levels.LeftAnkleY = smoothed.LeftAnkle.y;
                    levels.RightAnkleY = smoothed.RightAnkle.y;
                    levels.HasAnkles = Mathf.Abs(smoothed.LeftAnkle.y - smoothed.RightAnkle.y) < 0.05f * torso;
                }
                else
                {
                    levels.LeftAnkleY = Mathf.Lerp(levels.LeftAnkleY, smoothed.LeftAnkle.y,
                        Mathf.Abs(levels.LeftAnkleY - smoothed.LeftAnkle.y) < LevelFollowDeadband * torso ? follow : slowFollow * 0.25f);
                    levels.RightAnkleY = Mathf.Lerp(levels.RightAnkleY, smoothed.RightAnkle.y,
                        Mathf.Abs(levels.RightAnkleY - smoothed.RightAnkle.y) < LevelFollowDeadband * torso ? follow : slowFollow * 0.25f);
                }
            }
        }

        private bool UpdateBodyZone(float bodyCenterX, float now)
        {
            int desiredLane = ZoneWithHysteresis(bodyCenterX, stableBodyLane);
            if (stableBodyLane < 0)
            {
                CommitBodyLane(desiredLane);
                return true;
            }

            if (desiredLane == stableBodyLane)
            {
                candidateBodyLane = -1;
                TargetLane = stableBodyLane;
                return false;
            }

            if (desiredLane != candidateBodyLane)
            {
                candidateBodyLane = desiredLane;
                candidateBodyLaneStartedAt = now;
                candidateLaneFrames = 1;
                return false;
            }

            if (++candidateLaneFrames < ZoneConfirmationFrames)
            {
                return false;
            }

            CommitBodyLane(desiredLane);
            return true;
        }

        private void CommitBodyLane(int lane)
        {
            stableBodyLane = lane;
            candidateBodyLane = -1;
            TargetLane = lane;
            lastGesture = lane == 0 ? "Left section" : lane == 2 ? "Right section" : "Center section";
        }

        private static int ZoneWithHysteresis(float position, int currentLane)
        {
            switch (currentLane)
            {
                case 0:
                    if (position > RightZoneBoundary + ZoneHysteresis) return 2;
                    return position > LeftZoneBoundary + ZoneHysteresis ? 1 : 0;
                case 2:
                    if (position < LeftZoneBoundary - ZoneHysteresis) return 0;
                    return position < RightZoneBoundary - ZoneHysteresis ? 1 : 2;
                default:
                    if (position < LeftZoneBoundary - ZoneHysteresis) return 0;
                    return position > RightZoneBoundary + ZoneHysteresis ? 2 : 1;
            }
        }

        private static int ZoneFromPosition(float position)
        {
            return position < LeftZoneBoundary ? 0 : position > RightZoneBoundary ? 2 : 1;
        }

        private void ResetCalibrationCollection()
        {
            calibrationStarted = false;
            calibrationSamples = 0;
            calibrationSum = default;
            CalibrationProgress01 = 0f;
        }

        private void ResetBodyZone()
        {
            stableBodyLane = -1;
            candidateBodyLane = -1;
            candidateBodyLaneStartedAt = 0f;
            TargetLane = -1;
        }

        private void ResetStepChannels()
        {
            kneeSteps.Reset();
            ankleSteps.Reset();
            armSteps.Reset();
            lastStepAt = -100f;
        }

        private void RecognizeRunStep(float torso, float dt, float now)
        {
            float kneeThreshold = Mathf.Lerp(MaximumKneeStep, MinimumKneeStep, runSensitivity);
            float ankleThreshold = Mathf.Lerp(MaximumAnkleStep, MinimumAnkleStep, runSensitivity);
            float armThreshold = Mathf.Lerp(MaximumArmPump, MinimumArmPump, runSensitivity);

            bool step = false;
            step |= kneeSteps.Update((smoothed.LeftKnee.y - smoothed.RightKnee.y) / torso,
                kneeThreshold, dt, smoothed.KneesVisible);
            step |= ankleSteps.Update((smoothed.LeftAnkle.y - smoothed.RightAnkle.y) / torso,
                ankleThreshold, dt, smoothed.AnklesVisible);
            step |= armSteps.Update((smoothed.LeftWrist.y - smoothed.RightWrist.y) / torso,
                armThreshold, dt, smoothed.WristsVisible);

            // Knee, ankle and opposite-arm channels fire together on one stride.
            if (!step || now - lastStepAt < StepDebounceSeconds)
            {
                return;
            }

            float sinceLast = now - lastStepAt;
            bool continuing = sinceLast <= StepCadenceMaximum;
            if (continuing) stepInterval = Mathf.Lerp(stepInterval, sinceLast, 0.5f);
            float grace = continuing
                ? Mathf.Clamp(stepInterval * CadenceGraceMultiplier, MinimumRunGrace, MaximumRunGrace)
                : FirstStepRunGrace;
            // Never cut short a run that a jump or lane change deliberately extended.
            runUntil = Mathf.Max(runUntil, now + grace);
            lastStepAt = now;
            lastGesture = "Run";
        }

        private static float TorsoLength(BodyPoseSnapshot pose)
        {
            Vector2 delta = pose.HipCenter - pose.ShoulderCenter;
            delta.x *= Mathf.Max(0.5f, pose.Aspect);
            return delta.magnitude;
        }

        private static float KneeAngle(Vector2 hip, Vector2 knee, Vector2 ankle, float aspect)
        {
            Vector2 scale = new Vector2(Mathf.Max(0.5f, aspect), 1f);
            return Vector2.Angle(Vector2.Scale(hip - knee, scale), Vector2.Scale(ankle - knee, scale));
        }

        private void ClearTransientState()
        {
            jumpPoseActive = false;
            jumpArmed = false;
            jumpCandidateAt = landingCandidateAt = crouchCandidateAt = -1f;
            pendingJumpUntil = lastJumpAt = lastLandingAt = -100f;
            crouchHeld = false;
            crouchMaintainsRun = false;
            candidateBodyLane = -1;
            candidateBodyLaneStartedAt = 0f;
            ResetStepChannels();
            stepInterval = 0.4f;
            runUntil = -100f;
            pendingJump = false;
            previousHipRise = previousShoulderRise = previousFeetRise = 0f;
            previousStand = false;
            candidateLaneFrames = 0;
        }
    }
}
