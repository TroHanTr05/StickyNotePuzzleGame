// ─────────────────────────────────────────────────────────────────────────────
//  ItemPickup.cs
//
//  CHANGES FROM ORIGINAL:
//    • InventoryModel.Instance.Add(...) replaced with a resolved
//      IInventoryModel from ServiceResolver. ItemPickup now depends on the
//      interface, not the concrete class, and is consistent with how every
//      other runtime object accesses services.
//    • Added Game.Runtime namespace.
//    • Debug implicit calls replaced with IGameLog.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using UnityEngine;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    public class ItemPickup : MonoBehaviour
    {
        private static IGameLog         Log       => ServiceResolver.Resolve<IGameLog>();
        private static IInventoryModel  Inventory => ServiceResolver.Resolve<IInventoryModel>();

        [Tooltip("Tag stored in the inventory when picked up. " +
                 "Use the same string in DialogueInteractable ConversationSet RequiredItems.")]
        public string ItemTag = "Key";

        [Tooltip("Pickup radius used by the distance check (world units).")]
        public float PickupRadius = 0.6f;

        [Tooltip("Tags that count as 'player' for the physics-callback fallback.")]
        public string[] PlayerTags = { "Player" };

        [Tooltip("Destroy this GameObject after pickup?")]
        public bool DestroyOnPickup = true;

        [Tooltip("Optional particle / sound prefab spawned at pickup position.")]
        public GameObject PickupFX;

        public static event Action<string> OnPickedUp;

        SnakeController _snake;
        bool _collected;

        void Start()
        {
            _snake = FindFirstObjectByType<SnakeController>();
        }

        void Update()
        {
            if (_collected) return;

            if (_snake == null)
            {
                _snake = FindFirstObjectByType<SnakeController>();
                return;
            }

            if (_snake.Head != null &&
                Vector3.Distance(transform.position, _snake.Head.transform.position) <= PickupRadius)
            {
                Collect();
                return;
            }

            foreach (var seg in _snake.Segments)
            {
                if (seg == null) continue;
                if (Vector3.Distance(transform.position, seg.transform.position) <= PickupRadius)
                { Collect(); return; }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_collected && IsPlayer(other.gameObject)) Collect();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_collected && IsPlayer(other.gameObject)) Collect();
        }

        bool IsPlayer(GameObject go)
        {
            foreach (var t in PlayerTags)
                if (go.CompareTag(t)) return true;
            return false;
        }

        void Collect()
        {
            if (_collected) return;
            _collected = true;

            // Resolve inventory through the container — no static type reference
            Inventory.Add(ItemTag);
            OnPickedUp?.Invoke(ItemTag);
            Log.Info($"[ItemPickup] Collected: {ItemTag}");

            if (PickupFX)
                Instantiate(PickupFX, transform.position, Quaternion.identity);

            if (DestroyOnPickup)
                Destroy(gameObject);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.3f);
            Gizmos.DrawSphere(transform.position, PickupRadius);
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, PickupRadius);
        }
#endif
    }
}
