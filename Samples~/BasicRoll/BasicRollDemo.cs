using CascadeRoll;
using UnityEngine;

namespace CascadeRoll.Samples {
  /// <summary>
  /// The plain cascade: a chest rolls its rarities top down (Legendary 3 %, Epic 7 %, Rare 15 %, Uncommon 25 %, Common the rest, each number the real share), then the rarity picks an item at equal odds. No modifiers, no rules, no memory. Add to any GameObject and press Play.
  /// </summary>
  public class BasicRollDemo : MonoBehaviour {
    [SerializeField] private int seed = 1;
    [SerializeField] private int containers = 5;
    [Tooltip("Rolls for the simulation printed after the chests; 0 skips it.")]
    [SerializeField] private int simulateRolls = 10000;

    private void Start() {
      var common    = new RollTable<string>("Common").Add("Tuna", 1f).Add("Bandage", 1f).Add("Plank", 1f);
      var uncommon  = new RollTable<string>("Uncommon").Add("Antidote", 1f).Add("Fishing Pole", 1f);
      var rare      = new RollTable<string>("Rare").Add("Spear", 1f).Add("Telescope", 1f);
      var epic      = new RollTable<string>("Epic").Add("Bow", 1f).Add("Harpoon", 1f);
      var legendary = new RollTable<string>("Legendary").Add("Flare Gun", 1f);

      // Chain: top down. Each number is that tier's final share of all picks (the roller rescales the check at each
      // step against what fell through, so 7 really means 7 %). Common catches the rest: 50 %.
      var chest = new RollTable<string>("Chest", picks: 2, mode: RollMode.Chain)
        .Add(legendary, 3f, "Legendary")
        .Add(epic, 7f, "Epic")
        .Add(rare, 15f, "Rare")
        .Add(uncommon, 25f, "Uncommon")
        .Add(common, 100f, "Common");

      var roller = new Roller<string, object>();
      for (int i = 0; i < containers; i++) {
        RollResult<string> haul = roller.Roll(chest, null, new SystemRandom(seed * 1000 + i));
        Debug.Log("Chest " + (i + 1) + " → " + string.Join(", ", haul.Items) + "\n" + haul);
      }

      // The odds, measured: tiers should read 3 / 7 / 15 / 25 / 50.
      if (simulateRolls > 0) {
        Debug.Log("Simulation\n" + RollSimulator.Run(roller, chest, null, simulateRolls, seed));
      }
    }
  }
}
