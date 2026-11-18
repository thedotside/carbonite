namespace TheDotSide.Carbonite.TUnit.Tests;

public class ParameterizedTests
{
    private static int _attempts;

    [Test]
    [Arguments(1, 2)]
    [Arguments(10, 20)]
    public void Each_case_gets_its_own_snapshot(int left, int right)
    {
        Snapshot.Match(
            new
            {
                left,
                right,
                Sum = left + right,
            }
        );
    }

    [Test]
    [MethodDataSource(nameof(Prices))]
    public void Data_sources_name_their_cases_too(decimal price, string currency)
    {
        // An object, not $"{price} {currency}": interpolation would use the machine culture (9,99 in French).
        Snapshot.Match(new { price, currency });
    }

    [Test]
    public void Two_unnamed_snapshots_in_one_test()
    {
        Snapshot.Match(new { Step = 1 });
        Snapshot.Match(new { Step = 2 });
    }

    [Test]
    [Retry(2)]
    public async Task A_retried_test_counts_its_snapshots_from_one_again()
    {
        var attempt = Interlocked.Increment(ref _attempts);
        var context = new TUnitTestContextProvider().GetCurrent()!;

        await Assert.That(context.NextOccurrence!("probe")).IsEqualTo(1);
        Snapshot.Match(new { Value = "stable" });
        if (attempt == 1)
        {
            throw new InvalidOperationException("The first attempt fails on purpose.");
        }
    }

    [Test]
    [Repeat(2)]
    public void A_repeated_test_shares_its_snapshot()
    {
        Snapshot.Match(new { Value = "repeated" });
    }

    public static IEnumerable<(decimal, string)> Prices()
    {
        yield return (9.99m, "EUR");
        yield return (12m, "USD");
    }
}
