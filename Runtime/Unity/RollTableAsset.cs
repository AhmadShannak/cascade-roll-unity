using System;
using System.Collections.Generic;
using UnityEngine;

namespace CascadeRoll {
  /// <summary>
  /// Inspector-authored roll table: subclass it once per item type (`class ItemTable : RollTableAsset&lt;ItemDefinition&gt;` with a CreateAssetMenu) and call ToTable() at runtime.
  /// </summary>
  public abstract class RollTableAsset<TItem> : ScriptableObject where TItem : UnityEngine.Object {
    /// <summary>
    /// One authored line: fill Item for a leaf, or Table to descend; Weight and Tag as in RollEntry.
    /// </summary>
    [Serializable]
    public struct Entry {
      [Min(0f)] public float weight;
      public string tag;
      public TItem item;
      public RollTableAsset<TItem> table;
    }

    [Tooltip("Entries this table deals when entered (root: the container's draw count).")]
    [Min(1)] public int picks = 1;
    [Tooltip("Weighted: one die, shares by weight. Chain: top down, each weight is a % chance to stop there, the last entry catches the rest.")]
    public RollMode mode = RollMode.Weighted;
    public Entry[] entries = Array.Empty<Entry>();

    private RollTable<TItem> _built;

    /// <summary>The runtime tree for this asset and everything under it; built once and cached until the asset changes.</summary>
    public RollTable<TItem> ToTable() {
      if (_built == null) {
        _built = Build(new HashSet<RollTableAsset<TItem>>());
      }
      return _built;
    }

    private RollTable<TItem> Build(HashSet<RollTableAsset<TItem>> visiting) {
      if (!visiting.Add(this)) {
        throw new InvalidOperationException("RollTableAsset '" + name + "' references itself through its sub-tables.");
      }
      var table = new RollTable<TItem>(name, picks, mode);
      foreach (Entry e in entries) {
        if (e.table != null) {
          table.Add(e.table.Build(visiting), e.weight, string.IsNullOrEmpty(e.tag) ? null : e.tag);
        } else if (e.item != null) {
          table.Add(e.item, e.weight, string.IsNullOrEmpty(e.tag) ? null : e.tag);
        }
      }
      visiting.Remove(this);
      return table;
    }

    private void OnValidate() {
      _built = null;
    }
  }
}
