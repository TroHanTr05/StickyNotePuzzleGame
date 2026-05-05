// ─────────────────────────────────────────────────────────────────────────────
//  InventoryModel.cs
//
//  Pure C# — zero Unity dependencies. Works inside Unity and in a plain
//  .NET unit-test project.
//
//  CHANGES FROM ORIGINAL:
//    • Removed static Instance singleton — lifetime is now managed by
//      ServiceResolver, which registers it as a singleton and hands it out
//      via IInventoryModel. This lets tests create fresh instances freely,
//      and lets the DI container own the object graph.
//    • Implements IInventoryModel so callers depend on the interface, not
//      the concrete type.
//
//  MVVM role: MODEL
//    • Owns the authoritative list of collected item tags.
//    • Fires events that ViewModels / Views subscribe to.
//    • Never touches MonoBehaviour, Transform, or any Unity type.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Game.Runtime
{
    public class InventoryModel : IInventoryModel
    {
        // ── State ────────────────────────────────────────────────────────────
        private readonly HashSet<string> _items = new(StringComparer.OrdinalIgnoreCase);

        // ── Events (ViewModel / View binds here) ─────────────────────────────
        /// <summary>Fired whenever an item is added. Payload = item tag.</summary>
        public event Action<string> OnItemAdded;

        /// <summary>Fired whenever an item is removed. Payload = item tag.</summary>
        public event Action<string> OnItemRemoved;

        /// <summary>Fired after any change. Useful for a generic "refresh UI" hook.</summary>
        public event Action OnInventoryChanged;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Add an item by tag. Ignored if already present.</summary>
        public bool Add(string itemTag)
        {
            if (string.IsNullOrWhiteSpace(itemTag)) return false;
            if (!_items.Add(itemTag)) return false;

            OnItemAdded?.Invoke(itemTag);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>Remove an item by tag. Returns false if it wasn't present.</summary>
        public bool Remove(string itemTag)
        {
            if (!_items.Remove(itemTag)) return false;

            OnItemRemoved?.Invoke(itemTag);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>Check whether the player is carrying a specific item.</summary>
        public bool Has(string itemTag) => _items.Contains(itemTag);

        /// <summary>Check whether the player carries ALL of the listed items.</summary>
        public bool HasAll(params string[] tags)
        {
            foreach (var t in tags)
                if (!_items.Contains(t)) return false;
            return true;
        }

        /// <summary>Check whether the player carries ANY of the listed items.</summary>
        public bool HasAny(params string[] tags)
        {
            foreach (var t in tags)
                if (_items.Contains(t)) return true;
            return false;
        }

        /// <summary>Read-only snapshot of everything in the inventory.</summary>
        public IReadOnlyCollection<string> All => _items;

        /// <summary>Clear everything (e.g. on scene reload).</summary>
        public void Clear()
        {
            _items.Clear();
            OnInventoryChanged?.Invoke();
        }
    }
}
