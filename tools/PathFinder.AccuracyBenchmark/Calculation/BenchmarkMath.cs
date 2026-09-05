namespace PathFinder.AccuracyBenchmark.Calculation;

public static class BenchmarkMath
{
    public static double CircularDistanceDegrees(double first, double second)
    {
        RequireFinite(first, nameof(first));
        RequireFinite(second, nameof(second));
        var difference = Math.Abs(first - second) % 360;
        return Math.Min(difference, 360 - difference);
    }

    public static double Median(IEnumerable<double> values)
    {
        var ordered = values.ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("A median requires at least one value.", nameof(values));
        }

        foreach (var value in ordered)
        {
            RequireFinite(value, nameof(values));
        }

        Array.Sort(ordered);
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2;
    }

    public static double Maximum(IEnumerable<double> values)
    {
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new ArgumentException("A maximum requires at least one value.", nameof(values));
        }

        RequireFinite(enumerator.Current, nameof(values));
        var maximum = enumerator.Current;
        while (enumerator.MoveNext())
        {
            RequireFinite(enumerator.Current, nameof(values));
            maximum = Math.Max(maximum, enumerator.Current);
        }

        return maximum;
    }

    public static bool Passed(double error, double tolerance)
    {
        RequireFinite(error, nameof(error));
        RequireFinite(tolerance, nameof(tolerance));
        if (error < 0 || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(error), "Errors and tolerances cannot be negative.");
        }

        return error <= tolerance;
    }

    private static void RequireFinite(double value, string parameter)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameter, "Benchmark numbers must be finite.");
        }
    }
}
