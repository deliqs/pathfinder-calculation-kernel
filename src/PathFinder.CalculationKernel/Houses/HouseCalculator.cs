using NodaTime;

namespace PathFinder.CalculationKernel.Houses;

/// <summary>
/// Calculates the seven supported kernel house systems. The published external-reference
/// benchmark is scoped to Placidus. Fixed obliquity and the truncated GMST series are deliberate.
/// </summary>
public sealed class HouseCalculator
{
    private const double Obliquity = 23.4393;
    private const double J2000JulianDate = 2451545.0;
    private const double UnixEpochJulianDate = 2440587.5;

    public IReadOnlyList<HouseCusp> CalculateHouses(
        Instant utcMoment,
        GeoLocation location,
        HouseSystem system)
    {
        var ascendant = CalculateAscendant(utcMoment, location).Longitude;
        var midheaven = CalculateMidheaven(utcMoment, location).Longitude;

        return CalculateHousesCore(ascendant, midheaven, location.Latitude, system);
    }

    public EclipticPosition CalculateAscendant(Instant utcMoment, GeoLocation location)
    {
        var localSiderealTime = CalculateLocalSiderealTime(utcMoment, location.Longitude);
        return new EclipticPosition
        {
            Longitude = CalculateAscendantLongitude(localSiderealTime, location.Latitude)
        };
    }

    public EclipticPosition CalculateMidheaven(Instant utcMoment, GeoLocation location)
    {
        var lst = AstroMath.DegreesToRadians(
            CalculateLocalSiderealTime(utcMoment, location.Longitude));
        var epsilon = AstroMath.DegreesToRadians(Obliquity);
        var longitude = AstroMath.NormalizeDegrees(AstroMath.RadiansToDegrees(
            Math.Atan2(Math.Sin(lst), Math.Cos(lst) * Math.Cos(epsilon))));
        return new EclipticPosition { Longitude = longitude };
    }

    internal EclipticPosition CalculateAscendantFromMc(double mcLongitude, double latitude)
    {
        var ramc = AstroMath.CalculateRamc(mcLongitude, Obliquity);
        var ascLongitude = CalculateAscendantLongitude(ramc, latitude);
        return new EclipticPosition { Longitude = ascLongitude };
    }

    internal IReadOnlyList<HouseCusp> CalculateHousesFromMc(
        double mcLongitude,
        double latitude,
        HouseSystem system)
    {
        var ascLongitude = CalculateAscendantFromMc(mcLongitude, latitude).Longitude;
        return CalculateHousesCore(ascLongitude, mcLongitude, latitude, system);
    }

    private static double CalculateLocalSiderealTime(Instant utcMoment, double observerLongitude)
    {
        var julianDate = UnixEpochJulianDate + utcMoment.ToUnixTimeSeconds() / 86400.0;
        var gmst = 280.46061837 + 360.98564736629 * (julianDate - J2000JulianDate);
        return AstroMath.NormalizeDegrees(gmst + observerLongitude);
    }

    private static double CalculateAscendantLongitude(double ramc, double latitude)
    {
        var lst = AstroMath.DegreesToRadians(ramc);
        var epsilon = AstroMath.DegreesToRadians(Obliquity);
        var phi = AstroMath.DegreesToRadians(latitude);
        var y = Math.Cos(lst);
        var x = -(Math.Sin(epsilon) * Math.Tan(phi) + Math.Cos(epsilon) * Math.Sin(lst));
        return AstroMath.NormalizeDegrees(AstroMath.RadiansToDegrees(Math.Atan2(y, x)));
    }

    private static IReadOnlyList<HouseCusp> CalculateHousesCore(
        double ascendant,
        double midheaven,
        double latitude,
        HouseSystem system)
    {
        return system switch
        {
            HouseSystem.WholeSign => CalculateWholeSignHouses(ascendant),
            HouseSystem.Equal => CalculateEqualHouses(ascendant),
            HouseSystem.Porphyry => CalculatePorphyryHouses(ascendant, midheaven),
            HouseSystem.Placidus => PlacidusHouses.Calculate(
                ascendant,
                midheaven,
                latitude,
                CalculatePorphyryHouses),
            HouseSystem.Koch => KochHouses.Calculate(
                ascendant,
                midheaven,
                latitude,
                CalculateAscendantLongitude,
                CalculatePorphyryHouses),
            HouseSystem.Regiomontanus => RegiomontanusHouses.Calculate(
                ascendant,
                midheaven,
                latitude,
                CalculatePorphyryHouses),
            HouseSystem.Campanus => CampanusHouses.Calculate(
                ascendant,
                midheaven,
                latitude,
                CalculatePorphyryHouses),
            _ => throw new NotSupportedException($"House system {system} is not supported.")
        };
    }

    private static List<HouseCusp> CalculateWholeSignHouses(double ascendant)
    {
        var firstCusp = (int)(ascendant / 30.0) * 30.0;
        return CreateCusps(Enumerable.Range(0, 12).Select(index => firstCusp + index * 30.0));
    }

    private static List<HouseCusp> CalculateEqualHouses(double ascendant) =>
        CreateCusps(Enumerable.Range(0, 12).Select(index => ascendant + index * 30.0));

    private static List<HouseCusp> CalculatePorphyryHouses(double ascendant, double midheaven)
    {
        var descendant = AstroMath.NormalizeDegrees(ascendant + 180.0);
        var imumCoeli = AstroMath.NormalizeDegrees(midheaven + 180.0);
        var quadrants = new[]
        {
            AstroMath.NormalizeDegrees(ascendant - midheaven),
            AstroMath.NormalizeDegrees(imumCoeli - ascendant),
            AstroMath.NormalizeDegrees(descendant - imumCoeli),
            AstroMath.NormalizeDegrees(midheaven - descendant)
        };
        var cusps = new double[12];
        cusps[0] = ascendant;
        cusps[3] = imumCoeli;
        cusps[6] = descendant;
        cusps[9] = midheaven;
        cusps[10] = midheaven + quadrants[0] / 3.0;
        cusps[11] = midheaven + 2.0 * quadrants[0] / 3.0;
        cusps[1] = ascendant + quadrants[1] / 3.0;
        cusps[2] = ascendant + 2.0 * quadrants[1] / 3.0;
        cusps[4] = imumCoeli + quadrants[2] / 3.0;
        cusps[5] = imumCoeli + 2.0 * quadrants[2] / 3.0;
        cusps[7] = descendant + quadrants[3] / 3.0;
        cusps[8] = descendant + 2.0 * quadrants[3] / 3.0;
        return CreateCusps(cusps);
    }

    private static List<HouseCusp> CreateCusps(IEnumerable<double> longitudes) =>
        longitudes.Select((longitude, index) => new HouseCusp
        {
            HouseNumber = index + 1,
            CuspPosition = new EclipticPosition
            {
                Longitude = AstroMath.NormalizeDegrees(longitude)
            }
        }).ToList();
}
