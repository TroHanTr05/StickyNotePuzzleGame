using UnityEngine;

//public class SimplePowerUp : MonoBehaviour
//{
//    public bool givesBalls  = false;
//    public bool givesGrapple = false;
//    public bool givesGlide  = false;
//
//    public SnakeController snakeController;
//
//    private void OnCollisionEnter(Collision collision)
//    {
//        if (!collision.gameObject.CompareTag("Player")) return;
//
//        if (snakeController == null)
//            snakeController = collision.gameObject.GetComponentInParent<SnakeController>();
//
//        if (snakeController == null) return;
//
//        if (givesBalls)   snakeController.HasBalls   = true;
//        if (givesGrapple) snakeController.HasGrapple  = true;
//        if (givesGlide)   snakeController.HasGlide    = true;
//
//        Destroy(gameObject);
//    }
//}
