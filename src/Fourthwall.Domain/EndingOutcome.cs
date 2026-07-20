namespace Fourthwall.Domain;

/// <summary>
/// The outcome carried by an ending scene — how the story finished for the reader.
/// </summary>
/// <remarks>
/// Instances are created through <see cref="Death"/>, <see cref="Victory"/> and
/// <see cref="Other"/> so an outcome that violates its invariants cannot be constructed.
/// </remarks>
public sealed record EndingOutcome
{
    private EndingOutcome(OutcomeKind kind, string? label)
    {
        Kind = kind;
        Label = label;
    }

    /// <summary>
    /// Gets the broad category of this outcome.
    /// </summary>
    public OutcomeKind Kind { get; }

    /// <summary>
    /// Gets the creator-supplied description of the outcome, or <see langword="null"/>
    /// when the category alone describes it.
    /// </summary>
    public string? Label { get; }

    /// <summary>
    /// Creates an outcome in which the reader dies.
    /// </summary>
    /// <param name="label">An optional description, such as "Eaten by the grue".</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is supplied but blank.</exception>
    public static EndingOutcome Death(string? label = null) => Create(OutcomeKind.Death, label);

    /// <summary>
    /// Creates an outcome in which the reader wins.
    /// </summary>
    /// <param name="label">An optional description, such as "Crowned at dawn".</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException"><paramref name="label"/> is supplied but blank.</exception>
    public static EndingOutcome Victory(string? label = null) => Create(OutcomeKind.Victory, label);

    /// <summary>
    /// Creates an outcome that is neither death nor victory, described by its label.
    /// </summary>
    /// <param name="label">The required description of the ending.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="label"/> is blank.</exception>
    public static EndingOutcome Other(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new EndingOutcome(OutcomeKind.Other, label);
    }

    private static EndingOutcome Create(OutcomeKind kind, string? label)
    {
        if (label is not null && string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("An ending outcome label must not be blank.", nameof(label));
        }

        return new EndingOutcome(kind, label);
    }
}

/// <summary>
/// The broad category of an <see cref="EndingOutcome"/>.
/// </summary>
public enum OutcomeKind
{
    /// <summary>The story ends with the reader's death.</summary>
    Death,

    /// <summary>The story ends with the reader's victory.</summary>
    Victory,

    /// <summary>The story ends some other way, described by the outcome's label.</summary>
    Other,
}
