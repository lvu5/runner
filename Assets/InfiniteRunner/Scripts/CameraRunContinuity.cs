namespace InfiniteRunner
{
    /// <summary>Keeps an established camera run continuous through the avatar's jump.</summary>
    internal sealed class CameraRunContinuity
    {
        private const float SignalGraceSeconds = 0.18f;
        private const float TakeoffGraceSeconds = 0.4f;
        private const float LandingGraceSeconds = 0.35f;
        private const float MaximumJumpSeconds = 1.6f;

        private float lastRunAt = -100f;
        private float jumpStartedAt = -100f;
        private float landingUntil = -100f;
        private bool carryingRun;
        private bool leftGround;

        public bool Resolve(bool detectedRun, bool jumpStarted, bool grounded, bool trackingValid, float now)
        {
            if (!trackingValid)
            {
                Reset();
                return false;
            }

            if (detectedRun) lastRunAt = now;
            bool recentRun = now - lastRunAt <= TakeoffGraceSeconds;
            if (jumpStarted && (recentRun || carryingRun || now <= landingUntil))
            {
                carryingRun = true;
                leftGround = false;
                jumpStartedAt = now;
                landingUntil = -100f;
            }

            if (carryingRun)
            {
                if (!grounded) leftGround = true;
                else if (leftGround)
                {
                    carryingRun = false;
                    landingUntil = now + LandingGraceSeconds;
                }
                if (now - jumpStartedAt > MaximumJumpSeconds) carryingRun = false;
            }

            return detectedRun || now - lastRunAt <= SignalGraceSeconds || carryingRun || now <= landingUntil;
        }

        public void Reset()
        {
            lastRunAt = jumpStartedAt = landingUntil = -100f;
            carryingRun = leftGround = false;
        }
    }
}
