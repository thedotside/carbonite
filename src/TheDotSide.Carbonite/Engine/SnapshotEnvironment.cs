namespace TheDotSide.Carbonite.Engine;

/// <summary>Where the tests run: on a CI server, and whether the developer asked for an update.</summary>
internal sealed record SnapshotEnvironment(bool IsContinuousIntegration, bool UpdateRequested)
{
    internal const string UpdateVariable = "SNAPSHOT_UPDATE";

    // Set by the CI services themselves; CI=false (or 0, no, off) explicitly opts out.
    private static readonly string[] ContinuousIntegrationVariables =
    [
        "CI",
        "TF_BUILD",
        "GITHUB_ACTIONS",
        "GITLAB_CI",
        "JENKINS_URL",
        "TEAMCITY_VERSION",
        "BUILDKITE",
        "CIRCLECI",
        "TRAVIS",
        "APPVEYOR",
        "BITBUCKET_BUILD_NUMBER",
        "CODEBUILD_BUILD_ID",
    ];

    private static readonly string[] FalseValues = ["0", "false", "no", "off"];
    private static readonly string[] TrueValues = ["1", "true", "yes", "on"];

    internal static SnapshotEnvironment FromVariables(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var isContinuousIntegration = Array.Exists(
            ContinuousIntegrationVariables,
            name => IsEnabled(read(name))
        );
        return new SnapshotEnvironment(
            isContinuousIntegration,
            IsOneOf(read(UpdateVariable), TrueValues)
        );
    }

    internal static SnapshotEnvironment FromProcess() =>
        FromVariables(Environment.GetEnvironmentVariable);

    private static bool IsEnabled(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !IsOneOf(value, FalseValues);

    private static bool IsOneOf(string? value, string[] candidates) =>
        value is not null
        && Array.Exists(
            candidates,
            candidate => string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase)
        );
}
