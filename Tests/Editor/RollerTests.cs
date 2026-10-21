using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CascadeRoll.Tests {
  /// <summary>
  /// Drop history the test modifiers read: how many times each item and each tag has dropped.
  /// </summary>
  public class Ledger {
    public readonly Dictionary<string, int> Dropped = new Dictionary<string, int>();
    public float Progress;

    public int Count(string key) {
      return key != null && Dropped.TryGetValue(key, out int n) ? n : 0;
    }

    public void Record(string item, string tag) {
      Dropped[item] = Count(item) + 1;
      if (tag != null) {
        Dropped[tag] = Count(tag) + 1;
      }
    }
  }

  /// <summary>
  /// Halves an entry's weight per time it (or its tag) already dropped.
  /// </summary>
  public class Falloff : IWeightModifier<string, Ledger> {
    public float Modify(RollEntry<string> entry, Ledger ctx, IReadOnlyList<string> haul, float weight) {
      int dropped = entry.IsLeaf ? ctx.Count(entry.Item) : ctx.Count(entry.Tag);
      return weight / (1 + dropped);
    }
  }

  /// <summary>
  /// Excludes every entry tagged "Locked".
  /// </summary>
  public class LockGate : IWeightModifier<string, Ledger> {
    public float Modify(RollEntry<string> entry, Ledger ctx, IReadOnlyList<string> haul, float weight) {
      return entry.Tag == "Locked" ? 0f : weight;
    }
  }

  /// <summary>
  /// Forces a Telescope while nothing has dropped yet.
  /// </summary>
  public class FirstOpenTelescope : IRollRule<string, Ledger> {
    public IEnumerable<string> Guaranteed(Ledger ctx) {
      return ctx.Dropped.Count == 0 ? new[] { "Telescope" } : null;
    }
  }

  /// <summary>
  /// Excludes any item already in this haul.
  /// </summary>
  public class NoRepeatInHaul : IWeightModifier<string, Ledger> {
    public float Modify(RollEntry<string> entry, Ledger ctx, IReadOnlyList<string> haul, float weight) {
      return entry.IsLeaf && haul.Contains(entry.Item) ? 0f : weight;
    }
  }

  /// <summary>
  /// Concrete asset type the Unity-layer test authors in memory.
  /// </summary>
  public class TextTable : RollTableAsset<TextAsset> {
  }

  public class RollerTests {
    private static RollTable<string> Tree() {
      var consumable = new RollTable<string>("Consumable").Add("Tuna", 3f).Add("Bandage", 1f);
      var weapon = new RollTable<string>("Weapon").Add("Spear", 10f).Add("Bow", 3f);
      var coins = new RollTable<string>("CoinPile", 3).Add("Coin", 1f);
      return new RollTable<string>("Root", 2).Add(consumable, 6f, "Consumable").Add(weapon, 3f, "Weapon").Add(coins, 1f, "Coin");
    }

    [Test]
    public void SameSeedSameHaul() {
      var roller = new Roller<string, Ledger>(new[] { new Falloff() });
      RollResult<string> a = roller.Roll(Tree(), new Ledger(), new SystemRandom(42));
      RollResult<string> b = roller.Roll(Tree(), new Ledger(), new SystemRandom(42));
      RollResult<string> c = roller.Roll(Tree(), new Ledger(), new SystemRandom(43));
      CollectionAssert.AreEqual(a.Items, b.Items);
      bool anyDiffers = false;
      for (int seed = 43; seed < 60 && !anyDiffers; seed++) {
        anyDiffers = !new HashSet<string>(roller.Roll(Tree(), new Ledger(), new SystemRandom(seed)).Items).SetEquals(a.Items);
      }
      Assert.IsTrue(anyDiffers, "every seed dealt the same haul");
      Assert.AreEqual(a.Items.Count, a.Trace.Count > 0 ? a.Items.Count : -1);
    }

    [Test]
    public void RootPicksCountsItems_CoinPileDealsThree() {
      var roller = new Roller<string, Ledger>();
      var root = new RollTable<string>("Root", 1).Add(new RollTable<string>("CoinPile", 3).Add("Coin", 1f), 1f);
      RollResult<string> r = roller.Roll(root, new Ledger(), new SystemRandom(1));
      Assert.AreEqual(3, r.Items.Count);
      Assert.IsTrue(r.Items.TrueForAll(i => i == "Coin"));
    }

    [Test]
    public void ZeroWeightIsNeverPicked() {
      var roller = new Roller<string, Ledger>(new[] { new LockGate() });
      var root = new RollTable<string>("Root", 1).Add("Open", 1f).Add("Locked", 1000f, "Locked");
      for (int seed = 0; seed < 200; seed++) {
        Assert.AreEqual("Open", roller.Roll(root, new Ledger(), new SystemRandom(seed)).Items[0]);
      }
    }

    [Test]
    public void DeadBranchFallsBackToSibling() {
      var roller = new Roller<string, Ledger>(new[] { new LockGate() });
      var dead = new RollTable<string>("Dead").Add("Never", 1f, "Locked");
      var root = new RollTable<string>("Root", 1).Add(dead, 1000f).Add("Fallback", 1f);
      for (int seed = 0; seed < 50; seed++) {
        RollResult<string> r = roller.Roll(root, new Ledger(), new SystemRandom(seed));
        Assert.AreEqual(1, r.Items.Count, r.ToString());
        Assert.AreEqual("Fallback", r.Items[0]);
      }
    }

    [Test]
    public void EverythingZeroDealsNothing() {
      var roller = new Roller<string, Ledger>(new[] { new LockGate() });
      var root = new RollTable<string>("Root", 2).Add("A", 1f, "Locked").Add("B", 1f, "Locked");
      RollResult<string> r = roller.Roll(root, new Ledger(), new SystemRandom(7));
      Assert.AreEqual(0, r.Items.Count);
      Assert.AreEqual(2, r.Trace.Count);
    }

    [Test]
    public void GuaranteeCountsTowardPicks() {
      var roller = new Roller<string, Ledger>(rules: new[] { new FirstOpenTelescope() });
      RollResult<string> first = roller.Roll(Tree(), new Ledger(), new SystemRandom(3));
      Assert.AreEqual("Telescope", first.Items[0]);
      Assert.GreaterOrEqual(first.Items.Count, 2);
      var ledger = new Ledger();
      ledger.Record("Tuna", "Consumable");
      RollResult<string> later = roller.Roll(Tree(), ledger, new SystemRandom(3));
      CollectionAssert.DoesNotContain(later.Items, "Telescope");
    }

    [Test]
    public void FalloffShiftsShareTowardUndropped() {
      var roller = new Roller<string, Ledger>(new[] { new Falloff() });
      var root = new RollTable<string>("Root", 1).Add("Spear", 10f).Add("Bow", 3f);
      Assert.AreEqual(10f / 13f, Share(roller, root, new Ledger(), "Spear"), 0.03f);
      var ledger = new Ledger();
      ledger.Record("Spear", null);
      Assert.AreEqual(5f / 8f, Share(roller, root, ledger, "Spear"), 0.03f);
      ledger.Record("Spear", null);
      Assert.AreEqual(10f / 3f / (10f / 3f + 3f), Share(roller, root, ledger, "Spear"), 0.03f);
    }

    [Test]
    public void TagFalloffBalancesTypes() {
      var roller = new Roller<string, Ledger>(new[] { new Falloff() });
      var ledger = new Ledger();
      for (int i = 0; i < 4; i++) {
        ledger.Record("Tuna", "Consumable");
      }
      // Consumable 6/(1+4)=1.2 vs Weapon 3 vs Coin 1: weapons now lead.
      int weapons = 0;
      for (int seed = 0; seed < 2000; seed++) {
        RollResult<string> r = roller.Roll(new RollTable<string>("Root", 1).Add(Tree().Entries[0].Table, 6f, "Consumable").Add(Tree().Entries[1].Table, 3f, "Weapon").Add("Coin", 1f, "Coin"), ledger, new SystemRandom(seed));
        if (r.Items[0] == "Spear" || r.Items[0] == "Bow") {
          weapons++;
        }
      }
      Assert.AreEqual(3f / 5.2f, weapons / 2000f, 0.04f);
    }

    [Test]
    public void ModifierSeesHaulSoFar() {
      var roller = new Roller<string, Ledger>(new[] { new NoRepeatInHaul() });
      var root = new RollTable<string>("Root", 3).Add("A", 100f).Add("B", 1f).Add("C", 1f);
      for (int seed = 0; seed < 50; seed++) {
        RollResult<string> r = roller.Roll(root, new Ledger(), new SystemRandom(seed));
        CollectionAssert.AllItemsAreUnique(r.Items, r.ToString());
        Assert.AreEqual(3, r.Items.Count);
      }
    }

    [Test]
    public void ChainWrittenPercentsAreFinalPercents() {
      var roller = new Roller<string, Ledger>();
      var root = new RollTable<string>("Chest", 1, RollMode.Chain)
        .Add("Legendary", 3f).Add("Epic", 7f).Add("Rare", 15f).Add("Uncommon", 25f).Add("Common", 100f);
      var tally = new Dictionary<string, int> { { "Legendary", 0 }, { "Epic", 0 }, { "Rare", 0 }, { "Uncommon", 0 }, { "Common", 0 } };
      const int rolls = 40000;
      for (int seed = 0; seed < rolls; seed++) {
        RollResult<string> r = roller.Roll(root, new Ledger(), new SystemRandom(seed));
        Assert.AreEqual(1, r.Items.Count);
        tally[r.Items[0]]++;
      }
      // A naive chain would give 3 / 6.8 / 13.5 / 19.2 / 57.5; the rescale makes the written numbers the real ones.
      Assert.AreEqual(0.03f, tally["Legendary"] / (float)rolls, 0.005f);
      Assert.AreEqual(0.07f, tally["Epic"] / (float)rolls, 0.006f);
      Assert.AreEqual(0.15f, tally["Rare"] / (float)rolls, 0.008f);
      Assert.AreEqual(0.25f, tally["Uncommon"] / (float)rolls, 0.01f);
      Assert.AreEqual(0.50f, tally["Common"] / (float)rolls, 0.012f);
    }

    [Test]
    public void ChainTargetsOverHundredStillLandSomewhere() {
      var roller = new Roller<string, Ledger>();
      var root = new RollTable<string>("Chest", 1, RollMode.Chain).Add("A", 80f).Add("B", 80f).Add("C", 10f);
      for (int seed = 0; seed < 200; seed++) {
        RollResult<string> r = roller.Roll(root, new Ledger(), new SystemRandom(seed));
        Assert.AreEqual(1, r.Items.Count, r.ToString());
        Assert.AreNotEqual("C", r.Items[0]);   // A + B already claim everything
      }
    }

    [Test]
    public void ChainSkipsZeroAndEmptyBranches() {
      var roller = new Roller<string, Ledger>(new[] { new LockGate() });
      var dead = new RollTable<string>("Dead").Add("Never", 1f, "Locked");
      var root = new RollTable<string>("Chest", 1, RollMode.Chain).Add(dead, 100f).Add("Gone", 100f, "Locked").Add("Rest", 100f);
      for (int seed = 0; seed < 50; seed++) {
        RollResult<string> r = roller.Roll(root, new Ledger(), new SystemRandom(seed));
        Assert.AreEqual("Rest", r.Items.Count == 1 ? r.Items[0] : r.ToString());
      }
    }

    [Test]
    public void AssetBuildsTreeAndRolls() {
      var leafA = new TextAsset("a");
      var leafB = new TextAsset("b");
      var sub = ScriptableObject.CreateInstance<TextTable>();
      sub.name = "Sub";
      sub.picks = 2;
      sub.entries = new[] { new RollTableAsset<TextAsset>.Entry { weight = 1f, item = leafB } };
      var root = ScriptableObject.CreateInstance<TextTable>();
      root.name = "Root";
      root.entries = new[] {
        new RollTableAsset<TextAsset>.Entry { weight = 1f, item = leafA, tag = "A" },
        new RollTableAsset<TextAsset>.Entry { weight = 1f, table = sub, tag = "B" },
      };
      RollTable<TextAsset> table = root.ToTable();
      Assert.AreEqual(2, table.Entries.Count);
      Assert.AreSame(table, root.ToTable());
      Assert.AreEqual("Sub", table.Entries[1].Table.Name);
      RollResult<TextAsset> r = new Roller<TextAsset, object>().Roll(table, null, new SystemRandom(5));
      Assert.GreaterOrEqual(r.Items.Count, 1);
      foreach (TextAsset item in r.Items) {
        Assert.IsTrue(item == leafA || item == leafB);
      }
    }

    [Test]
    public void AssetCycleThrows() {
      var a = ScriptableObject.CreateInstance<TextTable>();
      a.name = "A";
      a.entries = new[] { new RollTableAsset<TextAsset>.Entry { weight = 1f, table = a } };
      Assert.Throws<System.InvalidOperationException>(() => a.ToTable());
    }

    private static float Share(Roller<string, Ledger> roller, RollTable<string> root, Ledger ledger, string item) {
      int hits = 0;
      for (int seed = 0; seed < 2000; seed++) {
        if (roller.Roll(root, ledger, new SystemRandom(seed)).Items[0] == item) {
          hits++;
        }
      }
      return hits / 2000f;
    }
  }
}
