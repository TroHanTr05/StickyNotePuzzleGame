using UnityEngine;

namespace Game.Runtime
{
    public class BillboardFacer : MonoBehaviour
    {
        public enum FaceMode { Full, YAxisOnly, LookAt }

        [Header("Settings")]
        public FaceMode Mode = FaceMode.Full;

        [Tooltip("Camera to face. Defaults to Camera.main if left empty.")]
        public Camera TargetCamera;

        [Tooltip("Flip the facing direction (useful if the sprite/text is back-to-front).")]
        public bool FlipFacing = false;

        [Tooltip("Offset in degrees added after the billboard rotation — for fine-tuning.")]
        public Vector3 RotationOffset = Vector3.zero;

        void LateUpdate()
        {
            Camera cam = TargetCamera != null ? TargetCamera : Camera.main;
            if (cam == null) return;

            switch (Mode)
            {
                case FaceMode.Full:
                    transform.rotation = cam.transform.rotation;
                    break;

                case FaceMode.YAxisOnly:
                    Vector3 camPosFlat = new Vector3(
                        cam.transform.position.x,
                        transform.position.y,
                        cam.transform.position.z);

                    Vector3 dir = camPosFlat - transform.position;
                    if (dir.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(
                            FlipFacing ? -dir : dir, Vector3.up);
                    break;

                case FaceMode.LookAt:
                    Vector3 toCamera = cam.transform.position - transform.position;
                    if (toCamera.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(
                            FlipFacing ? toCamera : -toCamera, Vector3.up);
                    break;
            }

            if (RotationOffset != Vector3.zero)
                transform.rotation *= Quaternion.Euler(RotationOffset);
        }
    }
}
