namespace PathFinder.AccuracyBenchmark.Calculation;

public static class PublicationPrecision
{
    public const int LongitudeDecimalPlaces = 7;
    public const int AngularErrorArcsecondDecimalPlaces = 3;
    public const int DeltaTSecondDecimalPlaces = 3;
    public const int TimingErrorMinuteDecimalPlaces = 3;

    public static double LongitudeDegrees(double value) =>
        Round(value, LongitudeDecimalPlaces);

    public static double AngularErrorArcseconds(double value) =>
        Round(value, AngularErrorArcsecondDecimalPlaces);

    public static double DeltaTSeconds(double value) =>
        Round(value, DeltaTSecondDecimalPlaces);

    public static double TimingErrorMinutes(double value) =>
        Round(value, TimingErrorMinuteDecimalPlaces);

    private static double Round(double value, int decimalPlaces)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Publication values must be finite.");
        }

        return Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
    }
}
