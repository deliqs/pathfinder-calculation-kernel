using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class HorizonsQueryBuilderTests
{
    [Fact]
    public void Build_PositionQueries_PinAllReferenceParametersAndTimeScales()
    {
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));

        var queries = HorizonsQueryBuilder.Build(cases);

        Assert.Equal(23, queries.Count);
        var nominal = queries.Single(query => query.Id == "sun-positions-ut");
        Assert.Equal("'10'", nominal.Parameters["COMMAND"]);
        Assert.Equal("'500@399'", nominal.Parameters["CENTER"]);
        Assert.Equal("'OBSERVER'", nominal.Parameters["EPHEM_TYPE"]);
        Assert.Equal("'31'", nominal.Parameters["QUANTITIES"]);
        Assert.Equal("'UT'", nominal.Parameters["TIME_TYPE"]);
        Assert.Equal("'GREGORIAN'", nominal.Parameters["CAL_TYPE"]);
        Assert.Equal("'AIRLESS'", nominal.Parameters["APPARENT"]);
        Assert.Equal("'ICRF'", nominal.Parameters["REF_SYSTEM"]);
        Assert.Equal("'YES'", nominal.Parameters["EXTRA_PREC"]);
        Assert.Contains("'2050-Jan-01 00:00:00.000'", nominal.Parameters["TLIST"], StringComparison.Ordinal);

        var matched = queries.Single(query => query.Id == "moon-positions-tt");
        Assert.Equal("'TT'", matched.Parameters["TIME_TYPE"]);
        Assert.Contains("'2050-Jan-01 00:01:32.968'", matched.Parameters["TLIST"], StringComparison.Ordinal);
        Assert.Contains("COMMAND=%27301%27", matched.RequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ChironQueriesPinNumericIdentitySolutionAndSeedVectorFrame()
    {
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));

        var queries = HorizonsQueryBuilder.Build(cases);
        var nominal = queries.Single(query => query.Id == "chiron-positions-ut");
        var seed = queries.Single(query => query.Id == HorizonsQueryBuilder.ChironSeedReferenceId);

        Assert.Equal("'2060;'", nominal.Parameters["COMMAND"]);
        Assert.Equal(
            "Target body name: 2060 Chiron (1977 UB) {source: JPL#171}",
            nominal.ExpectedTargetHeader);
        Assert.Equal("'2060;'", seed.Parameters["COMMAND"]);
        Assert.Equal("'500@10'", seed.Parameters["CENTER"]);
        Assert.Equal("'VECTORS'", seed.Parameters["EPHEM_TYPE"]);
        Assert.Equal("'TDB'", seed.Parameters["TIME_TYPE"]);
        Assert.Equal("'FRAME'", seed.Parameters["REF_PLANE"]);
        Assert.Equal("'ICRF'", seed.Parameters["REF_SYSTEM"]);
        Assert.Equal("'NONE'", seed.Parameters["VEC_CORR"]);
        Assert.Equal("'AU-D'", seed.Parameters["OUT_UNITS"]);
        Assert.Equal("'2451545.0'", seed.Parameters["TLIST"]);
        Assert.Equal(
            "jpl-horizons-chiron-seed-jpl171-j2000",
            HorizonsQueryBuilder.ChironSeedReferenceId);
    }

    [Fact]
    public void BuildTiming_ConsumesBodyTargetMotionWindowAndMethodFromEveryCase()
    {
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));

        var queries = HorizonsQueryBuilder.BuildTimings(cases);

        Assert.Equal(6, queries.Count);
        var crossing = queries.Single(query => query.CaseId ==
            "mercury-20-aries-retrograde");
        Assert.Equal("Mercury", crossing.Body);
        Assert.Equal("retrograde", crossing.Motion);
        Assert.Equal(20, crossing.TargetLongitudeDeg);
        Assert.Equal(20, crossing.SearchWindowDays);
        Assert.Equal("kernel-longitude-crossing", crossing.Method);
        Assert.Equal("'199'", crossing.Parameters["COMMAND"]);
        Assert.Equal("'2024-04-01 00:00:00'", crossing.Parameters["START_TIME"]);
        Assert.Equal("'2024-04-21 00:00:00'", crossing.Parameters["STOP_TIME"]);
        Assert.Equal("'1 h'", crossing.Parameters["STEP_SIZE"]);
        Assert.Equal(20 * 24 + 1, crossing.ExpectedRowTimes.Count);
        Assert.Equal("2024-Apr-01 00:00:00.000", crossing.ExpectedRowTimes[0]);
        Assert.Equal("2024-Apr-21 00:00:00.000", crossing.ExpectedRowTimes[^1]);

        var station = queries.Single(query => query.CaseId == "saturn-june-retrograde-station");
        Assert.Equal("Saturn", station.Body);
        Assert.Equal("maximum", station.Extremum);
        Assert.Equal("kernel-station-parabolic-10-minute", station.Method);
        Assert.Equal("'699'", station.Parameters["COMMAND"]);
        Assert.Equal("'10 m'", station.Parameters["STEP_SIZE"]);
        Assert.Equal("'AIRLESS'", station.Parameters["APPARENT"]);
        Assert.Equal("'ICRF'", station.Parameters["REF_SYSTEM"]);
        Assert.Equal("'YES'", station.Parameters["EXTRA_PREC"]);
        Assert.Equal("'2024-07-03 00:00:00'", station.Parameters["STOP_TIME"]);
        Assert.Equal(7 * 24 * 6 + 1, station.ExpectedRowTimes.Count);
    }
}
