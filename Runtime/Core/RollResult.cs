using System.Collections.Generic;

namespace CascadeRoll {
  /// <summary>
  /// What one roll dealt, plus one trace line per decision so any haul can be read back.
  /// </summary>
  public sealed class RollResult<TItem> {
    public readonly List<TItem> Items = new List<TItem>();
    public readonly List<string> Trace = new List<string>();

    /// <summary>The trace as one block of text.</summary>
    public override string ToString() {
      return string.Join("\n", Trace);
    }
  }
}
