using System.Globalization;
using PathFinder.AccuracyBenchmark.Serialization;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class CanonicalJsonTests
{
    [Fact]
    public void Serialize_ChangingCurrentCulture_ProducesIdenticalBytes()
    {
        var value = new CanonicalFixture("zeta", 12.768878258125937, ["b", "a"]);
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");
            var first = CanonicalJson.Serialize(value);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-EG");
            var second = CanonicalJson.Serialize(value);

            Assert.Equal(first, second);
            Assert.DoesNotContain((byte)'\r', first);
            Assert.Equal((byte)'\n', first[^1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Serialize_SameValueTwice_ProducesIdenticalHash()
    {
        var value = new CanonicalFixture("alpha", 0.603682, ["a"]);

        var first = CanonicalJson.Sha256(CanonicalJson.Serialize(value));
        var second = CanonicalJson.Sha256(CanonicalJson.Serialize(value));

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    private sealed record CanonicalFixture(string Name, double Value, IReadOnlyList<string> Tags);
}
