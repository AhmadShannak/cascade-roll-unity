using System.Collections.Generic;

namespace CascadeRoll {
  /// <summary>
  /// Bends one entry's weight at roll time from the game's context (drop history, progress, crew); return 0 to exclude the entry.
  /// </summary>
  public interface IWeightModifier<TItem, TContext> {
    /// <summary>Returns the new weight; `weight` is the base weight already run through earlier modifiers, `haul` is what this roll has dealt so far (rules included).</summary>
    float Modify(RollEntry<TItem> entry, TContext context, IReadOnlyList<TItem> haul, float weight);
  }
}
