using System.Collections.Generic;

namespace CascadeRoll {
  /// <summary>
  /// How a table chooses among its entries.
  /// </summary>
  public enum RollMode {
    /// <summary>One die: each entry's share is its weight over the table's total.</summary>
    Weighted,
    /// <summary>Top down: each entry's weight is its FINAL percent of all picks (0-100); the roller rescales it against what fell through so the written numbers are what you get. The last entry catches whatever is left.</summary>
    Chain,
  }

  /// <summary>
  /// One node of the roll tree: weighted entries that are each either an item or another table; entered, it rolls Picks times.
  /// </summary>
  public sealed class RollTable<TItem> {
    /// <summary>Shown in the trace.</summary>
    public string Name;
    /// <summary>How many entries this table deals when entered (root: the container's draw count; a coin pile: 3).</summary>
    public int Picks = 1;
    /// <summary>Weighted (default) or Chain; see RollMode.</summary>
    public RollMode Mode = RollMode.Weighted;
    public readonly List<RollEntry<TItem>> Entries = new List<RollEntry<TItem>>();

    public RollTable(string name, int picks = 1, RollMode mode = RollMode.Weighted) {
      Name = name;
      Picks = picks;
      Mode = mode;
    }

    /// <summary>Adds a leaf entry.</summary>
    public RollTable<TItem> Add(TItem item, float weight, string tag = null) {
      Entries.Add(new RollEntry<TItem>(item, weight, tag));
      return this;
    }

    /// <summary>Adds a sub-table entry.</summary>
    public RollTable<TItem> Add(RollTable<TItem> table, float weight, string tag = null) {
      Entries.Add(new RollEntry<TItem>(table, weight, tag));
      return this;
    }
  }

  /// <summary>
  /// One weighted line of a table: a leaf item or a sub-table to descend into, plus a free tag modifiers can key on.
  /// </summary>
  public sealed class RollEntry<TItem> {
    /// <summary>Base weight before modifiers: a share of the table's total (Weighted), or the final percent of picks that land here (Chain).</summary>
    public float Weight;
    /// <summary>Free label ("Consumable", "Weapon") for modifiers that count by kind rather than by item.</summary>
    public string Tag;
    public readonly TItem Item;
    public readonly RollTable<TItem> Table;

    /// <summary>True when this entry yields an item; false when it descends into Table.</summary>
    public bool IsLeaf => Table == null;

    public RollEntry(TItem item, float weight, string tag = null) {
      Item = item;
      Weight = weight;
      Tag = tag;
    }

    public RollEntry(RollTable<TItem> table, float weight, string tag = null) {
      Table = table;
      Weight = weight;
      Tag = tag ?? table.Name;
    }

    /// <summary>Entry label for the trace: the tag, else the item or table name.</summary>
    public override string ToString() {
      if (!string.IsNullOrEmpty(Tag)) {
        return Tag;
      }
      return IsLeaf ? (Item != null ? Item.ToString() : "null") : Table.Name;
    }
  }
}
