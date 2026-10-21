using System.Collections.Generic;

namespace CascadeRoll {
  /// <summary>
  /// Hard rule dealt before the dice ("the first chest holds a telescope"); its items count toward the root table's picks.
  /// </summary>
  public interface IRollRule<TItem, TContext> {
    /// <summary>Items this rule forces for the given context; null or empty when it does not apply.</summary>
    IEnumerable<TItem> Guaranteed(TContext context);
  }
}
