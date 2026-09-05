using CosineKitty;

namespace PathFinder.CalculationKernel.Ephemeris;

/// <summary>
/// Integrates 2060 Chiron from a JPL Horizons J2000 heliocentric ICRF state vector.
/// </summary>
internal sealed class ChironCalculator
{
    // JPL Horizons JPL#171 seed `jpl-horizons-chiron-seed-jpl171-j2000`:
    // COMMAND='2060;', CENTER='500@10' (Sun), JDTDB 2451545.0, ICRF/J2000
    // reference plane (FRAME), geometric vectors (NONE), AU and AU/day (AU-D).
    private const double InitX = -3.529597323721606;
    private const double InitY = -8.675401114502414;
    private const double InitZ = -2.935904700117773;
    private const double InitVx = 0.004971227226758336;
    private const double InitVy = -0.003626418894486951;
    private const double InitVz = -0.0008257960206970693;
    private const double StepDays = 10.0;
    private static readonly AstroTime J2000Time = new(2000, 1, 1, 12, 0, 0);

    internal EclipticPosition Calculate(AstroTime targetTime)
    {
        var chironHeliocentric = IntegrateToTime(targetTime);
        var earthHeliocentric = Astronomy.HelioVector(Body.Earth, targetTime);
        var geocentric = new AstroVector(
            chironHeliocentric.x - earthHeliocentric.x,
            chironHeliocentric.y - earthHeliocentric.y,
            chironHeliocentric.z - earthHeliocentric.z,
            targetTime);
        var ecliptic = Astronomy.EquatorialToEcliptic(geocentric);

        return new EclipticPosition
        {
            Longitude = ecliptic.elon,
            Latitude = ecliptic.elat,
            Distance = geocentric.Length()
        };
    }

    private AstroVector IntegrateToTime(AstroTime targetTime)
    {
        var totalDays = targetTime.ut - J2000Time.ut;
        if (Math.Abs(totalDays) < 0.001)
        {
            return new AstroVector(InitX, InitY, InitZ, targetTime);
        }

        var startState = CreateInitialState();
        var startTime = J2000Time;
        var remainingSigned = targetTime.ut - startTime.ut;
        var direction = Math.Sign(remainingSigned);
        var remainingDays = Math.Abs(remainingSigned);
        var simulator = new GravitySimulator(Body.Sun, startTime, [startState]);
        var states = new StateVector[1];
        var currentTime = startTime;

        while (remainingDays > 0)
        {
            var stepSize = Math.Min(StepDays, remainingDays);
            currentTime = currentTime.AddDays(direction * stepSize);
            simulator.Update(currentTime, states);
            remainingDays -= stepSize;
        }

        return states[0].Position();
    }

    private static StateVector CreateInitialState() => new(
        InitX,
        InitY,
        InitZ,
        InitVx,
        InitVy,
        InitVz,
        J2000Time);
}
