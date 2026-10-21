using System.Collections.Generic;
using CascadeRoll;
using UnityEngine;

namespace CascadeRoll.Samples {
  /// <summary>
  /// What the demo remembers between containers: how many times each item and each type has dropped this level.
  /// </summary>
  public class DemoLedger {
    public readonly Dictionary<string, int> Dropped = new Dictionary<string, int>();
    public int Opens;

    public int Count(string key) {
      return key != null && Dropped.TryGetValue(key, out int n) ? n : 0;
    }

    public void Record(string item, string type) {
      Dropped[item] = Count(item) + 1;
      Dropped[type] = Count(type) + 1;
    }
  }

  /// <summary>
  /// Weight ÷ (1 + times dropped, this haul included): an item or type that keeps dropping gets rarer, so the others rise on their own.
  /// </summary>
  public class Falloff : IWeightModifier<string, DemoLedger> {
    public float Modify(RollEntry<string> entry, DemoLedger ledger, IReadOnlyList<string> haul, float weight) {
      int inHaul = 0;
      foreach (string item in haul) {
        if (entry.IsLeaf ? item == entry.Item : TypeOf(item) == entry.Tag) {
          inHaul++;
        }
      }
      return weight / (1 + ledger.Count(entry.IsLeaf ? entry.Item : entry.Tag) + inHaul);
    }

    private static string TypeOf(string item) {
      return LootDemo.TypeOf(item);
    }
  }

  /// <summary>
  /// The first container always holds a Telescope.
  /// </summary>
  public class FirstOpenTelescope : IRollRule<string, DemoLedger> {
    public IEnumerable<string> Guaranteed(DemoLedger ledger) {
      return ledger.Opens == 0 ? new[] { "Telescope" } : null;
    }
  }

  /// <summary>
  /// Opens `containers` chests on Start and prints each haul with its trace; add to any GameObject and press Play.
  /// </summary>
  public class LootDemo : MonoBehaviour {
    [SerializeField] private int seed = 1;
    [SerializeField] private int containers = 6;
    [SerializeField] private int picksPerContainer = 2;

    private void Start() {
      RollTable<string> root = BuildTree(picksPerContainer);
      var roller = new Roller<string, DemoLedger>(new[] { new Falloff() }, new[] { new FirstOpenTelescope() });
      var ledger = new DemoLedger();
      for (int i = 0; i < containers; i++) {
        RollResult<string> haul = roller.Roll(root, ledger, new SystemRandom(seed * 1000 + i));
        Debug.Log("Container " + (i + 1) + " → " + string.Join(", ", haul.Items) + "\n" + haul);
        foreach (string item in haul.Items) {
          ledger.Record(item, TypeOf(item));
        }
        ledger.Opens++;
      }
    }

    // Type → item. Weights are the only rarity: Tuna 10 vs Bow 1.
    private static RollTable<string> BuildTree(int picks) {
      var consumable = new RollTable<string>("Consumable").Add("Tuna", 10f).Add("Bandage", 6f).Add("Antidote", 2f);
      var weapon = new RollTable<string>("Weapon").Add("Spear", 5f).Add("Bow", 1f);
      var coins = new RollTable<string>("CoinPile", 3).Add("Coin", 1f);
      return new RollTable<string>("Chest", picks)
        .Add(consumable, 6f, "Consumable")
        .Add(weapon, 3f, "Weapon")
        .Add(coins, 1f, "Coin");
    }

    private static RollTable<string> _tree;

    /// <summary>Which type table an item belongs to ("Consumable", "Weapon", "Coin"), or "Other".</summary>
    public static string TypeOf(string item) {
      _tree ??= BuildTree(1);
      foreach (RollEntry<string> e in _tree.Entries) {
        if (!e.IsLeaf && e.Table.Entries.Exists(x => x.IsLeaf && x.Item == item)) {
          return e.Tag;
        }
      }
      return "Other";
    }
  }
}
