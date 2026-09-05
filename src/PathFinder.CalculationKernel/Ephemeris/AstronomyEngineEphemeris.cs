using CosineKitty;
using NodaTime;

namespace PathFinder.CalculationKernel.Ephemeris;

/// <summary>
/// Calculates PathFinder's fourteen benchmark bodies with Astronomy Engine 2.1.19 and the
/// published mean-node, mean-Lilith, and gravity-simulated Chiron conventions.
/// </summary>
public sealed class AstronomyEngineEphemeris : IEphemeris
{
    private readonly ChironCalculator _chironCalculator = new();

    public PlanetPosition GetPlanetPosition(Planet planet, Instant utcMoment)
    {
        var position = GetPositionOnly(planet, utcMoment);
        return new PlanetPosition
        {
            Planet = planet,
            Position = position,
            DailyMotion = ComputeDailyMotion(planet, utcMoment, position)
        };
    }

    public double GetLongitude(Planet planet, Instant utcMoment) =>
        GetPositionOnly(planet, utcMoment).Longitude;

    public IReadOnlyList<PlanetPosition> GetAllPlanetPositions(Instant utcMoment) =>
        Enum.GetValues<Planet>()
            .Select(planet => GetPlanetPosition(planet, utcMoment))
            .ToArray();

    public double GetDailyMotion(Planet planet, Instant utcMoment) =>
        ComputeDailyMotion(planet, utcMoment, GetPositionOnly(planet, utcMoment));

    public Instant FindPlanetAtLongitude(
        Planet planet,
        double targetLongitude,
        Instant searchStart,
        int searchDaysForward = 400) =>
        planet == Planet.Sun
            ? LongitudeSearch.FindSun(targetLongitude, searchStart, searchDaysForward, ToAstroTime)
            : LongitudeSearch.FindPlanet(
                planet,
                targetLongitude,
                searchStart,
                searchDaysForward,
                GetPositionOnly);

    internal static AstroTime ToAstroTime(Instant instant)
    {
        var utc = instant.InUtc();
        return new AstroTime(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            utc.Second + utc.Millisecond / 1000.0);
    }

    private static Body MapBody(Planet planet) => planet switch
    {
        Planet.Sun => Body.Sun,
        Planet.Mercury => Body.Mercury,
        Planet.Venus => Body.Venus,
        Planet.Mars => Body.Mars,
        Planet.Jupiter => Body.Jupiter,
        Planet.Saturn => Body.Saturn,
        Planet.Uranus => Body.Uranus,
        Planet.Neptune => Body.Neptune,
        Planet.Pluto => Body.Pluto,
        _ => throw new NotSupportedException($"Planet {planet} is not an Astronomy Engine body.")
    };

    private static EclipticPosition CalculateStandardPosition(Planet planet, AstroTime time)
    {
        var vector = Astronomy.GeoVector(MapBody(planet), time, Aberration.Corrected);
        var ecliptic = Astronomy.EquatorialToEcliptic(vector);
        return new EclipticPosition
        {
            Longitude = ecliptic.elon,
            Latitude = ecliptic.elat,
            Distance = vector.Length()
        };
    }

    private static EclipticPosition CalculateMoonPosition(AstroTime time)
    {
        var moon = Astronomy.EclipticGeoMoon(time);
        return new EclipticPosition
        {
            Longitude = moon.lon,
            Latitude = moon.lat,
            Distance = moon.dist
        };
    }

    private double ComputeDailyMotion(
        Planet planet,
        Instant utcMoment,
        EclipticPosition currentPosition)
    {
        var nextPosition = GetPositionOnly(planet, utcMoment + Duration.FromDays(1));
        var difference = nextPosition.Longitude - currentPosition.Longitude;
        if (difference > 180)
        {
            difference -= 360;
        }

        if (difference < -180)
        {
            difference += 360;
        }

        return difference;
    }

    private EclipticPosition GetPositionOnly(Planet planet, Instant utcMoment)
    {
        var time = ToAstroTime(utcMoment);
        return planet switch
        {
            Planet.Moon => CalculateMoonPosition(time),
            Planet.NorthNode => CalculatedPointEphemeris.MeanNorthNode(time),
            Planet.SouthNode => CalculatedPointEphemeris.MeanSouthNode(time),
            Planet.Lilith => CalculatedPointEphemeris.MeanLilith(time),
            Planet.Chiron => _chironCalculator.Calculate(time),
            _ => CalculateStandardPosition(planet, time)
        };
    }
}
