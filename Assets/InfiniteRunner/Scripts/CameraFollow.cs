using UnityEngine;

namespace InfiniteRunner
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 5.25f, -8.5f);
        [SerializeField] private float positionSmoothTime = 0.12f;
        [SerializeField] private float rotationSharpness = 9f;
        [SerializeField, Range(0f, 1f)] private float lateralPositionFollow = 0.25f;
        [SerializeField, Range(0f, 1f)] private float lateralLookFollow = 0.5f;

        private Transform target;
        private Vector3 velocity;
        private float trackCenterX;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            trackCenterX = target.position.x;
            transform.position = DesiredPosition();
            LookAtTarget(true);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = DesiredPosition();
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref velocity,
                positionSmoothTime);

            LookAtTarget(false);
        }

        private void LookAtTarget(bool immediately)
        {
            float focusX = Mathf.Lerp(trackCenterX, target.position.x, lateralLookFollow);
            Vector3 focusPoint = new Vector3(
                focusX,
                target.position.y + 1.1f,
                target.position.z + 9f);
            Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);

            transform.rotation = immediately
                ? desiredRotation
                : Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }

        private Vector3 DesiredPosition()
        {
            float followedX = Mathf.Lerp(trackCenterX, target.position.x, lateralPositionFollow);
            return new Vector3(
                followedX + offset.x,
                target.position.y + offset.y,
                target.position.z + offset.z);
        }
    }
}
