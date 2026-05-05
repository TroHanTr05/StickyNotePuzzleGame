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
//  CHANGES FROM ORIGINAL:
//    • Added "using Game.Runtime" so InventoryModel and WallHealthModel
//      resolve from the correct namespace.
//    • No logic changes — the tests already used "new InventoryModel()" which
//      is exactly right. Tests must never go through the DI container; they
//      construct their own isolated instances so each test is independent.
//      This is intentional and correct — do not change it to use ServiceResolver.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Game.Runtime;

#if !UNITY_EDITOR && !UNITY_5_3_OR_NEWER
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
        // Always create a fresh instance — never ServiceResolver.Resolve.
        // Tests must be isolated; the DI container is for runtime, not tests.
        _inv = new InventoryModel();
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Test] public void Add_NewItem_ReturnsTrue()           { Assert.IsTrue(_inv.Add("Key")); }

    [Test] public void Add_DuplicateItem_ReturnsFalse()    { _inv.Add("Key"); Assert.IsFalse(_inv.Add("Key")); }

    [Test] public void Add_EmptyTag_ReturnsFalse()         { Assert.IsFalse(_inv.Add("")); }

    [Test] public void Add_NullTag_ReturnsFalse()          { Assert.IsFalse(_inv.Add(null)); }

    [Test]
    public void Add_TagIsCaseInsensitive()
    {
        _inv.Add("KEY");
        Assert.IsFalse(_inv.Add("key"));
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

    [Test] public void Has_AfterAdd_ReturnsTrue()   { _inv.Add("Key"); Assert.IsTrue(_inv.Has("Key")); }
    [Test] public void Has_WhenAbsent_ReturnsFalse() { Assert.IsFalse(_inv.Has("Key")); }
    [Test] public void Has_CaseInsensitive()        { _inv.Add("key"); Assert.IsTrue(_inv.Has("KEY")); }

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

    [Test] public void HasAll_Empty_ReturnsTrue() { Assert.IsTrue(_inv.HasAll()); }

    // ── HasAny ────────────────────────────────────────────────────────────────

    [Test]
    public void HasAny_OnePresent_ReturnsTrue()
    {
        _inv.Add("Key");
        Assert.IsTrue(_inv.HasAny("Key", "Map"));
    }

    [Test] public void HasAny_NonePresent_ReturnsFalse() { Assert.IsFalse(_inv.HasAny("Key", "Map")); }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Test] public void Remove_ExistingItem_ReturnsTrue()  { _inv.Add("Key"); Assert.IsTrue(_inv.Remove("Key")); }
    [Test] public void Remove_AbsentItem_ReturnsFalse()   { Assert.IsFalse(_inv.Remove("Key")); }

    [Test]
    public void Remove_ItemNoLongerInInventory()
    {
        _inv.Add("Key"); _inv.Remove("Key");
        Assert.IsFalse(_inv.Has("Key"));
    }

    [Test]
    public void Remove_FiresOnItemRemovedEvent()
    {
        string received = null;
        _inv.OnItemRemoved += tag => received = tag;
        _inv.Add("Key"); _inv.Remove("Key");
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
    [Test]
    public void Constructor_SetsMaxAndCurrentHealth()
    {
        var model = new WallHealthModel(5);
        Assert.AreEqual(5, model.MaxHealth);
        Assert.AreEqual(5, model.CurrentHealth);
    }

    [Test] public void Constructor_ZeroMaxHealth_Throws()     { Assert.Throws<ArgumentOutOfRangeException>(() => new WallHealthModel(0)); }
    [Test] public void Constructor_NegativeMaxHealth_Throws() { Assert.Throws<ArgumentOutOfRangeException>(() => new WallHealthModel(-1)); }

    [Test] public void TakeDamage_ReducesHealth()       { var m = new WallHealthModel(10); m.TakeDamage(3); Assert.AreEqual(7, m.CurrentHealth); }
    [Test] public void TakeDamage_ReturnsDamageDealt()  { var m = new WallHealthModel(5);  Assert.AreEqual(3, m.TakeDamage(3)); }
    [Test] public void TakeDamage_ClampsToZero()        { var m = new WallHealthModel(5);  m.TakeDamage(100); Assert.AreEqual(0, m.CurrentHealth); }
    [Test] public void TakeDamage_ReturnsCappedDamage_WhenOverkill() { var m = new WallHealthModel(5); Assert.AreEqual(5, m.TakeDamage(100)); }

    [Test]
    public void TakeDamage_WhenZeroHealth_ReturnsZero()
    {
        var m = new WallHealthModel(5);
        m.TakeDamage(5);
        Assert.AreEqual(0, m.TakeDamage(1));
    }

    [Test]
    public void TakeDamage_FiresOnHealthChangedEvent()
    {
        var m = new WallHealthModel(10);
        int firedCurrent = -1;
        m.OnHealthChanged += (c, _) => firedCurrent = c;
        m.TakeDamage(4);
        Assert.AreEqual(6, firedCurrent);
    }

    [Test]
    public void TakeDamage_ToZero_FiresOnDestroyedEvent()
    {
        var m = new WallHealthModel(3);
        bool fired = false;
        m.OnDestroyed += () => fired = true;
        m.TakeDamage(3);
        Assert.IsTrue(fired);
    }

    [Test]
    public void TakeDamage_BelowZero_OnlyFiresDestroyedOnce()
    {
        var m = new WallHealthModel(3);
        int count = 0;
        m.OnDestroyed += () => count++;
        m.TakeDamage(10);
        m.TakeDamage(10);
        Assert.AreEqual(1, count);
    }

    [Test] public void IsDestroyed_WhenHealthAboveZero_ReturnsFalse() { Assert.IsFalse(new WallHealthModel(5).IsDestroyed); }
    [Test] public void IsDestroyed_WhenHealthReachesZero_ReturnsTrue() { var m = new WallHealthModel(5); m.TakeDamage(5); Assert.IsTrue(m.IsDestroyed); }

    [Test] public void NormalisedHealth_FullHealth_IsOne()       { Assert.AreEqual(1f, new WallHealthModel(10).NormalisedHealth, 0.001f); }
    [Test] public void NormalisedHealth_HalfHealth_IsPointFive() { var m = new WallHealthModel(10); m.TakeDamage(5); Assert.AreEqual(0.5f, m.NormalisedHealth, 0.001f); }
    [Test] public void NormalisedHealth_ZeroHealth_IsZero()      { var m = new WallHealthModel(10); m.TakeDamage(10); Assert.AreEqual(0f, m.NormalisedHealth, 0.001f); }

    [Test]
    public void Heal_IncreasesHealth()
    {
        var m = new WallHealthModel(10);
        m.TakeDamage(6); m.Heal(3);
        Assert.AreEqual(7, m.CurrentHealth);
    }

    [Test] public void Heal_ClampsToMax()       { var m = new WallHealthModel(10); m.TakeDamage(2); m.Heal(100); Assert.AreEqual(10, m.CurrentHealth); }
    [Test] public void Heal_WhenDestroyed_ReturnsZero() { var m = new WallHealthModel(5); m.TakeDamage(5); Assert.AreEqual(0, m.Heal(5)); }

    [Test]
    public void Reset_RestoresFullHealth()
    {
        var m = new WallHealthModel(10);
        m.TakeDamage(7); m.Reset();
        Assert.AreEqual(10, m.CurrentHealth);
    }

    // ── Quest logic (simulates ConversationSet.IsUnlocked without Unity) ─────

    [Test]
    public void QuestLogic_NoKeyYet_DefaultSetSelected()
    {
        var inv = new InventoryModel();
        bool hasKeySetUnlocked   = inv.Has("Key");
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
