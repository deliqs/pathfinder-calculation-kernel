using NodaTime;
using PathFinder.CalculationKernel.Ephemeris;

namespace PathFinder.CalculationKernel.Tests;

public class AstronomyEngineTimeTests
{
    [Fact]
    public void GetMetadata_2050Instant_ExposesExactKernelDeltaTConvention()
    {
        var instant = Instant.FromUtc(2050, 1, 1, 0, 0);

        var metadata = AstronomyEngineTime.GetMetadata(instant);

        Assert.Equal("AstronomyEngine-2.1.19:Espenak-Meeus", metadata.Convention);
        Assert.Equal(92.9681800422259, metadata.DeltaTSeconds, 9);
    }
}
