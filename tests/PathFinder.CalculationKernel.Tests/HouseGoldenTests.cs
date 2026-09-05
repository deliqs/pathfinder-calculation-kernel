using NodaTime;
using NodaTime.Text;
using PathFinder.CalculationKernel.Houses;

namespace PathFinder.CalculationKernel.Tests;

public class HouseGoldenTests
{
    private readonly HouseCalculator _calculator = new();

    [Fact]
    public void CalculateHouses_PublishedBenchmarkCases_MatchCharacterizedCusps()
    {
        // Characterized from the published PathFinder benchmark inputs. Swiss values and
        // tolerances are independently archived by the benchmark runner, not used as this oracle.
        Assert.Equal(228, PublishedCases.Sum(testCase => testCase.Longitudes.Count));
        Assert.Contains(PublishedCases, testCase => testCase.System == HouseSystem.Koch);
        Assert.Contains(PublishedCases, testCase => testCase.System == HouseSystem.Regiomontanus);
        Assert.Contains(PublishedCases, testCase => testCase.System == HouseSystem.Campanus);

        foreach (var testCase in PublishedCases)
        {
            var instant = InstantPattern.ExtendedIso.Parse(testCase.Utc).Value;
            var location = new GeoLocation
            {
                Latitude = testCase.Latitude,
                Longitude = testCase.Longitude,
                TimezoneId = "UTC"
            };

            var cusps = _calculator.CalculateHouses(instant, location, testCase.System);

            Assert.Equal(12, cusps.Count);
            for (var index = 0; index < cusps.Count; index++)
            {
                Assert.Equal(index + 1, cusps[index].HouseNumber);
                Assert.Equal(testCase.Longitudes[index], cusps[index].CuspPosition.Longitude, 9);
            }
        }
    }

    [Fact]
    public void CalculateHouses_PolarPlacidusCase_EqualsPorphyryFallback()
    {
        var instant = Instant.FromUtc(2024, 6, 21, 12, 0);
        var tromso = new GeoLocation
        {
            Latitude = 69.6492,
            Longitude = 18.9553,
            TimezoneId = "Europe/Oslo"
        };

        var placidus = _calculator.CalculateHouses(instant, tromso, HouseSystem.Placidus);
        var porphyry = _calculator.CalculateHouses(instant, tromso, HouseSystem.Porphyry);

        Assert.Equal(
            porphyry.Select(cusp => cusp.CuspPosition.Longitude),
            placidus.Select(cusp => cusp.CuspPosition.Longitude));
    }

    [Theory]
    [InlineData(HouseSystem.Placidus)]
    [InlineData(HouseSystem.WholeSign)]
    [InlineData(HouseSystem.Equal)]
    [InlineData(HouseSystem.Koch)]
    [InlineData(HouseSystem.Regiomontanus)]
    [InlineData(HouseSystem.Campanus)]
    [InlineData(HouseSystem.Porphyry)]
    public void CalculateHouses_SupportedSystem_ReturnsTwelveNumberedCusps(HouseSystem system)
    {
        var location = new GeoLocation
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            TimezoneId = "Europe/London"
        };

        var cusps = _calculator.CalculateHouses(
            Instant.FromUtc(2000, 1, 1, 0, 0),
            location,
            system);

        Assert.Equal(Enumerable.Range(1, 12), cusps.Select(cusp => cusp.HouseNumber));
        Assert.All(cusps, cusp => Assert.InRange(cusp.CuspPosition.Longitude, 0, 360));
    }

    private static IReadOnlyList<HouseCase> PublishedCases =>
    [
        new(
            "2000-01-01T00:00:00Z",
            51.5074,
            -0.1278,
            HouseSystem.Placidus,
            [186.93906153606568, 211.62739612330878, 242.47795329737852,
             279.04201845564137, 314.6361509343626, 343.9166598989988,
             6.939061536065651, 31.627396123308813, 62.47795329737852,
             99.04201845564138, 134.6361509343626, 163.91665989899877]),
        new(
            "2024-04-08T18:00:00Z",
            40.7128,
            -74.006,
            HouseSystem.Placidus,
            [135.48789278034957, 156.95269406902958, 183.3723099918528,
             215.78590126217858, 251.84740458323773, 286.0263345085102,
             315.48789278034957, 336.9526940690296, 3.3723099918528305,
             35.78590126217858, 71.84740458323772, 106.02633450851022]),
        new(
            "1950-01-01T00:00:00Z",
            -33.8688,
            151.2093,
            HouseSystem.Placidus,
            [344.2270512557373, 10.29093161453461, 40.269594699897524,
             72.73369994543663, 105.37145790250065, 136.2135532246735,
             164.22705125573725, 190.2909316145346, 220.26959469989754,
             252.73369994543663, 285.37145790250065, 316.2135532246735]),
        new(
            "2024-06-21T12:00:00Z",
            69.6492,
            18.9553,
            HouseSystem.Placidus,
            [189.59291617262681, 222.28038968110926, 254.9678631895917,
             287.65533669807417, 314.9678631895917, 342.2803896811093,
             9.592916172626815, 42.280389681109256, 74.9678631895917,
             107.65533669807414, 134.9678631895917, 162.28038968110926]),
        new(
            "2000-01-01T00:00:00Z",
            51.5074,
            -0.1278,
            HouseSystem.Koch,
            [186.93906153606568, 215.56852291702543, 244.52979644088109,
             279.04201845564137, 309.61677944207702, 338.19061064821813,
             6.9390615360656511, 35.568522917025462, 64.529796440881114,
             99.042018455641383, 129.61677944207702, 158.19061064821813]),
        new(
            "2000-01-01T00:00:00Z",
            51.5074,
            -0.1278,
            HouseSystem.Regiomontanus,
            [186.93906153606568, 209.38480997387961, 238.89879588434133,
             279.04201845564137, 317.49789182311042, 345.09120008947821,
             6.9390615360656511, 29.384809973879612, 58.898795884341325,
             99.042018455641383, 137.49789182311042, 165.09120008947821]),
        new(
            "2000-01-01T00:00:00Z",
            51.5074,
            -0.1278,
            HouseSystem.Campanus,
            [186.93906153606568, 220.75634623394146, 251.61917311987816,
             279.04201845564137, 305.59842818193187, 334.38210940198405,
             6.9390615360656511, 40.756346233941485, 71.619173119878155,
             99.042018455641383, 125.59842818193187, 154.38210940198405]),
        new(
            "2024-04-08T18:00:00Z",
            40.7128,
            -74.006,
            HouseSystem.Koch,
            [135.48789278034957, 162.12344802348014, 189.0424550734428,
             215.78590126217858, 257.91654086143313, 288.50173013419135,
             315.48789278034957, 342.12344802348014, 9.0424550734427953,
             35.785901262178577, 77.916540861433148, 108.50173013419133]),
        new(
            "2024-04-08T18:00:00Z",
            40.7128,
            -74.006,
            HouseSystem.Regiomontanus,
            [135.48789278034957, 158.2175606935634, 183.19570756826963,
             215.78590126217858, 255.07332845115468, 289.4312130633823,
             315.48789278034957, 338.21756069356343, 3.1957075682696541,
             35.785901262178577, 75.07332845115468, 109.43121306338227]),
        new(
            "2024-04-08T18:00:00Z",
            40.7128,
            -74.006,
            HouseSystem.Campanus,
            [135.48789278034957, 163.8708835495581, 189.32467224616454,
             215.78590126217858, 246.74224779767962, 281.95297536454302,
             315.48789278034957, 343.87088354955813, 9.324672246164539,
             35.785901262178577, 66.742247797679624, 101.95297536454299]),
        new(
            "1950-01-01T00:00:00Z",
            -33.8688,
            151.2093,
            HouseSystem.Koch,
            [344.22705125573731, 14.010227527438813, 43.409209337937511,
             72.733699945436626, 105.47825625537644, 134.86292536727103,
             164.22705125573725, 194.01022752743881, 223.40920933793751,
             252.73369994543663, 285.47825625537644, 314.86292536727103]),
        new(
            "1950-01-01T00:00:00Z",
            -33.8688,
            151.2093,
            HouseSystem.Regiomontanus,
            [344.22705125573731, 9.8166040600926276, 38.722008243803174,
             72.733699945436626, 107.70316815101705, 138.06864920113935,
             164.22705125573725, 189.81660406009263, 218.72200824380317,
             252.73369994543663, 287.70316815101705, 318.06864920113935]),
        new(
            "1950-01-01T00:00:00Z",
            -33.8688,
            151.2093,
            HouseSystem.Campanus,
            [344.22705125573731, 14.138666138226775, 43.398617075165944,
             72.733699945436626, 102.80209832848294, 133.55549593941379,
             164.22705125573725, 194.13866613822677, 223.39861707516593,
             252.73369994543663, 282.80209832848294, 313.55549593941379]),
        new(
            "2024-06-21T12:00:00Z",
            69.6492,
            18.9553,
            HouseSystem.Koch,
            [189.59291617262681, 222.28038968110926, 254.9678631895917,
             287.65533669807417, 314.9678631895917, 342.2803896811093,
             9.592916172626815, 42.280389681109256, 74.9678631895917,
             107.65533669807414, 134.9678631895917, 162.28038968110926]),
        new(
            "2024-06-21T12:00:00Z",
            69.6492,
            18.9553,
            HouseSystem.Regiomontanus,
            [189.59291617262681, 222.28038968110926, 254.9678631895917,
             287.65533669807417, 314.9678631895917, 342.2803896811093,
             9.592916172626815, 42.280389681109256, 74.9678631895917,
             107.65533669807414, 134.9678631895917, 162.28038968110926]),
        new(
            "2024-06-21T12:00:00Z",
            69.6492,
            18.9553,
            HouseSystem.Campanus,
            [189.59291617262681, 222.28038968110926, 254.9678631895917,
             287.65533669807417, 314.9678631895917, 342.2803896811093,
             9.592916172626815, 42.280389681109256, 74.9678631895917,
             107.65533669807414, 134.9678631895917, 162.28038968110926]),
        new(
            "2024-06-21T12:00:00Z",
            57.7089,
            11.9746,
            HouseSystem.Koch,
            [187.85192689865616, 216.08804645784693, 244.63838159771871,
             281.17499272422032, 311.31175871438688, 339.4404791818456,
             7.8519268986561883, 36.088046457846929, 64.638381597718706,
             101.1749927242203, 131.31175871438691, 159.44047918184557]),
        new(
            "2024-06-21T12:00:00Z",
            64.1466,
            -21.9426,
            HouseSystem.Regiomontanus,
            [167.50236790878867, 185.05552228402715, 208.68478176410267,
             249.88060520579347, 298.70024186855517, 328.43854189777517,
             347.50236790878864, 5.0555222840271199, 28.684781764102695,
             69.880605205793486, 118.70024186855517, 148.43854189777514]),
        new(
            "2024-06-21T12:00:00Z",
            64.1466,
            -21.9426,
            HouseSystem.Campanus,
            [167.50236790878867, 202.02020778358229, 227.65129733157841,
             249.88060520579347, 274.53007337936202, 307.3755040082616,
             347.50236790878864, 22.020207783582293, 47.651297331578405,
             69.880605205793486, 94.530073379362037, 127.37550400826163])
    ];

    private sealed record HouseCase(
        string Utc,
        double Latitude,
        double Longitude,
        HouseSystem System,
        IReadOnlyList<double> Longitudes);
}
