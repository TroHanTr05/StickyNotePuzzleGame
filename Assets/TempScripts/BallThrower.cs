using UnityEngine;

public class BallThrower : MonoBehaviour
{
    public GameObject ballPrefab;
    public Transform throwPoint;
    public float arcHeight = 5f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Throw();
        }
    }

    void Throw()
    {
        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

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
        
        Vector3 velocity = CalculateArcVelocity(throwPoint.position, targetPoint, arcHeight);
        
        rb.linearVelocity = velocity;
    }

    Vector3 CalculateArcVelocity(Vector3 start, Vector3 end, float height)
    {
        float gravity = Physics.gravity.y;
        
        Vector3 displacement = end -  start;
        Vector3 displacementXZ = new Vector3(displacement.x, 0, displacement.z);
        
        float timeUp = Mathf.Sqrt(-2 * height /  gravity);
        float timeDown = Mathf.Sqrt(2 * (displacement.y - height) /  gravity);
        float totalTime = timeUp + timeDown;

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * height);
        Vector3 velocityXZ = displacementXZ /  totalTime;
        return velocityXZ + velocityY;
    }
}
