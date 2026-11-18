namespace TheDotSide.Carbonite.TUnit.Tests;

[Arguments("fr")]
[Arguments("en")]
public class LocalizedTests(string language)
{
    [Test]
    public void Class_arguments_name_the_case()
    {
        Snapshot.Match(
            new
            {
                language,
                Greeting = string.Equals(language, "fr", StringComparison.Ordinal)
                    ? "Bonjour"
                    : "Hello",
            }
        );
    }
}
