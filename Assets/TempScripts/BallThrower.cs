using UnityEngine;

public class ArcThrow : MonoBehaviour
{
    public GameObject ballPrefab;
    public Transform throwPoint;

    [Header("Throw Settings")]
    public float launchSpeed = 15f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Throw();
        }
    }

    void Throw()
    {
        // Get aim point from mouse
        Vector3 targetPoint = GetMouseWorldPoint();

        // Calculate velocity using fixed speed
        if (!CalculateLaunchVelocity(
            throwPoint.position,
            targetPoint,
            launchSpeed,
            out Vector3 velocity))
        {
            return;
        }

        // Spawn ball ONLY if valid
        GameObject ball = Instantiate(ballPrefab, throwPoint.position, Quaternion.identity);
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        rb.linearVelocity = velocity;
    }
    
    Vector3 GetMouseWorldPoint()
    {
        Camera cam = Camera.main;

        // Plane at the player's height
        Plane plane = new Plane(Vector3.up, throwPoint.position);

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return throwPoint.position;
    }

    // using fixed speed
    bool CalculateLaunchVelocity(
        Vector3 start,
        Vector3 target,
        float speed,
        out Vector3 velocity)
    {
        velocity = Vector3.zero;

        Vector3 toTarget = target - start;
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0, toTarget.z);

        float y = toTarget.y;
        float x = toTargetXZ.magnitude;

        float gravity = Mathf.Abs(Physics.gravity.y);

        float speedSquared = speed * speed;

        float underRoot = speedSquared * speedSquared -
                          gravity * (gravity * x * x + 2 * y * speedSquared);
        
        if (underRoot < 0)
            return false;

        float root = Mathf.Sqrt(underRoot);

        // Use HIGH arc
        float angle = Mathf.Atan2(speedSquared + root, gravity * x);

        Vector3 direction = toTargetXZ.normalized;

        velocity =
            direction * speed * Mathf.Cos(angle) +
            Vector3.up * speed * Mathf.Sin(angle);

        // Safety check
        if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
            return false;

        return true;
    }
}