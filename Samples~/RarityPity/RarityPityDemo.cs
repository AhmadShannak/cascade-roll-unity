using System.Collections.Generic;
using CascadeRoll;
using UnityEngine;

namespace CascadeRoll.Samples {
  /// <summary>
  /// What the mid demo remembers: chests opened since the last Epic-or-better drop.
  /// </summary>
  public class PityLedger {
    public int ChestsSinceEpic;
  }

  /// <summary>
  /// Every chest without an Epic or Legendary adds pityPerChest percent to both; a hit resets the ledger. Only the two top tiers are touched.
  /// </summary>
  public class RarityPity : IWeightModifier<string, PityLedger> {
    private readonly float _pityPerChest;

    public RarityPity(float pityPerChest) {
      _pityPerChest = pityPerChest;
    }

    public float Modify(RollEntry<string> entry, PityLedger ledger, IReadOnlyList<string> haul, float weight) {
      if (entry.Tag == "Legendary" || entry.Tag == "Epic") {
        return weight + ledger.ChestsSinceEpic * _pityPerChest;
      }
      return weight;
    }
  }

  /// <summary>
  /// Middle step between the basic and advanced demos: a Chain on rarity, real weights inside each tier (Tuna drops more than Plank), and one modifier that raises Epic and Legendary odds per dry chest. Add to any GameObject and press Play.
  /// </summary>
  public class RarityPityDemo : MonoBehaviour {
    [SerializeField] private int seed = 1;
    [SerializeField] private int containers = 8;
    [SerializeField] private float pityPerChest = 2f;

    private void Start() {
      // Inside a tier, weights differ: Tuna 3 : Bandage 2 : Plank 1 = 50 % / 33 % / 17 %.
      var common    = new RollTable<string>("Common").Add("Tuna", 3f).Add("Bandage", 2f).Add("Plank", 1f);
      var uncommon  = new RollTable<string>("Uncommon").Add("Antidote", 2f).Add("Fishing Pole", 1f);
      var rare      = new RollTable<string>("Rare").Add("Spear", 3f).Add("Telescope", 1f);
      var epic      = new RollTable<string>("Epic").Add("Bow", 2f).Add("Harpoon", 1f);
      var legendary = new RollTable<string>("Legendary").Add("Flare Gun", 1f);

      // Rarity chain (each number = final share of picks), tagged so the pity modifier can find the top tiers.
      var chest = new RollTable<string>("Chest", picks: 2, mode: RollMode.Chain)
        .Add(legendary, 3f, "Legendary")
        .Add(epic, 7f, "Epic")
        .Add(rare, 15f, "Rare")
        .Add(uncommon, 25f, "Uncommon")
        .Add(common, 100f, "Common");

      var roller = new Roller<string, PityLedger>(new[] { new RarityPity(pityPerChest) });
      var ledger = new PityLedger();
      for (int i = 0; i < containers; i++) {
        RollResult<string> haul = roller.Roll(chest, ledger, new SystemRandom(seed * 1000 + i));
        Debug.Log("Chest " + (i + 1) + " (pity +" + ledger.ChestsSinceEpic * pityPerChest + " %) → " + string.Join(", ", haul.Items) + "\n" + haul);
        bool gotEpic = haul.Items.Exists(item => epic.Entries.Exists(e => e.Item == item) || legendary.Entries.Exists(e => e.Item == item));
        ledger.ChestsSinceEpic = gotEpic ? 0 : ledger.ChestsSinceEpic + 1;
      }
    }
  }
}
