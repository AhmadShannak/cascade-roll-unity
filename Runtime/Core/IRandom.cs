namespace CascadeRoll {
  /// <summary>
  /// Source of randomness a roll draws from; pass a seeded one and the roll is a pure function of its inputs.
  /// </summary>
  public interface IRandom {
    /// <summary>Uniform value in [0, 1).</summary>
    double NextDouble();
  }

  /// <summary>
  /// Wraps System.Random as an IRandom.
  /// </summary>
  public sealed class SystemRandom : IRandom {
    private readonly System.Random _rng;

    public SystemRandom(int seed) {
      _rng = new System.Random(seed);
    }

    public SystemRandom(System.Random rng) {
      _rng = rng;
    }

    public double NextDouble() {
      return _rng.NextDouble();
    }
  }
}
