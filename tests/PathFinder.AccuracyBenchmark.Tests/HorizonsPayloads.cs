using System.Globalization;
using System.Text.Json;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

internal static class HorizonsPayloads
{
    public static string Longitudes(
        string body,
        string targetHeader,
        string timeType,
        IReadOnlyList<string> rowTimes)
    {
        var rows = rowTimes.Select((time, index) => FormattableString.Invariant(
            $" {time}, {2451545.0 + index / 24.0:0.000000000}, , , {10.0 + index / 10.0:0.0000000}, 0.0000000,"));
        var result = string.Join('\n', new[]
        {
            targetHeader,
            "Center body name: Earth (399) {source: DE441}",
            "Calendar mode   : Gregorian",
            "Atmos refraction: NO (AIRLESS)",
            $" Date__({timeType})__HR:MN:SC.fff, Date_________JDUT, , , ObsEcLon, ObsEcLat,",
            "$$SOE"
        }.Concat(rows).Concat(new[]
        {
            "$$EOE",
            "Observer-centered IAU76/80 ecliptic-of-date longitude and latitude of the",
            "target centers' apparent position, with light-time, gravitational deflection of",
            "light, and stellar aberrations.  Units: DEGREES"
        }));
        return JsonSerializer.Serialize(new
        {
            signature = new { source = HorizonsQueryBuilder.ApiSource, version = HorizonsQueryBuilder.ApiVersion },
            result
        });
    }

    public static string Vector(HorizonsQuery query)
    {
        var result = string.Join('\n',
            query.ExpectedTargetHeader,
            "Center body name: Sun (10) {source: DE441}",
            "Output units    : AU-D",
            "Calendar mode   : Gregorian",
            "Output type     : GEOMETRIC cartesian states",
            "Output format   : 2 (position and velocity)",
            "Reference frame : ICRF",
            "JDTDB, Calendar Date (TDB), X, Y, Z, VX, VY, VZ,",
            "$$SOE",
            "2451545.000000000, A.D. 2000-Jan-01 12:00:00.0000, -3.529597323721606E+00, -8.675401114502414E+00, -2.935904700117773E+00, 4.971227226758336E-03, -3.626418894486951E-03, -8.257960206970693E-04,",
            "$$EOE");
        return JsonSerializer.Serialize(new
        {
            signature = new { source = HorizonsQueryBuilder.ApiSource, version = HorizonsQueryBuilder.ApiVersion },
            result
        });
    }
}
