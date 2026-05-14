using UnityEngine;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    public class ArcThrow : MonoBehaviour
    {
        private static IGameLog Log => ServiceResolver.Resolve<IGameLog>();

        public GameObject ballPrefab;
        public Transform throwPoint;

        [Header("Throw Settings")]
        public float launchSpeed = 15f;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                Throw();
        }

        void Throw()
        {
            Vector3 targetPoint = GetMouseWorldPoint();

            if (!CalculateLaunchVelocity(throwPoint.position, targetPoint, launchSpeed, out Vector3 velocity))
            {
                Log.Warn("[ArcThrow] Could not compute launch velocity — target may be out of range.");
                return;
            }

            GameObject ball = Instantiate(ballPrefab, throwPoint.position, Quaternion.identity);
            Rigidbody rb    = ball.GetComponent<Rigidbody>();
            rb.linearVelocity = velocity;

            Log.Info($"[ArcThrow] Ball thrown at speed {launchSpeed}.");
        }

        Vector3 GetMouseWorldPoint()
        {
            Camera cam = Camera.main;
            Plane  plane = new Plane(Vector3.up, throwPoint.position);
            Ray    ray   = cam.ScreenPointToRay(Input.mousePosition);

            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : throwPoint.position;
        }

        bool CalculateLaunchVelocity(
            Vector3 start,
            Vector3 target,
            float   speed,
            out Vector3 velocity)
        {
            velocity = Vector3.zero;

            Vector3 toTarget   = target - start;
            Vector3 toTargetXZ = new Vector3(toTarget.x, 0, toTarget.z);

            float y       = toTarget.y;
            float x       = toTargetXZ.magnitude;
            float gravity = Mathf.Abs(Physics.gravity.y);
            float speed2  = speed * speed;

            float underRoot = speed2 * speed2 - gravity * (gravity * x * x + 2 * y * speed2);
            if (underRoot < 0) return false;

            float root  = Mathf.Sqrt(underRoot);
            float angle = Mathf.Atan2(speed2 + root, gravity * x);

            Vector3 direction = toTargetXZ.normalized;
            velocity = direction * speed * Mathf.Cos(angle) + Vector3.up * speed * Mathf.Sin(angle);

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                return false;

            return true;
        }
    }
}
