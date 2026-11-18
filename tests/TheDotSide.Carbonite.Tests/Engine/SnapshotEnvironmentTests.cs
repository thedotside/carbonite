using TheDotSide.Carbonite.Engine;

namespace TheDotSide.Carbonite.Tests.Engine;

public class SnapshotEnvironmentTests
{
    private static SnapshotEnvironment From(params (string Name, string Value)[] variables)
    {
        var values = variables.ToDictionary(
            variable => variable.Name,
            variable => variable.Value,
            StringComparer.Ordinal
        );
        return SnapshotEnvironment.FromVariables(name => values.GetValueOrDefault(name));
    }

    [Test]
    public async Task A_developer_machine_is_neither_ci_nor_updating()
    {
        await Assert.That(From()).IsEqualTo(new SnapshotEnvironment(false, false));
    }

    [Test]
    [Arguments("CI", "true")]
    [Arguments("CI", "1")]
    [Arguments("GITHUB_ACTIONS", "true")]
    [Arguments("TF_BUILD", "True")]
    [Arguments("GITLAB_CI", "true")]
    [Arguments("JENKINS_URL", "https://jenkins.example")]
    [Arguments("TEAMCITY_VERSION", "2026.1")]
    [Arguments("BUILDKITE", "true")]
    [Arguments("CIRCLECI", "true")]
    [Arguments("TRAVIS", "true")]
    [Arguments("APPVEYOR", "True")]
    [Arguments("BITBUCKET_BUILD_NUMBER", "42")]
    [Arguments("CODEBUILD_BUILD_ID", "build:1")]
    public async Task Detects_continuous_integration(string name, string value)
    {
        await Assert.That(From((name, value)).IsContinuousIntegration).IsTrue();
    }

    [Test]
    [Arguments("false")]
    [Arguments("0")]
    [Arguments("")]
    [Arguments("off")]
    public async Task A_disabled_ci_variable_is_not_ci(string value)
    {
        await Assert.That(From(("CI", value)).IsContinuousIntegration).IsFalse();
    }

    [Test]
    [Arguments("1", true)]
    [Arguments("true", true)]
    [Arguments("TRUE", true)]
    [Arguments(" yes ", true)]
    [Arguments("on", true)]
    [Arguments("0", false)]
    [Arguments("false", false)]
    [Arguments("please", false)]
    public async Task Reads_the_update_request(string value, bool expected)
    {
        await Assert.That(From(("SNAPSHOT_UPDATE", value)).UpdateRequested).IsEqualTo(expected);
    }
}
