using System;
using System.Collections.Generic;
using System.Text;

namespace CascadeRoll {
  /// <summary>
  /// Rolls a table many times and tallies what came out, so odds can be read off a histogram instead of guessed.
  /// </summary>
  public static class RollSimulator {
    /// <summary>
    /// Rolls `rolls` times with seeds `seed`, `seed + 1`, …; `afterRoll` runs between rolls so a ledger (pity, falloff) can be updated the way the game would.
    /// </summary>
    public static SimulationReport<TItem> Run<TItem, TContext>(Roller<TItem, TContext> roller, RollTable<TItem> table, TContext context, int rolls, int seed = 0, Action<TContext, RollResult<TItem>> afterRoll = null) {
      if (roller == null) {
        throw new ArgumentNullException(nameof(roller));
      }
      var report = new SimulationReport<TItem> { Rolls = rolls };
      for (int i = 0; i < rolls; i++) {
        RollResult<TItem> result = roller.Roll(table, context, new SystemRandom(seed + i));
        report.Record(result, table);
        afterRoll?.Invoke(context, result);
      }
      return report;
    }
  }

  /// <summary>
  /// Tally of a simulation: how often each item and each tag came out, and how many items a roll dealt.
  /// </summary>
  public sealed class SimulationReport<TItem> {
    public int Rolls;
    public int ItemsDealt;
    public int MinPerRoll = int.MaxValue;
    public int MaxPerRoll;
    public readonly Dictionary<TItem, int> ByItem = new Dictionary<TItem, int>();
    public readonly Dictionary<string, int> ByTag = new Dictionary<string, int>();

    /// <summary>Share of all dealt items that were this item (0..1).</summary>
    public float Share(TItem item) {
      return ItemsDealt > 0 && ByItem.TryGetValue(item, out int n) ? n / (float)ItemsDealt : 0f;
    }

    /// <summary>Share of all dealt items that came through an entry with this tag (0..1).</summary>
    public float TagShare(string tag) {
      return ItemsDealt > 0 && ByTag.TryGetValue(tag, out int n) ? n / (float)ItemsDealt : 0f;
    }

    internal void Record(RollResult<TItem> result, RollTable<TItem> table) {
      int count = result.Items.Count;
      ItemsDealt += count;
      MinPerRoll = Math.Min(MinPerRoll, count);
      MaxPerRoll = Math.Max(MaxPerRoll, count);
      foreach (TItem item in result.Items) {
        ByItem[item] = (ByItem.TryGetValue(item, out int n) ? n : 0) + 1;
        string tag = TagOf(table, item);
        if (tag != null) {
          ByTag[tag] = (ByTag.TryGetValue(tag, out int t) ? t : 0) + 1;
        }
      }
    }

    // The tag of the first tagged entry on the path to this item: a tiered tree tags its tiers, so the tier gets counted.
    private static string TagOf(RollTable<TItem> table, TItem item) {
      foreach (RollEntry<TItem> e in table.Entries) {
        if (e.IsLeaf) {
          if (EqualityComparer<TItem>.Default.Equals(e.Item, item) && !string.IsNullOrEmpty(e.Tag)) {
            return e.Tag;
          }
        } else if (Contains(e.Table, item)) {
          return !string.IsNullOrEmpty(e.Tag) ? e.Tag : TagOf(e.Table, item);
        }
      }
      return null;
    }

    private static bool Contains(RollTable<TItem> table, TItem item) {
      foreach (RollEntry<TItem> e in table.Entries) {
        if (e.IsLeaf ? EqualityComparer<TItem>.Default.Equals(e.Item, item) : Contains(e.Table, item)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>Text table: tags first, then items, highest share first.</summary>
    public override string ToString() {
      var sb = new StringBuilder();
      sb.Append(Rolls).Append(" rolls, ").Append(ItemsDealt).Append(" items (")
        .Append(Rolls > 0 ? (ItemsDealt / (float)Rolls).ToString("0.00") : "0").Append(" per roll, min ")
        .Append(Rolls > 0 ? MinPerRoll : 0).Append(" max ").Append(MaxPerRoll).AppendLine(")");
      if (ByTag.Count > 0) {
        sb.AppendLine("by tag:");
        foreach (KeyValuePair<string, int> kv in Sorted(ByTag)) {
          sb.Append("  ").Append(kv.Key.PadRight(16)).Append((100f * kv.Value / ItemsDealt).ToString("0.00").PadLeft(7)).Append(" %  ").Append(kv.Value).AppendLine();
        }
      }
      sb.AppendLine("by item:");
      foreach (KeyValuePair<TItem, int> kv in Sorted(ByItem)) {
        sb.Append("  ").Append((kv.Key != null ? kv.Key.ToString() : "null").PadRight(16)).Append((100f * kv.Value / ItemsDealt).ToString("0.00").PadLeft(7)).Append(" %  ").Append(kv.Value).AppendLine();
      }
      return sb.ToString();
    }

    private static List<KeyValuePair<TKey, int>> Sorted<TKey>(Dictionary<TKey, int> d) {
      var list = new List<KeyValuePair<TKey, int>>(d);
      list.Sort((a, b) => b.Value.CompareTo(a.Value));
      return list;
    }
  }
}
