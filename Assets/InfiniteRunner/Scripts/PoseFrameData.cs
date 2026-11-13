using System;
using UnityEngine;

namespace InfiniteRunner
{
    /// <summary>
    /// Compact, platform-neutral pose sample. The Web bridge fills this from
    /// MediaPipe; a future iOS bridge can provide the exact same data.
    /// Coordinates are normalized to the camera image and already mirrored so
    /// moving toward screen-left produces a smaller X value.
    /// </summary>
    [Serializable]
    public sealed class PoseFrameData
    {
        public int sequence;
        public float timestampMs;
        public bool tracking;
        public float confidence;
        public float aspect = 1f;
        // Horizontal torso center in the visible, mirrored camera preview.
        public float bodyCenterX;

        public float noseX;
        public float noseY;
        public float leftShoulderX;
        public float leftShoulderY;
        public float rightShoulderX;
        public float rightShoulderY;
        public float leftHipX;
        public float leftHipY;
        public float rightHipX;
        public float rightHipY;
        public float leftKneeX;
        public float leftKneeY;
        public float rightKneeX;
        public float rightKneeY;
        public float leftAnkleX;
        public float leftAnkleY;
        public float rightAnkleX;
        public float rightAnkleY;
        public float leftWristX;
        public float leftWristY;
        public float rightWristX;
        public float rightWristY;
        // Optional joints. Only shoulders and hips are required for tracking;
        // legs and arms add evidence when the camera can see them clearly.
        public bool kneesVisible = true;
        public bool anklesVisible = true;
        public bool wristsVisible;

        public bool HasFiniteCoordinates()
        {
            return IsFinite(confidence) && IsFinite(aspect) && IsFinite(bodyCenterX) &&
                   IsFinite(noseX) && IsFinite(noseY) &&
                   IsFinite(leftShoulderX) && IsFinite(leftShoulderY) &&
                   IsFinite(rightShoulderX) && IsFinite(rightShoulderY) &&
                   IsFinite(leftHipX) && IsFinite(leftHipY) &&
                   IsFinite(rightHipX) && IsFinite(rightHipY) &&
                   IsFinite(leftKneeX) && IsFinite(leftKneeY) &&
                   IsFinite(rightKneeX) && IsFinite(rightKneeY) &&
                   IsFinite(leftAnkleX) && IsFinite(leftAnkleY) &&
                   IsFinite(rightAnkleX) && IsFinite(rightAnkleY) &&
                   (!wristsVisible || (IsFinite(leftWristX) && IsFinite(leftWristY) &&
                                       IsFinite(rightWristX) && IsFinite(rightWristY)));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal struct BodyPoseSnapshot
    {
        public Vector2 Nose;
        public Vector2 LeftShoulder;
        public Vector2 RightShoulder;
        public Vector2 LeftHip;
        public Vector2 RightHip;
        public Vector2 LeftKnee;
        public Vector2 RightKnee;
        public Vector2 LeftAnkle;
        public Vector2 RightAnkle;
        public Vector2 LeftWrist;
        public Vector2 RightWrist;
        public bool KneesVisible;
        public bool AnklesVisible;
        public bool WristsVisible;
        public float Aspect;
        public float BodyCenterX;

        public Vector2 ShoulderCenter => (LeftShoulder + RightShoulder) * 0.5f;
        public Vector2 HipCenter => (LeftHip + RightHip) * 0.5f;
        public Vector2 KneeCenter => (LeftKnee + RightKnee) * 0.5f;
        public Vector2 AnkleCenter => (LeftAnkle + RightAnkle) * 0.5f;
        public float TorsoHeight => Mathf.Abs(HipCenter.y - ShoulderCenter.y);
        public float ShoulderWidth => Mathf.Abs(RightShoulder.x - LeftShoulder.x) * Aspect;

        public static BodyPoseSnapshot FromFrame(PoseFrameData frame)
        {
            return new BodyPoseSnapshot
            {
                Nose = new Vector2(frame.noseX, frame.noseY),
                LeftShoulder = new Vector2(frame.leftShoulderX, frame.leftShoulderY),
                RightShoulder = new Vector2(frame.rightShoulderX, frame.rightShoulderY),
                LeftHip = new Vector2(frame.leftHipX, frame.leftHipY),
                RightHip = new Vector2(frame.rightHipX, frame.rightHipY),
                LeftKnee = new Vector2(frame.leftKneeX, frame.leftKneeY),
                RightKnee = new Vector2(frame.rightKneeX, frame.rightKneeY),
                LeftAnkle = new Vector2(frame.leftAnkleX, frame.leftAnkleY),
                RightAnkle = new Vector2(frame.rightAnkleX, frame.rightAnkleY),
                LeftWrist = new Vector2(frame.leftWristX, frame.leftWristY),
                RightWrist = new Vector2(frame.rightWristX, frame.rightWristY),
                KneesVisible = frame.kneesVisible,
                AnklesVisible = frame.anklesVisible && frame.kneesVisible,
                WristsVisible = frame.wristsVisible,
                Aspect = Mathf.Clamp(frame.aspect, 0.5f, 3f),
                BodyCenterX = frame.bodyCenterX
            };
        }

        public static BodyPoseSnapshot Lerp(BodyPoseSnapshot from, BodyPoseSnapshot to, float amount)
        {
            return new BodyPoseSnapshot
            {
                Nose = Vector2.Lerp(from.Nose, to.Nose, amount),
                LeftShoulder = Vector2.Lerp(from.LeftShoulder, to.LeftShoulder, amount),
                RightShoulder = Vector2.Lerp(from.RightShoulder, to.RightShoulder, amount),
                LeftHip = Vector2.Lerp(from.LeftHip, to.LeftHip, amount),
                RightHip = Vector2.Lerp(from.RightHip, to.RightHip, amount),
                LeftKnee = Vector2.Lerp(from.LeftKnee, to.LeftKnee, amount),
                RightKnee = Vector2.Lerp(from.RightKnee, to.RightKnee, amount),
                LeftAnkle = Vector2.Lerp(from.LeftAnkle, to.LeftAnkle, amount),
                RightAnkle = Vector2.Lerp(from.RightAnkle, to.RightAnkle, amount),
                LeftWrist = Vector2.Lerp(from.LeftWrist, to.LeftWrist, amount),
                RightWrist = Vector2.Lerp(from.RightWrist, to.RightWrist, amount),
                KneesVisible = to.KneesVisible,
                AnklesVisible = to.AnklesVisible,
                WristsVisible = to.WristsVisible,
                Aspect = Mathf.Lerp(from.Aspect, to.Aspect, amount),
                BodyCenterX = Mathf.Lerp(from.BodyCenterX, to.BodyCenterX, amount)
            };
        }

        public static BodyPoseSnapshot operator +(BodyPoseSnapshot left, BodyPoseSnapshot right)
        {
            return new BodyPoseSnapshot
            {
                Nose = left.Nose + right.Nose,
                LeftShoulder = left.LeftShoulder + right.LeftShoulder,
                RightShoulder = left.RightShoulder + right.RightShoulder,
                LeftHip = left.LeftHip + right.LeftHip,
                RightHip = left.RightHip + right.RightHip,
                LeftKnee = left.LeftKnee + right.LeftKnee,
                RightKnee = left.RightKnee + right.RightKnee,
                LeftAnkle = left.LeftAnkle + right.LeftAnkle,
                RightAnkle = left.RightAnkle + right.RightAnkle,
                LeftWrist = left.LeftWrist + right.LeftWrist,
                RightWrist = left.RightWrist + right.RightWrist,
                KneesVisible = left.KneesVisible || right.KneesVisible,
                AnklesVisible = left.AnklesVisible || right.AnklesVisible,
                WristsVisible = left.WristsVisible || right.WristsVisible,
                Aspect = left.Aspect + right.Aspect,
                BodyCenterX = left.BodyCenterX + right.BodyCenterX
            };
        }

        public static BodyPoseSnapshot operator /(BodyPoseSnapshot pose, float divisor)
        {
            return new BodyPoseSnapshot
            {
                Nose = pose.Nose / divisor,
                LeftShoulder = pose.LeftShoulder / divisor,
                RightShoulder = pose.RightShoulder / divisor,
                LeftHip = pose.LeftHip / divisor,
                RightHip = pose.RightHip / divisor,
                LeftKnee = pose.LeftKnee / divisor,
                RightKnee = pose.RightKnee / divisor,
                LeftAnkle = pose.LeftAnkle / divisor,
                RightAnkle = pose.RightAnkle / divisor,
                LeftWrist = pose.LeftWrist / divisor,
                RightWrist = pose.RightWrist / divisor,
                KneesVisible = pose.KneesVisible,
                AnklesVisible = pose.AnklesVisible,
                WristsVisible = pose.WristsVisible,
                Aspect = pose.Aspect / divisor,
                BodyCenterX = pose.BodyCenterX / divisor
            };
        }
    }
}
