# Changelog

All notable changes to this package are documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/); versions follow [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-10-21

### Added
- `RollSimulator.Run(roller, table, context, rolls, seed, afterRoll)` → `SimulationReport<T>`: share per item and per tag, items per roll, printable table; `afterRoll` feeds a ledger so pity and falloff simulate the way the game runs them.
- **Simulate** button on every `RollTableAsset` Inspector (base odds, no modifiers).
- Every sample ends with a simulation printout (`simulateRolls`, 0 skips); Rarity Pity prints pity on vs off.

### Changed
- README: compounding table (written vs naive chain vs CascadeRoll) and real simulation output.

## [1.0.0] - 2026-10-20

### Added
- Initial release: `Roller<TItem, TContext>.Roll(table, context, rng)` walks a weighted table tree and returns the picked items plus a human-readable trace.
- `RollTable<T>` / `RollEntry<T>`: an entry is either a leaf item or a sub-table; a table rolls `Picks` times when entered.
- `RollMode.Weighted` (one die, shares by weight) and `RollMode.Chain` (top-down; each weight is the entry's final share, rescaled per step so the written percents are the real ones; last entry catches the rest) per table.
- `IWeightModifier<TItem, TContext>`: bend any entry's weight at roll time from your context and the haul dealt so far (falloff by drop count, pity, progress gates, no-duplicate rules). Weight 0 excludes the entry.
- `IRollRule<TItem, TContext>`: guaranteed drops dealt before the dice; they count toward the root's picks.
- Cascade fallback: a sub-table whose every entry weighs 0 is dropped from the parent's choices and the parent re-rolls.
- `IRandom` + `SystemRandom`: bring your own seeded RNG so a roll is a pure function of its inputs.
- `RollTableAsset<T>`: abstract ScriptableObject for authoring tables in the Inspector; `ToTable()` builds the runtime tree.
- `Basic Roll`, `Rarity Pity` and `Advanced Loot Demo` samples, EditMode tests.
