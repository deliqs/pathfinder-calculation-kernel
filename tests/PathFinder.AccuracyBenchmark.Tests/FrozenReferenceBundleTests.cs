using System.Text;
using PathFinder.AccuracyBenchmark.Cases;
using PathFinder.AccuracyBenchmark.References;

namespace PathFinder.AccuracyBenchmark.Tests;

public sealed class FrozenReferenceBundleTests
{
    [Fact]
    public void Load_FrozenBundle_VerifiesAndParsesEveryIndependentReference()
    {
        var cases = BenchmarkCaseManifestLoader.Load(File.ReadAllBytes(
            RepositoryPaths.File("benchmark", "cases", "cases.json")));
        var referenceRoot = RepositoryPaths.File("benchmark", "references");
        var verified = ReferenceManifestLoader.Load(
            File.ReadAllBytes(Path.Combine(referenceRoot, "manifests", "reference-manifest.json")),
            referenceRoot,
            cases);

        Assert.Equal(78, verified.ArtifactCount);
        Assert.Equal(29, verified.Manifest.Jpl.Requests.Count);
        var timings = new List<JplTimingReference>();
        foreach (var request in verified.Manifest.Jpl.Requests)
        {
            var payload = Encoding.UTF8.GetString(verified.Read(request.Response));
            if (request.Purpose == "chiron-seed-vector")
            {
                var vector = HorizonsVectorParser.Parse(payload, new HorizonsVectorExpectation(
                    request.ExpectedTargetHeader,
                    "Sun (10)",
                    "ICRF",
                    "AU-D",
                    "TDB",
                    2451545.0,
                    verified.Manifest.Jpl.ApiSource,
                    verified.Manifest.Jpl.ApiVersion));
                ChironSeedBinding.Verify(vector);
                continue;
            }

            var timeType = request.Parameters["TIME_TYPE"].Trim('\'');
            var body = cases.Positions.First(row => row.Body == request.Body);
            var rows = HorizonsResponseParser.Parse(payload, new HorizonsResponseExpectation(
                request.Body,
                body.HorizonsTargetId,
                request.ExpectedTargetHeader,
                "500@399",
                "OBSERVER",
                "31",
                timeType,
                "GREGORIAN",
                verified.Manifest.Jpl.ApiSource,
                verified.Manifest.Jpl.ApiVersion,
                request.ExpectedRowTimes));
            if (request.Purpose.StartsWith("timing:", StringComparison.Ordinal))
            {
                var caseId = request.Id["timing-".Length..];
                timings.Add(JplTimingDeriver.Derive(
                    cases.Timings.Single(row => row.Id == caseId), rows));
            }
        }

        Assert.Equal(6, timings.Count);
        var versionIdentification = Encoding.UTF8.GetString(
            verified.Read(verified.Manifest.Swiss.VersionOutput));
        Assert.Equal("Version: 2.10.03\n", versionIdentification);
        SwissOutputParser.ParseVersion(
            versionIdentification,
            verified.Manifest.Swiss.RequiredVersion);
        foreach (var request in verified.Manifest.Swiss.Requests)
        {
            Assert.Equal(12, SwissOutputParser.Parse(
                Encoding.UTF8.GetString(verified.Read(request.StandardOutput)),
                new SwissOutputExpectation(
                    request.CaseId,
                    request.HouseSystemCode,
                    request.UtJulianDate,
                    request.EastPositiveLongitude,
                    request.Latitude)).Count);
        }
    }
}
