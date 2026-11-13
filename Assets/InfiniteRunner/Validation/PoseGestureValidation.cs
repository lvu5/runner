#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace InfiniteRunner
{
    public static class PoseGestureValidation
    {
        private static int checks;
        private static float maxJumpLatency, maxCrouchLatency;

        [MenuItem("Infinite Runner/Validate Body Controls", priority = 26)]
        public static void Validate()
        {
            checks = 0; maxJumpLatency = maxCrouchLatency = 0;
            foreach (float fps in new[] { 12f, 15f, 24f, 30f }) ValidateAtRate(fps);
            ValidateRunContinuity();
            foreach (float fps in new[] { 15f, 30f }) ValidateRobustness(fps);
            Directory.CreateDirectory("Builds/Previews");
            File.WriteAllText("Builds/Previews/pose-control-checks.json",
                $"{{\"source\":\"synthetic landmark replay, not a live camera test\",\"checks\":{checks},\"fps\":[12,15,24,30],\"robustness\":[\"cropped feet\",\"arm pumping\",\"flickering feet\",\"camera distance drift\",\"static arm asymmetry\"],\"maximumJumpDetectionMs\":{Mathf.RoundToInt(maxJumpLatency * 1000)},\"maximumCrouchDetectionMs\":{Mathf.RoundToInt(maxCrouchLatency * 1000)}}}");
            Debug.Log($"POSE CONTROL CHECKS PASSED: {checks} checks at 12/15/24/30 Hz; stationary noise, isolated spikes, frontal knee lifts, sideways jumps, repeated jumps, landing/rearm, crouches, sensitivity, calibration, tracking recovery, and continuous running through avatar jumps. Synthetic max jump/crouch detection: {maxJumpLatency * 1000:0}/{maxCrouchLatency * 1000:0} ms.");
        }

        private static void ValidateRunContinuity()
        {
            foreach (float gameFps in new[] { 30f, 60f, 120f })
            {
                var continuity = new CameraRunContinuity();
                float dt = 1 / gameFps;
                Require(!continuity.Resolve(false, true, true, true, 0), "A standing jump must not start forward movement");
                continuity.Reset();
                Require(continuity.Resolve(true, false, true, true, 0), "Running must start normally");
                Require(continuity.Resolve(false, false, true, true, 0.1f), "A short missed running sample caused braking");
                Require(!continuity.Resolve(false, false, true, true, 0.3f), "Standing still must eventually stop the run");
                continuity.Reset();
                continuity.Resolve(true, false, true, true, 0);
                Require(continuity.Resolve(false, true, true, true, 0.15f), "Takeoff lost the recently established run");
                // No run gestures arrive for the whole flight. Forward speed must stay constant.
                // This includes a longer flight from a metro roof back to street level.
                foreach (float speed in new[] { 8f, 17f })
                {
                    continuity.Reset();
                    continuity.Resolve(true, false, true, true, 0);
                    continuity.Resolve(false, true, true, true, 0.15f);
                    float actualSpeed = speed;
                    for (float t = 0.15f + dt; t < 1.3f; t += dt)
                    {
                        bool runHeld = continuity.Resolve(false, false, t >= 1.1f, true, t);
                        actualSpeed = Mathf.MoveTowards(actualSpeed, runHeld ? speed : 0, 60 * dt);
                    }
                    Require(Mathf.Abs(actualSpeed - speed) < 0.001f, $"Run speed dropped during flight/landing at {gameFps} fps");
                    Require(continuity.Resolve(true, false, true, true, 1.3f), "Resuming a step after landing caused a stop");
                    Require(!continuity.Resolve(false, false, true, true, 1.8f), "Landing assistance continued after the player stopped");
                }
                continuity.Reset();
                continuity.Resolve(true, false, true, true, 0);
                continuity.Resolve(false, true, true, true, 0.1f);
                continuity.Resolve(false, false, false, true, 0.3f);
                continuity.Resolve(false, false, true, true, 0.9f);
                Require(continuity.Resolve(false, true, true, true, 1.1f), "A consecutive jump interrupted forward movement");
                Require(continuity.Resolve(false, false, false, true, 1.6f), "Consecutive jump lost its carried run");
                Require(!continuity.Resolve(false, false, false, false, 1.7f), "Tracking loss must cancel carried momentum");
                Require(!continuity.Resolve(false, false, true, true, 1.8f), "Tracking recovery resumed an old run");
                continuity.Resolve(true, true, true, true, 2);
                continuity.Reset();
                Require(!continuity.Resolve(false, false, true, true, 2.1f), "Mode change/restart retained momentum");
                continuity.Resolve(true, true, true, true, 3);
                Require(!continuity.Resolve(false, false, false, true, 5), "Missing landing must not latch running indefinitely");
            }
        }

        private static void ValidateRobustness(float fps)
        {
            // Feet cropped by the camera: calibration, jump, crouch and arm-pump running still work.
            var cropped = new Replay(fps);
            cropped.Feed(1.7f, _ => Cropped(Neutral()));
            Require(cropped.Recognizer.IsCalibrated, "Calibration required visible feet");
            cropped.Feed(1.2f, t => Cropped(ArmPump(t * 14)));
            Require(cropped.Recognizer.IsRunHeld(cropped.Now) && cropped.Jumps == 0, "Arm pumping did not run with cropped legs");
            cropped.Feed(1.5f, _ => Cropped(Neutral()));
            Require(!cropped.Recognizer.IsRunHeld(cropped.Now), "Arm-pump running stayed latched");
            cropped.Feed(0.45f, _ => Cropped(JumpPose(0.07f)));
            Require(cropped.Jumps == 1, "A jump was missed with cropped feet");
            cropped.Feed(0.4f, _ => Cropped(Neutral()));
            cropped.Feed(0.6f, _ => Cropped(CrouchPose(0.05f)));
            Require(cropped.Recognizer.CrouchHeld, "A crouch was missed with cropped feet");
            cropped.Feed(0.4f, _ => Cropped(Neutral()));
            Require(!cropped.Recognizer.CrouchHeld && cropped.Jumps == 1, "Cropped crouch did not release cleanly");
            cropped.Feed(0.4f, _ => Cropped(JumpPose(0.035f)));
            Require(cropped.Jumps == 1, "A small torso bob became a jump without feet evidence");

            // Feet flicker in and out of view while running: no phantom jumps or stops.
            var flicker = new Replay(fps); flicker.Calibrate();
            int frame = 0;
            flicker.Feed(2f, t => { var p = StepPose(Mathf.Sin(t * 12) >= 0); p.AnklesVisible = (frame++ / 3) % 2 == 0; return p; });
            Require(flicker.Recognizer.IsRunHeld(flicker.Now) && flicker.Jumps == 0 && !flicker.EverCrouched, "Flickering feet broke running");

            // Walking slowly toward the camera must not register as crouch or jump.
            var drift = new Replay(fps); drift.Calibrate();
            drift.Feed(3f, t => Zoom(Neutral(), 1 + 0.18f * t / 3));
            drift.Feed(1f, _ => Zoom(Neutral(), 1.18f));
            Require(!drift.EverCrouched && drift.Jumps == 0, "Moving closer to the camera triggered an action");
            // ...and actions still work at the new distance, measured from the adapted posture.
            drift.Feed(0.5f, _ => Zoom(CrouchPose(0.05f), 1.18f));
            Require(drift.Recognizer.CrouchHeld, "Crouch failed after the player moved closer");
            drift.Feed(0.5f, _ => Zoom(Neutral(), 1.18f));
            drift.Feed(0.45f, _ => Zoom(JumpPose(0.07f), 1.18f));
            Require(drift.Jumps == 1, "Jump failed after the player moved closer");

            // A static arm asymmetry (holding a phone, one hand on hip) must not run.
            var asym = new Replay(fps); asym.Calibrate();
            asym.Feed(3f, _ => { var p = Neutral(); p.LeftWrist.y -= 0.12f; return p; });
            asym.Feed(1.2f, _ => { var p = Neutral(); p.LeftWrist.y -= 0.12f; return p; });
            Require(!asym.Recognizer.IsRunHeld(asym.Now), "A raised hand kept the runner moving");
        }

        private static void ValidateAtRate(float fps)
        {
            var replay = new Replay(fps);
            replay.Calibrate();
            replay.Feed(2f, t => Shift(Neutral(), 0, Mathf.Sin(t * 23) * 0.0015f));
            Require(replay.Jumps == 0 && !replay.Recognizer.CrouchHeld && replay.Recognizer.TargetLane == 1, "Idle noise issued an action");
            replay.Step(JumpPose(0.08f)); replay.Feed(0.5f, _ => Neutral());
            Require(replay.Jumps == 0, "A one-frame spike triggered a jump");
            replay.Feed(1.2f, t => StepPose(Mathf.Sin(t * 12) >= 0));
            Require(replay.Jumps == 0 && !replay.Recognizer.CrouchHeld, "Knee lifts became a jump or crouch");
            Require(replay.Recognizer.IsRunHeld(replay.Now), "Running regressed");
            replay.Feed(0.5f, _ => Neutral());

            var frontalRun = new Replay(fps); frontalRun.Calibrate();
            frontalRun.Feed(0.3f, _ => FrontalStepPose(true));
            Require(frontalRun.Recognizer.IsRunHeld(frontalRun.Now), "A frontal knee lift was rejected by projected knee angle");
            frontalRun.Feed(0.4f, _ => JumpPose(0.06f));
            frontalRun.Feed(0.35f, _ => Neutral());
            frontalRun.Feed(0.3f, _ => FrontalStepPose(true));
            Require(frontalRun.Recognizer.IsRunHeld(frontalRun.Now), "Running could not resume with the same foot after a jump");
            frontalRun.Feed(1.6f, _ => Neutral());
            Require(!frontalRun.Recognizer.IsRunHeld(frontalRun.Now), "Frontal running remained latched while standing still");

            replay.ResetCount();
            float startedAt = replay.Now;
            replay.Feed(0.42f, t => Shift(JumpPose(0.06f * Mathf.Sin(Mathf.PI * t / 0.42f)), -0.27f, 0));
            Require(replay.Jumps == 1, "A sideways jump was missed or duplicated at " + fps);
            maxJumpLatency = Mathf.Max(maxJumpLatency, replay.FirstJumpAt - startedAt);
            Require(replay.FirstJumpAt - startedAt <= 0.26f, "Jump recognition is too late");
            replay.Feed(0.35f, _ => Shift(Neutral(), -0.27f, 0));
            Require(!replay.Recognizer.CrouchHeld, "Landing triggered a crouch");
            replay.Feed(0.42f, t => Shift(JumpPose(0.06f * Mathf.Sin(Mathf.PI * t / 0.42f)), -0.27f, 0));
            Require(replay.Jumps == 2, "A second jump after landing was missed");

            replay = new Replay(fps); replay.Calibrate();
            replay.Feed(0.5f, _ => JumpPose(0.06f));
            Require(replay.Jumps == 1, "Jump confirmation failed");
            replay.Feed(0.3f, _ => FeetOnly(0.06f));
            replay.Feed(0.4f, _ => JumpPose(0.06f));
            Require(replay.Jumps == 1, "Hip jitter rearmed a jump before the feet landed");

            replay = new Replay(fps); replay.Calibrate();
            replay.Step(CrouchPose(0.055f)); replay.Feed(0.35f, _ => Neutral());
            Require(!replay.EverCrouched, "A one-frame crouch spike was accepted");
            replay.Feed(0.7f, _ => LeanPose());
            Require(!replay.EverCrouched, "Leaning the shoulders caused a crouch");
            replay.Feed(0.3f, _ => Neutral());
            startedAt = replay.Now;
            replay.Feed(0.8f, _ => CrouchPose(0.048f));
            Require(replay.Recognizer.CrouchHeld, "Front-facing crouch with straight projected knees was missed");
            maxCrouchLatency = Mathf.Max(maxCrouchLatency, replay.FirstCrouchAt - startedAt);
            Require(replay.FirstCrouchAt - startedAt <= 0.26f, "Crouch recognition is too late");
            replay.Step(Neutral()); replay.Feed(0.3f, _ => CrouchPose(0.048f));
            Require(replay.Recognizer.CrouchHeld, "One noisy standing frame released the crouch");
            replay.Feed(0.35f, _ => Neutral());
            Require(!replay.Recognizer.CrouchHeld && replay.Jumps == 0, "Standing up stuck crouch or triggered jump");

            foreach (bool sensitive in new[] { false, true })
            {
                replay = new Replay(fps); replay.Calibrate();
                replay.Recognizer.SetSensitivities(0.65f, sensitive ? 1 : 0, sensitive ? 1 : 0);
                replay.Feed(0.5f, _ => CrouchPose(0.026f));
                Require(replay.Recognizer.CrouchHeld == sensitive, "Crouch sensitivity has no useful effect");
                replay.Feed(0.4f, _ => Neutral());
                replay.Feed(0.45f, _ => JumpPose(0.028f));
                Require((replay.Jumps == 1) == sensitive, "Jump sensitivity has no useful effect");
            }

            replay = new Replay(fps);
            replay.Feed(2.5f, t => Shift(Neutral(), 0, Mathf.Sin(t * 6) * 0.04f));
            Require(!replay.Recognizer.IsCalibrated, "Moving samples produced a neutral calibration");
            replay.Feed(1.7f, _ => Neutral());
            Require(replay.Recognizer.IsCalibrated, "Still pose could not finish calibration");
            replay.Recognizer.LoseTracking();
            replay.Feed(0.5f, _ => JumpPose(0.06f));
            Require(replay.Jumps == 0, "Reacquiring an airborne pose caused a phantom jump");
            replay.Feed(0.35f, _ => Neutral());
            replay.Feed(0.4f, _ => JumpPose(0.06f));
            Require(replay.Jumps == 1, "Jump did not recover after returning to neutral");
            replay.Recognizer.LoseTracking();
            Require(!replay.Recognizer.CrouchHeld && !replay.Recognizer.IsRunHeld(replay.Now) && !replay.Recognizer.ConsumeJump(replay.Now), "Tracking loss retained an action");

            replay = new Replay(fps); replay.Calibrate(); replay.ConsumeActions = false;
            replay.Feed(0.25f, _ => JumpPose(0.06f));
            replay.Now += 0.3f; replay.Recognizer.Tick(replay.Now);
            Require(!replay.Recognizer.ConsumeJump(replay.Now), "An old jump remained buffered");
            replay.Recognizer.LoseTracking(); replay.Feed(0.3f, _ => Neutral());
            replay.Feed(0.17f, _ => JumpPose(0.06f));
            Require(replay.Recognizer.ConsumeJump(replay.Now), "A fresh jump was not buffered");
            Require(!replay.Recognizer.ConsumeJump(replay.Now), "A buffered jump was consumed twice");
            replay.Feed(0.5f, _ => Neutral()); replay.Feed(0.4f, _ => CrouchPose(0.05f));
            replay.Recognizer.ClearBufferedActions(replay.Now);
            Require(!replay.Recognizer.CrouchHeld, "Restart retained a stale crouch");
        }

        private sealed class Replay
        {
            public readonly PoseGestureRecognizer Recognizer = new PoseGestureRecognizer();
            public float Now, FirstJumpAt = -1, FirstCrouchAt = -1;
            public int Jumps;
            public bool EverCrouched, ConsumeActions = true;
            private readonly float dt;
            public Replay(float fps) { dt = 1 / fps; }
            public void Calibrate()
            {
                Feed(1.7f, _ => Neutral());
                Require(Recognizer.IsCalibrated, "Neutral calibration failed");
            }
            public void ResetCount() { Jumps = 0; FirstJumpAt = -1; }
            public void Feed(float duration, Func<float, BodyPoseSnapshot> pose)
            {
                for (float elapsed = dt; elapsed <= duration + dt * 0.01f; elapsed += dt) Step(pose(elapsed));
            }
            public void Step(BodyPoseSnapshot pose)
            {
                Now += dt;
                Recognizer.Process(pose, Now); Recognizer.Tick(Now);
                if (ConsumeActions && Recognizer.ConsumeJump(Now))
                { Jumps++; if (FirstJumpAt < 0) FirstJumpAt = Now; }
                if (Recognizer.CrouchHeld)
                { EverCrouched = true; if (FirstCrouchAt < 0) FirstCrouchAt = Now; }
            }
        }

        private static BodyPoseSnapshot Neutral() => new BodyPoseSnapshot
        {
            Nose = new Vector2(0.5f, 0.24f),
            LeftShoulder = new Vector2(0.44f, 0.35f), RightShoulder = new Vector2(0.56f, 0.35f),
            LeftHip = new Vector2(0.46f, 0.55f), RightHip = new Vector2(0.54f, 0.55f),
            LeftKnee = new Vector2(0.46f, 0.73f), RightKnee = new Vector2(0.54f, 0.73f),
            LeftAnkle = new Vector2(0.46f, 0.93f), RightAnkle = new Vector2(0.54f, 0.93f),
            LeftWrist = new Vector2(0.42f, 0.56f), RightWrist = new Vector2(0.58f, 0.56f),
            KneesVisible = true, AnklesVisible = true, WristsVisible = true,
            Aspect = 4f / 3, BodyCenterX = 0.5f
        };
        // Typical laptop framing: the feet (and often the knees) are cropped.
        private static BodyPoseSnapshot Cropped(BodyPoseSnapshot p, bool knees = false)
        { p.AnklesVisible = false; p.KneesVisible = knees; return p; }
        // Scale the whole body about the camera centre, as when walking toward the lens.
        private static BodyPoseSnapshot Zoom(BodyPoseSnapshot p, float s)
        {
            Vector2 c = new Vector2(0.5f, 0.5f);
            Vector2 Z(Vector2 v) => c + (v - c) * s;
            p.Nose = Z(p.Nose); p.LeftShoulder = Z(p.LeftShoulder); p.RightShoulder = Z(p.RightShoulder);
            p.LeftHip = Z(p.LeftHip); p.RightHip = Z(p.RightHip); p.LeftKnee = Z(p.LeftKnee); p.RightKnee = Z(p.RightKnee);
            p.LeftAnkle = Z(p.LeftAnkle); p.RightAnkle = Z(p.RightAnkle); p.LeftWrist = Z(p.LeftWrist); p.RightWrist = Z(p.RightWrist);
            return p;
        }
        private static BodyPoseSnapshot ArmPump(float phase)
        {
            var p = Neutral();
            float swing = Mathf.Sin(phase) * 0.06f;
            p.LeftWrist.y += swing; p.RightWrist.y -= swing;
            return p;
        }
        private static BodyPoseSnapshot Shift(BodyPoseSnapshot p, float x, float y)
        {
            Vector2 d = new Vector2(x, y);
            p.Nose += d; p.LeftShoulder += d; p.RightShoulder += d;
            p.LeftHip += d; p.RightHip += d; p.LeftKnee += d; p.RightKnee += d;
            p.LeftAnkle += d; p.RightAnkle += d; p.BodyCenterX += x;
            return p;
        }
        private static BodyPoseSnapshot JumpPose(float rise) => Shift(Neutral(), 0, -rise);
        private static BodyPoseSnapshot FeetOnly(float rise)
        { var p = Neutral(); p.LeftAnkle.y -= rise; p.RightAnkle.y -= rise; return p; }
        private static BodyPoseSnapshot CrouchPose(float drop)
        {
            var p = Neutral(); p.LeftHip.y += drop; p.RightHip.y += drop;
            p.LeftShoulder.y += drop; p.RightShoulder.y += drop; p.Nose.y += drop;
            p.LeftKnee.y += drop * 0.3f; p.RightKnee.y += drop * 0.3f;
            return p;
        }
        private static BodyPoseSnapshot LeanPose()
        { var p = Neutral(); p.LeftShoulder.y += 0.06f; p.RightShoulder.y += 0.06f; return p; }
        private static BodyPoseSnapshot StepPose(bool left)
        {
            var p = Neutral();
            if (left) { p.LeftKnee += new Vector2(-0.05f, -0.075f); p.LeftAnkle.y -= 0.05f; }
            else { p.RightKnee += new Vector2(0.05f, -0.075f); p.RightAnkle.y -= 0.05f; }
            return p;
        }
        private static BodyPoseSnapshot FrontalStepPose(bool left)
        {
            var p = StepPose(left);
            p.LeftKnee.x = p.LeftHip.x;
            p.RightKnee.x = p.RightHip.x;
            return p;
        }
        private static void Require(bool valid, string message)
        { checks++; if (!valid) throw new InvalidOperationException("Pose controls: " + message); }
    }
}
#endif
