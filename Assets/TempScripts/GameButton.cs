// ─────────────────────────────────────────────────────────────────────────────
//  GameButton.cs
//
//  CHANGES FROM ORIGINAL:
//    • Added Game.Runtime namespace.
//    • IGameLog resolved from ServiceResolver — replaces bare Debug.Log TODO comment.
//    • No logic changes.
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    public enum ButtonMoveDirection { Up, Down, Left, Right }

    public class GameButton : MonoBehaviour
    {
        private static IGameLog Log => ServiceResolver.Resolve<IGameLog>();

        [Header("Activation")]
        public bool activateOnPlayer = true;
        public bool activateOnBall = true;
        public bool oneShot = false;

        [Header("Action — Toggle Object")]
        public bool toggleObject = false;
        public GameObject targetObject;
        public bool showObject = true;

        [Header("Action — Move Object")]
        public bool moveObject = false;
        public GameObject moveTarget;
        public ButtonMoveDirection moveDirection = ButtonMoveDirection.Up;
        public float moveDistance = 3f;
        public float moveSpeed = 5f;

        [Header("Action — End Game")]
        public bool endGame = false;

        private bool _triggered = false;
        private bool _isMoving = false;
        private Vector3 _moveStart;
        private Vector3 _moveEnd;
        private float _moveT;

        private void OnCollisionEnter(Collision collision) => HandleContact(collision.gameObject);
        private void OnTriggerEnter(Collider other)        => HandleContact(other.gameObject);

        void HandleContact(GameObject other)
        {
            if (oneShot && _triggered) return;

            bool isPlayer = activateOnPlayer && other.CompareTag("Player");
            bool isBall   = activateOnBall   && other.CompareTag("Ball");

            if (!isPlayer && !isBall) return;

            _triggered = true;
            Execute();
        }

        void Execute()
        {
            if (toggleObject && targetObject != null)
                targetObject.SetActive(showObject);

            if (moveObject && moveTarget != null && !_isMoving)
            {
                _moveStart = moveTarget.transform.position;
                _moveEnd   = _moveStart + GetMoveVector() * moveDistance;
                _moveT     = 0f;
                _isMoving  = true;
                Log.Info($"[GameButton] Moving {moveTarget.name} {moveDirection} by {moveDistance}u");
            }

            if (endGame)
            {
                Log.Info("[GameButton] End game triggered.");
                // TODO: end game logic
            }
        }

        Vector3 GetMoveVector() => moveDirection switch
        {
            ButtonMoveDirection.Up    => Vector3.up,
            ButtonMoveDirection.Down  => Vector3.down,
            ButtonMoveDirection.Left  => Vector3.left,
            ButtonMoveDirection.Right => Vector3.right,
            _                         => Vector3.up,
        };

        void Update()
        {
            if (!_isMoving || moveTarget == null) return;

            _moveT += Time.deltaTime * moveSpeed / Mathf.Max(moveDistance, 0.01f);
            _moveT  = Mathf.Clamp01(_moveT);
            moveTarget.transform.position = Vector3.Lerp(_moveStart, _moveEnd, _moveT);

            if (_moveT >= 1f) _isMoving = false;
        }
    }
}
