using UnityEngine;

namespace InfiniteRunner
{
    public sealed class RunnerVisualAnimator : MonoBehaviour
    {
        private GameManager gameManager;
        private RunnerController runner;
        private Transform visualRoot;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftLeg;
        private Transform rightLeg;
        private Vector3 visualStartPosition;
        private float runPhase;

        public void Configure(
            GameManager manager,
            RunnerController runnerController,
            Transform root,
            Transform armLeft,
            Transform armRight,
            Transform legLeft,
            Transform legRight)
        {
            gameManager = manager;
            runner = runnerController;
            visualRoot = root;
            leftArm = armLeft;
            rightArm = armRight;
            leftLeg = legLeft;
            rightLeg = legRight;
            visualStartPosition = root.localPosition;
        }

        private void Update()
        {
            if (gameManager == null || visualRoot == null)
            {
                return;
            }

            if (!gameManager.IsRunning)
            {
                visualRoot.localRotation = Quaternion.Slerp(
                    visualRoot.localRotation,
                    Quaternion.Euler(0f, 0f, -14f),
                    1f - Mathf.Exp(-6f * Time.deltaTime));
                return;
            }

            visualRoot.localRotation = Quaternion.identity;

            if (runner.IsSliding)
            {
                SetLimbRotations(20f, -20f);
                visualRoot.localPosition = visualStartPosition;
                return;
            }

            if (!runner.IsMovingForward)
            {
                leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.identity, 10f * Time.deltaTime);
                rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.identity, 10f * Time.deltaTime);
                leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, 10f * Time.deltaTime);
                rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, 10f * Time.deltaTime);
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, visualStartPosition, 10f * Time.deltaTime);
                return;
            }

            // Keep the stride continuous while forward speed eases up or down.
            runPhase += gameManager.CurrentSpeed * 1.25f * Time.deltaTime;
            float swing = Mathf.Sin(runPhase) * 38f;
            SetLimbRotations(swing, -swing);

            float bob = runner.IsGrounded ? Mathf.Abs(Mathf.Sin(runPhase)) * 0.045f : 0f;
            visualRoot.localPosition = visualStartPosition + Vector3.up * bob;
        }

        private void SetLimbRotations(float leftSwing, float rightSwing)
        {
            leftArm.localRotation = Quaternion.Euler(leftSwing, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(rightSwing, 0f, 0f);
            leftLeg.localRotation = Quaternion.Euler(rightSwing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(leftSwing, 0f, 0f);
        }
    }
}
