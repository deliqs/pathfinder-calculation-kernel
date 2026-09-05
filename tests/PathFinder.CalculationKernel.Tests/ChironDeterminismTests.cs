using NodaTime;
using PathFinder.CalculationKernel;
using PathFinder.CalculationKernel.Ephemeris;

namespace PathFinder.CalculationKernel.Tests;

public sealed class ChironDeterminismTests
{
    private static readonly Instant[] PublishedEpochs =
    [
        Instant.FromUtc(1950, 1, 1, 0, 0),
        Instant.FromUtc(2000, 1, 1, 12, 0),
        Instant.FromUtc(2024, 1, 1, 12, 0),
        Instant.FromUtc(2050, 1, 1, 0, 0)
    ];

    [Fact]
    public void GetLongitude_ChironAfterPreJ2000Query_MatchesFreshInstanceExactly()
    {
        var sharedEphemeris = new AstronomyEngineEphemeris();
        _ = sharedEphemeris.GetLongitude(Planet.Chiron, PublishedEpochs[0]);
        var expected = new AstronomyEngineEphemeris()
            .GetLongitude(Planet.Chiron, PublishedEpochs[2]);

        var actual = sharedEphemeris.GetLongitude(Planet.Chiron, PublishedEpochs[2]);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(EvaluationOrders))]
    public void GetLongitude_ChironAcrossEvaluationOrders_MatchesFreshInstances(
        int[] evaluationOrder)
    {
        var expected = PublishedEpochs
            .Select(epoch => new AstronomyEngineEphemeris().GetLongitude(Planet.Chiron, epoch))
            .ToArray();
        var sharedEphemeris = new AstronomyEngineEphemeris();

        foreach (var index in evaluationOrder)
        {
            var actual = sharedEphemeris.GetLongitude(Planet.Chiron, PublishedEpochs[index]);

            Assert.Equal(expected[index], actual);
        }
    }

    public static TheoryData<int[]> EvaluationOrders => new()
    {
        { [0, 1, 2, 3] },
        { [3, 2, 1, 0] },
        { [0, 2, 3, 1] }
    };
}
