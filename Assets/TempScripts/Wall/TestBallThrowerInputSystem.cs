using UnityEngine;
using UnityEngine.InputSystem;

public class TestBallThrowerInputSystem : MonoBehaviour
{
    public GameObject ballPrefab;
    public Transform throwPoint;
    public float arcHeight = 5f;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Throw();
        }
    }

    void Throw()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("No Main Camera found.");
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePosition);

        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(30f);
        }

        GameObject ball = Instantiate(ballPrefab, throwPoint.position, Quaternion.identity);
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Ball prefab does not have a Rigidbody.");
            return;
        }

        Vector3 velocity = CalculateArcVelocity(throwPoint.position, targetPoint, arcHeight);

        rb.linearVelocity = velocity;
    }

    Vector3 CalculateArcVelocity(Vector3 start, Vector3 end, float height)
    {
        float gravity = Physics.gravity.y;

        Vector3 displacement = end - start;
        Vector3 displacementXZ = new Vector3(displacement.x, 0, displacement.z);

        float timeUp = Mathf.Sqrt(-2 * height / gravity);
        float timeDown = Mathf.Sqrt(2 * (displacement.y - height) / gravity);
        float totalTime = timeUp + timeDown;

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * height);
        Vector3 velocityXZ = displacementXZ / totalTime;

        return velocityXZ + velocityY;
    }
}