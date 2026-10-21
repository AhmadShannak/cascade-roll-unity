# CascadeRoll

*My game's loot system grew into bags, fallbacks and cooldowns until nobody could say why a chest held what it held. I replaced it with one idea: a tree of tables and one `Roll()` call. This is that idea, with nothing game-specific left in it.*

## What is it?

A **cascading loot roll** for Unity (pure C# core). You build a tree of tables. The roll starts at the root, lands on an entry, and if that entry is another table it rolls again inside it, until it hits an item. Every source in your game (chest, wreck, boss) calls the same function.

```
Chest (picks 2)
 ├─ Legendary  3 %  → Flare Gun
 ├─ Epic       7 %  → Bow 2, Harpoon 1
 ├─ Rare      15 %  → Spear 3, Telescope 1
 ├─ Uncommon  25 %  → Antidote 2, Fishing Pole 1
 └─ Common    rest  → Tuna 3, Bandage 2, Plank 1
```

Every roll comes back with a trace:

```
[Chest] Legendary 3 % miss
[Chest] Epic 7 % miss
[Chest] Rare 15 % hit
  [Rare] Spear 75 % (w=3/4)
```

## Install

**Package Manager → `+` → Add package from git URL:**

```
https://github.com/AhmadShannak/cascade-roll-unity.git
```

## Quick start

```csharp
using CascadeRoll;

var common = new RollTable<string>("Common").Add("Tuna", 3f).Add("Bandage", 2f).Add("Plank", 1f);
var rare   = new RollTable<string>("Rare").Add("Spear", 3f).Add("Telescope", 1f);

var chest = new RollTable<string>("Chest", picks: 2, mode: RollMode.Chain)
  .Add(rare, 15f)        // 15 % of picks
  .Add(common, 100f);    // the rest

var roller = new Roller<string, object>();
RollResult<string> haul = roller.Roll(chest, null, new SystemRandom(seed));

haul.Items;        // ["Spear", "Tuna"]
Debug.Log(haul);   // the trace
```

`string` can be any type: your `ItemDefinition`, an enum, a prefab.

## Two ways a table can choose

**Weighted** (default). One die. Each entry's share is `weight / total`. Order does not matter. Use it for siblings with no order: item lists, Consumable / Weapon / Coin.

**Chain.** Top down. Each weight is that entry's final percent of all picks. The last entry catches the rest. Use it for ordered tiers. Rarest first.

A naive chain compounds: every step only sees what fell through the steps above, so the middle tiers come out thin and the last one fat. CascadeRoll rescales each check against what is still unclaimed (`target / remaining`), so the numbers you write are the numbers you get. Measured over 10 000 chests from the Basic Roll sample:

| tier | written | naive chain | CascadeRoll |
|---|---|---|---|
| Legendary | 3 | 3.0 | 3.01 |
| Epic | 7 | 6.8 | 7.12 |
| Rare | 15 | 13.5 | 15.24 |
| Uncommon | 25 | 19.2 | 24.96 |
| Common | rest | 57.5 | 49.68 |

Mix the modes freely: a Chain on rarity, Weighted inside each tier.

## Changing odds at roll time

A modifier sees each entry, your own context, and what the roll has dealt so far. It returns a new weight. `0` excludes the entry.

```csharp
public class Pity : IWeightModifier<string, Ledger> {
  public float Modify(RollEntry<string> e, Ledger ctx, IReadOnlyList<string> haul, float w) {
    return e.Tag == "Epic" ? w + ctx.ChestsSinceEpic * 2f : w;   // +2 % per dry chest
  }
}

var roller = new Roller<string, Ledger>(modifiers: new[] { new Pity() });
```

Typical modifiers, each a few lines: pity, falloff (`w / (1 + timesDropped)`), unlock by progress, no duplicates in one haul (`haul.Contains(e.Item) ? 0 : w`).

The library keeps no state. Your context (a ledger of what dropped) is yours to update and, in a networked game, replicate.

## Guaranteed drops

```csharp
public class FirstChestTelescope : IRollRule<string, Ledger> {
  public IEnumerable<string> Guaranteed(Ledger ctx) => ctx.Opens == 0 ? new[] { "Telescope" } : null;
}
```

Rules are dealt before the dice and count toward the table's picks.

## Simulate

A roll is deterministic, so odds are a loop away:

```csharp
SimulationReport<string> report = RollSimulator.Run(roller, chest, ledger, rolls: 10000,
  afterRoll: (ctx, haul) => ctx.Record(haul));   // feed your ledger so pity and falloff simulate too
Debug.Log(report);
```

```
10000 rolls, 20000 items (2.00 per roll, min 2 max 2)
by tag:
  Common            49.68 %  9935
  Uncommon          24.96 %  4991
  Rare              15.24 %  3048
  Epic               7.12 %  1424
  Legendary          3.01 %  602
by item:
  Plank             16.82 %  3364
  Tuna              16.64 %  3327
  Bandage           16.22 %  3244
  Fishing Pole      12.58 %  2515
  ...
```

Run it with a modifier and the report shows what the modifier did. The Rarity Pity sample prints both:

| tier | pity off | pity on |
|---|---|---|
| Legendary | 3.01 | 5.64 |
| Epic | 7.12 | 10.90 |
| Common | 49.68 | 43.31 |

Every sample ends with a simulation (`simulateRolls` in the Inspector, 0 skips it), and every `RollTableAsset` has a **Simulate** button in its Inspector (base odds, no modifiers).

## Inspector tables

Subclass once per item type, then author tables as assets that reference each other.

```csharp
[CreateAssetMenu(menuName = "Loot/Table")]
public class ItemTable : RollTableAsset<ItemDefinition> { }

RollTable<ItemDefinition> chest = chestAsset.ToTable();
```

## Samples

Import from the package's Samples tab. Read in order.

| Sample | What it adds |
|---|---|
| **Basic Roll** | Chain on rarity, equal odds inside each tier. Nothing else. |
| **Rarity Pity** | Weights inside tiers, plus one modifier: dry chests raise Epic and Legendary. |
| **Advanced Loot Demo** | Type-first tree, ledger falloff on items and types, no duplicates in a haul, a guaranteed first drop. |

## Good to know

- Deterministic: same tree, same context, same seed → same haul.
- A branch that yields nothing is skipped and the table chooses again. No fallback tables.
- Chain order is the order you `Add`. Not sorted for you, on purpose: modifiers move weights at run time.

## License

MIT
