// ─────────────────────────────────────────────────────────────────────────────
//  IInventoryModel.cs
//
//  Pure C# interface — zero Unity dependencies.
//  Registered in ServiceResolver so any class can resolve it without
//  taking a hard dependency on the concrete InventoryModel type.
//
//  MVVM role: MODEL (interface)
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace Game.Runtime
{
    public interface IInventoryModel
    {
        /// <summary>Fired whenever an item is added. Payload = item tag.</summary>
        event Action<string> OnItemAdded;

        /// <summary>Fired whenever an item is removed. Payload = item tag.</summary>
        event Action<string> OnItemRemoved;

        /// <summary>Fired after any change. Useful for a generic "refresh UI" hook.</summary>
        event Action OnInventoryChanged;

        /// <summary>Add an item by tag. Ignored if already present.</summary>
        bool Add(string itemTag);

        /// <summary>Remove an item by tag. Returns false if it wasn't present.</summary>
        bool Remove(string itemTag);

        /// <summary>Check whether the player is carrying a specific item.</summary>
        bool Has(string itemTag);

        /// <summary>Check whether the player carries ALL of the listed items.</summary>
        bool HasAll(params string[] tags);

        /// <summary>Check whether the player carries ANY of the listed items.</summary>
        bool HasAny(params string[] tags);

        /// <summary>Read-only snapshot of everything in the inventory.</summary>
        IReadOnlyCollection<string> All { get; }

        /// <summary>Clear everything (e.g. on scene reload).</summary>
        void Clear();
    }
}
