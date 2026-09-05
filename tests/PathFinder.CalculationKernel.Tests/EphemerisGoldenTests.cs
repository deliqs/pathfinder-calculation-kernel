using NodaTime;
using NodaTime.Text;
using PathFinder.CalculationKernel;
using PathFinder.CalculationKernel.Ephemeris;

namespace PathFinder.CalculationKernel.Tests;

public class EphemerisGoldenTests
{
    private readonly AstronomyEngineEphemeris _ephemeris = new();

    [Fact]
    public void GetLongitude_PublishedBenchmarkCases_MatchCharacterizedOutputs()
    {
        Assert.Equal(44, PublishedPositionRows.Count);

        foreach (var (body, utc, expectedLongitude) in PublishedPositionRows)
        {
            var instant = InstantPattern.ExtendedIso.Parse(utc).Value;
            var actual = _ephemeris.GetLongitude(body, instant);

            Assert.Equal(expectedLongitude, actual, 9);
        }
    }

    [Fact]
    public void GetAllPlanetPositions_OneInstant_ReturnsTheFourteenSupportedBodies()
    {
        var instant = Instant.FromUtc(2024, 1, 1, 12, 0);

        var positions = _ephemeris.GetAllPlanetPositions(instant);

        Assert.Equal(Enum.GetValues<Planet>(), positions.Select(position => position.Planet));
    }

    [Theory]
    [InlineData(Planet.NorthNode, 125.0444808687064)]
    [InlineData(Planet.SouthNode, 305.0444808687064)]
    [InlineData(Planet.Lilith, 263.3533288239676)]
    public void GetLongitude_MeanCalculatedPointAtJ2000_PreservesConvention(
        Planet body,
        double expectedLongitude)
    {
        // Mean-node and mean-apogee formulas are deliberate PathFinder conventions.
        var instant = Instant.FromUtc(2000, 1, 1, 12, 0);

        var actual = _ephemeris.GetLongitude(body, instant);

        Assert.Equal(expectedLongitude, actual, 9);
    }

    private static IReadOnlyList<(Planet Body, string Utc, double Longitude)> PublishedPositionRows =>
    [
        (Planet.Sun, "1950-01-01T00:00:00Z", 280.00468734384907),
        (Planet.Sun, "2000-01-01T12:00:00Z", 280.3687386398185),
        (Planet.Sun, "2024-01-01T12:00:00Z", 280.54852712121806),
        (Planet.Sun, "2050-01-01T00:00:00Z", 280.74850900125466),
        (Planet.Moon, "1950-01-01T00:00:00Z", 61.415389043285934),
        (Planet.Moon, "2000-01-01T12:00:00Z", 223.32389112747416),
        (Planet.Moon, "2024-01-01T12:00:00Z", 161.90773694497042),
        (Planet.Moon, "2050-01-01T00:00:00Z", 18.679143510627256),
        (Planet.Mercury, "1950-01-01T00:00:00Z", 299.44697211853463),
        (Planet.Mercury, "2000-01-01T12:00:00Z", 271.88891206646304),
        (Planet.Mercury, "2024-01-01T12:00:00Z", 262.2107926218511),
        (Planet.Mercury, "2050-01-01T00:00:00Z", 270.059740825464),
        (Planet.Venus, "1950-01-01T00:00:00Z", 316.9792598140073),
        (Planet.Venus, "2000-01-01T12:00:00Z", 241.56523294036205),
        (Planet.Venus, "2024-01-01T12:00:00Z", 243.2204270975457),
        (Planet.Venus, "2050-01-01T00:00:00Z", 281.2485917855707),
        (Planet.Mars, "1950-01-01T00:00:00Z", 182.212262029853),
        (Planet.Mars, "2000-01-01T12:00:00Z", 327.96389907790285),
        (Planet.Mars, "2024-01-01T12:00:00Z", 267.6793880534845),
        (Planet.Mars, "2050-01-01T00:00:00Z", 227.714706655574),
        (Planet.Jupiter, "1950-01-01T00:00:00Z", 306.50353485196393),
        (Planet.Jupiter, "2000-01-01T12:00:00Z", 25.25419984487424),
        (Planet.Jupiter, "2024-01-01T12:00:00Z", 35.58285330824777),
        (Planet.Jupiter, "2050-01-01T00:00:00Z", 121.69152391644337),
        (Planet.Saturn, "1950-01-01T00:00:00Z", 169.4369526079382),
        (Planet.Saturn, "2000-01-01T12:00:00Z", 40.396124137178404),
        (Planet.Saturn, "2024-01-01T12:00:00Z", 333.2869084412452),
        (Planet.Saturn, "2050-01-01T00:00:00Z", 297.57324930074554),
        (Planet.Uranus, "1950-01-01T00:00:00Z", 92.68287246742689),
        (Planet.Uranus, "2000-01-01T12:00:00Z", 314.8061046271608),
        (Planet.Uranus, "2024-01-01T12:00:00Z", 49.373057663582706),
        (Planet.Uranus, "2050-01-01T00:00:00Z", 170.7335824116723),
        (Planet.Neptune, "1950-01-01T00:00:00Z", 197.26418961818885),
        (Planet.Neptune, "2000-01-01T12:00:00Z", 303.1954420594734),
        (Planet.Neptune, "2024-01-01T12:00:00Z", 355.08681389378523),
        (Planet.Neptune, "2050-01-01T00:00:00Z", 53.60266335711403),
        (Planet.Pluto, "1950-01-01T00:00:00Z", 137.7987550816819),
        (Planet.Pluto, "2000-01-01T12:00:00Z", 251.45473515991364),
        (Planet.Pluto, "2024-01-01T12:00:00Z", 299.37288059066),
        (Planet.Pluto, "2050-01-01T00:00:00Z", 337.5317758898643),
        (Planet.Chiron, "1950-01-01T00:00:00Z", 255.79805387442764),
        (Planet.Chiron, "2000-01-01T12:00:00Z", 251.6245580324396),
        (Planet.Chiron, "2024-01-01T12:00:00Z", 15.464574353619973),
        (Planet.Chiron, "2050-01-01T00:00:00Z", 246.58069336574965)
    ];
}
