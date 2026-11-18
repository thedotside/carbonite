using TheDotSide.Carbonite.Naming;

namespace TheDotSide.Carbonite.Tests.Naming;

public class SourcePathResolverTests
{
    private static readonly string Work = Path.Combine(Path.GetTempPath(), "work", "repo");

    private static SourcePathResolver Resolver(string[] existingFiles, params string[] projects)
    {
        var files = new HashSet<string>(existingFiles, StringComparer.Ordinal);
        return new SourcePathResolver(files.Contains, () => projects);
    }

    [Test]
    public async Task Returns_an_existing_path_unchanged()
    {
        var file = Path.Combine(Work, "tests", "BarTests.cs");

        await Assert.That(Resolver([file]).Resolve(file, null)).IsEqualTo(file);
    }

    [Test]
    [Arguments("/_/tests/Foo/BarTests.cs")]
    [Arguments("/_/tests\\Foo\\BarTests.cs")]
    [Arguments("/_1/Foo/BarTests.cs")]
    public async Task Maps_a_deterministic_build_path_back_under_the_project_directory(
        string mapped
    )
    {
        var project = Path.Combine(Work, "tests", "Foo");
        var file = Path.Combine(project, "BarTests.cs");

        await Assert.That(Resolver([file], project).Resolve(mapped, null)).IsEqualTo(file);
    }

    [Test]
    public async Task Prefers_the_longest_matching_path()
    {
        var project = Path.Combine(Work, "tests", "Foo");
        var nested = Path.Combine(project, "Sub", "BarTests.cs");
        var other = Path.Combine(project, "BarTests.cs");

        await Assert
            .That(Resolver([nested, other], project).Resolve("/_/tests/Foo/Sub/BarTests.cs", null))
            .IsEqualTo(nested);
    }

    [Test]
    public async Task Explains_how_to_fix_an_unresolvable_path()
    {
        var exception = Assert.Throws<SnapshotException>(() =>
            Resolver([], Path.Combine(Work, "tests")).Resolve("/_/tests/Missing.cs", null)
        );
        var empty = Assert.Throws<SnapshotException>(() => Resolver([]).Resolve("", null));

        await Assert.That(exception.Message).Contains("/_/tests/Missing.cs");
        await Assert.That(exception.Message).Contains("ContinuousIntegrationBuild");
        await Assert.That(empty.Message).Contains("source file");
    }

    [Test]
    public async Task The_build_targets_record_the_test_project_directory()
    {
        var directories = SourcePathResolver
            .ProjectDirectoriesOf(typeof(SourcePathResolverTests).Assembly)
            .ToList();

        await Assert.That(directories).Count().IsEqualTo(1);
        await Assert.That(Path.GetFileName(directories[0])).IsEqualTo("TheDotSide.Carbonite.Tests");
    }

    [Test]
    public async Task The_default_resolver_finds_this_file_from_a_deterministic_path()
    {
        var mapped = "/_/tests/TheDotSide.Carbonite.Tests/Naming/SourcePathResolverTests.cs";

        var resolved = SourcePathResolver.Default.Resolve(mapped, null);

        await Assert.That(File.Exists(resolved)).IsTrue();
        await Assert.That(resolved).EndsWith(Path.Combine("Naming", "SourcePathResolverTests.cs"));
    }
}
