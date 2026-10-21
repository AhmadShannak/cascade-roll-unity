using System;
using System.Collections.Generic;

namespace CascadeRoll {
  /// <summary>
  /// Deals a haul from a table tree: rules first, then picks that descend through sub-tables (one weighted die per table, or a top-down chain of percent checks), each weight bent by the modifiers; a branch that yields nothing is skipped and the table chooses again.
  /// </summary>
  public sealed class Roller<TItem, TContext> {
    /// <summary>Dead branches a table tolerates before it gives up on that pick.</summary>
    public int MaxAttempts = 3;

    private readonly IWeightModifier<TItem, TContext>[] _modifiers;
    private readonly IRollRule<TItem, TContext>[] _rules;

    public Roller(IEnumerable<IWeightModifier<TItem, TContext>> modifiers = null, IEnumerable<IRollRule<TItem, TContext>> rules = null) {
      _modifiers = modifiers != null ? new List<IWeightModifier<TItem, TContext>>(modifiers).ToArray() : Array.Empty<IWeightModifier<TItem, TContext>>();
      _rules = rules != null ? new List<IRollRule<TItem, TContext>>(rules).ToArray() : Array.Empty<IRollRule<TItem, TContext>>();
    }

    /// <summary>Deals root.Picks items: whatever the rules guarantee first, the dice for the rest.</summary>
    public RollResult<TItem> Roll(RollTable<TItem> root, TContext context, IRandom rng) {
      if (root == null) {
        throw new ArgumentNullException(nameof(root));
      }
      if (rng == null) {
        throw new ArgumentNullException(nameof(rng));
      }
      var result = new RollResult<TItem>();
      foreach (IRollRule<TItem, TContext> rule in _rules) {
        IEnumerable<TItem> forced = rule.Guaranteed(context);
        if (forced == null) {
          continue;
        }
        foreach (TItem item in forced) {
          result.Items.Add(item);
          result.Trace.Add("rule " + rule.GetType().Name + ": " + item);
        }
      }
      int picks = Math.Max(0, root.Picks - result.Items.Count);
      for (int p = 0; p < picks; p++) {
        if (!PickOne(root, context, rng, result, 0)) {
          result.Trace.Add("[" + root.Name + "] nothing: every entry weighs 0");
        }
      }
      return result;
    }

    // One pick from `table`: weigh every entry, choose, descend or add. A sub-table that yields nothing is zeroed and
    // the choice is made again, up to MaxAttempts; false when the table has nothing left to give.
    private bool PickOne(RollTable<TItem> table, TContext context, IRandom rng, RollResult<TItem> result, int depth) {
      return table.Mode == RollMode.Chain
        ? PickChain(table, context, rng, result, depth)
        : PickWeighted(table, context, rng, result, depth);
    }

    // Top down. An entry's weight is the share of ALL picks it should get, so the check at its step is rescaled by
    // what is still unclaimed (chance = weight / remaining): a plain chain would squeeze the middle tiers and fatten
    // the last one. A hit that yields an item ends the pick; a miss or an empty branch falls through. The last live
    // entry is taken without a check so the chain always lands somewhere.
    private bool PickChain(RollTable<TItem> table, TContext context, IRandom rng, RollResult<TItem> result, int depth) {
      string indent = new string(' ', depth * 2);
      int n = table.Entries.Count;
      int lastLive = -1;
      for (int i = n - 1; i >= 0; i--) {
        if (Weigh(table.Entries[i], context, result.Items) > 0f) {
          lastLive = i;
          break;
        }
      }
      float remaining = 100f;
      for (int i = 0; i <= lastLive; i++) {
        RollEntry<TItem> entry = table.Entries[i];
        float target = Weigh(entry, context, result.Items);
        if (target <= 0f) {
          continue;
        }
        float chance = remaining <= 0f ? 100f : Math.Min(100f, target / remaining * 100f);
        bool hit = i == lastLive || rng.NextDouble() * 100.0 < chance;
        result.Trace.Add(indent + "[" + table.Name + "] " + entry + " " + (i == lastLive ? "rest" : target.ToString("0.##") + " % (" + chance.ToString("0.#") + " % of rest)") + (hit ? " hit" : " miss"));
        remaining -= target;
        if (!hit) {
          continue;
        }
        if (Enter(entry, context, rng, result, depth)) {
          return true;
        }
        result.Trace.Add(indent + "[" + table.Name + "] " + entry + " is empty, falling through");
      }
      return false;
    }

    private bool PickWeighted(RollTable<TItem> table, TContext context, IRandom rng, RollResult<TItem> result, int depth) {
      int n = table.Entries.Count;
      var weights = new float[n];
      float total = 0f;
      for (int i = 0; i < n; i++) {
        weights[i] = Weigh(table.Entries[i], context, result.Items);
        total += weights[i];
      }
      string indent = new string(' ', depth * 2);
      for (int attempt = 0; attempt < MaxAttempts && total > 0f; attempt++) {
        int chosen = Choose(weights, total, rng);
        RollEntry<TItem> entry = table.Entries[chosen];
        result.Trace.Add(indent + "[" + table.Name + "] " + entry + " " + (weights[chosen] / total).ToString("P0") + " (w=" + weights[chosen].ToString("0.##") + "/" + total.ToString("0.##") + ")");
        if (Enter(entry, context, rng, result, depth)) {
          return true;
        }
        result.Trace.Add(indent + "[" + table.Name + "] " + entry + " is empty, re-rolling without it");
        total -= weights[chosen];
        weights[chosen] = 0f;
      }
      return false;
    }

    // Takes a chosen entry: a leaf is added, a sub-table is rolled its own Picks times. False when nothing came out.
    private bool Enter(RollEntry<TItem> entry, TContext context, IRandom rng, RollResult<TItem> result, int depth) {
      if (entry.IsLeaf) {
        result.Items.Add(entry.Item);
        return true;
      }
      bool any = false;
      for (int p = 0; p < Math.Max(1, entry.Table.Picks); p++) {
        any |= PickOne(entry.Table, context, rng, result, depth + 1);
      }
      return any;
    }

    private float Weigh(RollEntry<TItem> entry, TContext context, IReadOnlyList<TItem> haul) {
      float w = entry.Weight;
      for (int m = 0; m < _modifiers.Length && w > 0f; m++) {
        w = _modifiers[m].Modify(entry, context, haul, w);
      }
      return w > 0f && !float.IsNaN(w) ? w : 0f;
    }

    private static int Choose(float[] weights, float total, IRandom rng) {
      double r = rng.NextDouble() * total;
      int last = -1;
      for (int i = 0; i < weights.Length; i++) {
        if (weights[i] <= 0f) {
          continue;
        }
        last = i;
        r -= weights[i];
        if (r < 0) {
          return i;
        }
      }
      return last;   // float rounding landed past the end: the last live entry
    }
  }
}
