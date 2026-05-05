// ─────────────────────────────────────────────────────────────────────────────
//  Tests.cs
//
//  Pure C# unit tests — NO Unity dependency.
//  Run with any standard test runner:
//    • dotnet test  (create a .NET console / xUnit / NUnit project,
//                    add InventoryModel.cs + WallHealthModel from WallHealth.cs)
//    • Unity Test Runner (place this file in an Editor/Tests folder and add
//      the NUnit attribute "using NUnit.Framework" — the tests are identical)
//
//  The classes under test (InventoryModel, WallHealthModel) contain zero
//  Unity types so they compile and run in a plain .NET environment.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

// ── Minimal NUnit-compatible stubs so the file compiles both in Unity's
//    NUnit environment and in a plain .NET project that has NUnit installed ──

#if !UNITY_EDITOR && !UNITY_5_3_OR_NEWER
// When running outside Unity, reference NUnit.Framework via NuGet
using NUnit.Framework;
#else
using NUnit.Framework;
#endif

// ══════════════════════════════════════════════════════════════════════════════
//  INVENTORY MODEL TESTS
// ══════════════════════════════════════════════════════════════════════════════

[TestFixture]
public class InventoryModelTests
{
    InventoryModel _inv;

    [SetUp]
    public void SetUp()
    {
        // Create a fresh instance for each test — don't use the singleton
        // so tests are fully isolated.
        _inv = new InventoryModel();
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Test]
    public void Add_NewItem_ReturnsTrue()
    {
        bool result = _inv.Add("Key");
        Assert.IsTrue(result);
    }

    [Test]
    public void Add_DuplicateItem_ReturnsFalse()
    {
        _inv.Add("Key");
        bool result = _inv.Add("Key");
        Assert.IsFalse(result);
    }

    [Test]
    public void Add_EmptyTag_ReturnsFalse()
    {
        bool result = _inv.Add("");
        Assert.IsFalse(result);
    }

    [Test]
    public void Add_NullTag_ReturnsFalse()
    {
        bool result = _inv.Add(null);
        Assert.IsFalse(result);
    }

    [Test]
    public void Add_TagIsCaseInsensitive()
    {
        _inv.Add("KEY");
        bool result = _inv.Add("key");      // should be treated as duplicate
        Assert.IsFalse(result);
    }

    [Test]
    public void Add_FiresOnItemAddedEvent()
    {
        string received = null;
        _inv.OnItemAdded += tag => received = tag;

        _inv.Add("Sword");

        Assert.AreEqual("Sword", received);
    }

    [Test]
    public void Add_DuplicateDoesNotFireEvent()
    {
        int callCount = 0;
        _inv.OnItemAdded += _ => callCount++;

        _inv.Add("Shield");
        _inv.Add("Shield");

        Assert.AreEqual(1, callCount);
    }

    // ── Has ───────────────────────────────────────────────────────────────────

    [Test]
    public void Has_AfterAdd_ReturnsTrue()
    {
        _inv.Add("Key");
        Assert.IsTrue(_inv.Has("Key"));
    }

    [Test]
    public void Has_WhenAbsent_ReturnsFalse()
    {
        Assert.IsFalse(_inv.Has("Key"));
    }

    [Test]
    public void Has_CaseInsensitive()
    {
        _inv.Add("key");
        Assert.IsTrue(_inv.Has("KEY"));
    }

    // ── HasAll ────────────────────────────────────────────────────────────────

    [Test]
    public void HasAll_AllPresent_ReturnsTrue()
    {
        _inv.Add("Key"); _inv.Add("Map"); _inv.Add("Torch");
        Assert.IsTrue(_inv.HasAll("Key", "Map", "Torch"));
    }

    [Test]
    public void HasAll_OneMissing_ReturnsFalse()
    {
        _inv.Add("Key"); _inv.Add("Map");
        Assert.IsFalse(_inv.HasAll("Key", "Map", "Torch"));
    }

    [Test]
    public void HasAll_Empty_ReturnsTrue()
    {
        // Vacuously true — no requirements
        Assert.IsTrue(_inv.HasAll());
    }

    // ── HasAny ────────────────────────────────────────────────────────────────

    [Test]
    public void HasAny_OnePresent_ReturnsTrue()
    {
        _inv.Add("Key");
        Assert.IsTrue(_inv.HasAny("Key", "Map"));
    }

    [Test]
    public void HasAny_NonePresent_ReturnsFalse()
    {
        Assert.IsFalse(_inv.HasAny("Key", "Map"));
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Test]
    public void Remove_ExistingItem_ReturnsTrue()
    {
        _inv.Add("Key");
        Assert.IsTrue(_inv.Remove("Key"));
    }

    [Test]
    public void Remove_AbsentItem_ReturnsFalse()
    {
        Assert.IsFalse(_inv.Remove("Key"));
    }

    [Test]
    public void Remove_ItemNoLongerInInventory()
    {
        _inv.Add("Key");
        _inv.Remove("Key");
        Assert.IsFalse(_inv.Has("Key"));
    }

    [Test]
    public void Remove_FiresOnItemRemovedEvent()
    {
        string received = null;
        _inv.OnItemRemoved += tag => received = tag;

        _inv.Add("Key");
        _inv.Remove("Key");

        Assert.AreEqual("Key", received);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Test]
    public void Clear_EmptiesInventory()
    {
        _inv.Add("Key"); _inv.Add("Map");
        _inv.Clear();
        Assert.AreEqual(0, _inv.All.Count);
    }

    [Test]
    public void Clear_FiresOnInventoryChangedEvent()
    {
        int callCount = 0;
        _inv.OnInventoryChanged += () => callCount++;

        _inv.Clear();

        Assert.AreEqual(1, callCount);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  WALL HEALTH MODEL TESTS
// ══════════════════════════════════════════════════════════════════════════════

[TestFixture]
public class WallHealthModelTests
{
    // ── Constructor ───────────────────────────────────────────────────────────

    [Test]
    public void Constructor_SetsMaxAndCurrentHealth()
    {
        var model = new WallHealthModel(5);
        Assert.AreEqual(5, model.MaxHealth);
        Assert.AreEqual(5, model.CurrentHealth);
    }

    [Test]
    public void Constructor_ZeroMaxHealth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WallHealthModel(0));
    }

    [Test]
    public void Constructor_NegativeMaxHealth_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WallHealthModel(-1));
    }

    // ── TakeDamage ────────────────────────────────────────────────────────────

    [Test]
    public void TakeDamage_ReducesHealth()
    {
        var model = new WallHealthModel(10);
        model.TakeDamage(3);
        Assert.AreEqual(7, model.CurrentHealth);
    }

    [Test]
    public void TakeDamage_ReturnsDamageDealt()
    {
        var model = new WallHealthModel(5);
        int dealt = model.TakeDamage(3);
        Assert.AreEqual(3, dealt);
    }

    [Test]
    public void TakeDamage_ClampsToZero()
    {
        var model = new WallHealthModel(5);
        model.TakeDamage(100);
        Assert.AreEqual(0, model.CurrentHealth);
    }

    [Test]
    public void TakeDamage_ReturnsCappedDamage_WhenOverkill()
    {
        var model = new WallHealthModel(5);
        int dealt = model.TakeDamage(100);
        Assert.AreEqual(5, dealt);         // can't deal more than remaining hp
    }

    [Test]
    public void TakeDamage_WhenZeroHealth_ReturnsZero()
    {
        var model = new WallHealthModel(5);
        model.TakeDamage(5);               // destroy
        int dealt = model.TakeDamage(1);   // already dead
        Assert.AreEqual(0, dealt);
    }

    [Test]
    public void TakeDamage_FiresOnHealthChangedEvent()
    {
        var model = new WallHealthModel(10);
        int firedCurrent = -1;
        model.OnHealthChanged += (c, _) => firedCurrent = c;

        model.TakeDamage(4);
        Assert.AreEqual(6, firedCurrent);
    }

    [Test]
    public void TakeDamage_ToZero_FiresOnDestroyedEvent()
    {
        var model = new WallHealthModel(3);
        bool fired = false;
        model.OnDestroyed += () => fired = true;

        model.TakeDamage(3);
        Assert.IsTrue(fired);
    }

    [Test]
    public void TakeDamage_BelowZero_OnlyFiresDestroyedOnce()
    {
        var model = new WallHealthModel(3);
        int count = 0;
        model.OnDestroyed += () => count++;

        model.TakeDamage(10);
        model.TakeDamage(10);   // already destroyed
        Assert.AreEqual(1, count);
    }

    // ── IsDestroyed ───────────────────────────────────────────────────────────

    [Test]
    public void IsDestroyed_WhenHealthAboveZero_ReturnsFalse()
    {
        var model = new WallHealthModel(5);
        Assert.IsFalse(model.IsDestroyed);
    }

    [Test]
    public void IsDestroyed_WhenHealthReachesZero_ReturnsTrue()
    {
        var model = new WallHealthModel(5);
        model.TakeDamage(5);
        Assert.IsTrue(model.IsDestroyed);
    }

    // ── NormalisedHealth ──────────────────────────────────────────────────────

    [Test]
    public void NormalisedHealth_FullHealth_IsOne()
    {
        var model = new WallHealthModel(10);
        Assert.AreEqual(1f, model.NormalisedHealth, 0.001f);
    }

    [Test]
    public void NormalisedHealth_HalfHealth_IsPointFive()
    {
        var model = new WallHealthModel(10);
        model.TakeDamage(5);
        Assert.AreEqual(0.5f, model.NormalisedHealth, 0.001f);
    }

    [Test]
    public void NormalisedHealth_ZeroHealth_IsZero()
    {
        var model = new WallHealthModel(10);
        model.TakeDamage(10);
        Assert.AreEqual(0f, model.NormalisedHealth, 0.001f);
    }

    // ── Heal ──────────────────────────────────────────────────────────────────

    [Test]
    public void Heal_IncreasesHealth()
    {
        var model = new WallHealthModel(10);
        model.TakeDamage(6);
        model.Heal(3);
        Assert.AreEqual(7, model.CurrentHealth);
    }

    [Test]
    public void Heal_ClampsToMax()
    {
        var model = new WallHealthModel(10);
        model.TakeDamage(2);
        model.Heal(100);
        Assert.AreEqual(10, model.CurrentHealth);
    }

    [Test]
    public void Heal_WhenDestroyed_ReturnsZero()
    {
        var model = new WallHealthModel(5);
        model.TakeDamage(5);
        int restored = model.Heal(5);
        Assert.AreEqual(0, restored);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Test]
    public void Reset_RestoresFullHealth()
    {
        var model = new WallHealthModel(10);
        model.TakeDamage(7);
        model.Reset();
        Assert.AreEqual(10, model.CurrentHealth);
    }

    // ── ConversationSet condition logic (pure C# simulation) ─────────────────
    // These mirror what DialogueInteractable.ConversationSet.IsUnlocked() does,
    // using InventoryModel directly so we can test the quest logic without Unity.

    [Test]
    public void QuestLogic_NoKeyYet_DefaultSetSelected()
    {
        var inv = new InventoryModel();
        // Simulate two sets: "has key" set and fallback set
        bool hasKeySetUnlocked  = inv.Has("Key");    // requires Key
        bool fallbackSetUnlocked = true;             // no requirements

        string activeSet = hasKeySetUnlocked ? "ReturnWithKey" : "AskForKey";
        Assert.AreEqual("AskForKey", activeSet);
    }

    [Test]
    public void QuestLogic_AfterPickingUpKey_KeySetSelected()
    {
        var inv = new InventoryModel();
        inv.Add("Key");

        bool hasKeySetUnlocked = inv.Has("Key");
        string activeSet = hasKeySetUnlocked ? "ReturnWithKey" : "AskForKey";
        Assert.AreEqual("ReturnWithKey", activeSet);
    }
}
