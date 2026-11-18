namespace TheDotSide.Carbonite.Serialization;

/// <summary>A formatted scalar: JSON string content when <paramref name="IsQuoted"/>, a literal otherwise.</summary>
internal readonly record struct Scalar(string Text, bool IsQuoted);
