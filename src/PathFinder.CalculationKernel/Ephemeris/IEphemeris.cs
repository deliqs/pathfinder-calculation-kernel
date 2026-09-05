using NodaTime;

namespace PathFinder.CalculationKernel.Ephemeris;

public interface IEphemeris
{
    PlanetPosition GetPlanetPosition(Planet planet, Instant utcMoment);
    double GetLongitude(Planet planet, Instant utcMoment);
    IReadOnlyList<PlanetPosition> GetAllPlanetPositions(Instant utcMoment);
    double GetDailyMotion(Planet planet, Instant utcMoment);
    Instant FindPlanetAtLongitude(
        Planet planet,
        double targetLongitude,
        Instant searchStart,
        int searchDaysForward = 400);
}
