using PathFinder.AccuracyBenchmark.Calculation;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class PublicationPrecisionTests
{
    [Theory]
    [InlineData(18.679143510627256, 18.679143510627252, 18.6791435)]
    [InlineData(40.396124137178404, 40.3961241371784, 40.3961241)]
    [InlineData(137.7987550816819, 137.79875508168192, 137.7987551)]
    [InlineData(10.29093161453461, 10.290931614831067, 10.2909316)]
    [InlineData(136.2135532246735, 136.2135532247143, 136.2135532)]
    public void LongitudeDegrees_ObservedMacAndLinuxVariantsConverge(
        double macOs,
        double linux,
        double expected)
    {
        Assert.Equal(expected, PublicationPrecision.LongitudeDegrees(macOs));
        Assert.Equal(expected, PublicationPrecision.LongitudeDegrees(linux));
    }

    [Theory]
    [InlineData(1.8611942694235495, 1.8611942694107597, 1.861)]
    [InlineData(1.517352738687805, 1.517352738738964, 1.517)]
    [InlineData(2.408452324596766, 2.4084533918390605, 2.408)]
    [InlineData(0.8574311754045993, 0.8574310284757303, 0.857)]
    public void AngularErrorArcseconds_ObservedMacAndLinuxVariantsConverge(
        double macOs,
        double linux,
        double expected)
    {
        Assert.Equal(expected, PublicationPrecision.AngularErrorArcseconds(macOs));
        Assert.Equal(expected, PublicationPrecision.AngularErrorArcseconds(linux));
    }

    [Fact]
    public void TimeMetadata_UsesDocumentedPublicationUnits()
    {
        Assert.Equal(92.968, PublicationPrecision.DeltaTSeconds(92.96818004222587));
        Assert.Equal(15.258, PublicationPrecision.TimingErrorMinutes(15.257971311066667));
    }
}
