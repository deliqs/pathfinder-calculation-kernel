# Canonical benchmark output

Canonical result files are UTF-8 JSON with a single LF after the closing brace and no byte-order
mark. Property order is the declaration order of the public result records. Row order is the frozen
case order; house cusp rows are additionally ordered from cusp 1 through 12. No dictionary is used
to determine output order.

Raw PathFinder calculations remain binary64 values. Raw longitudes and external references feed the
shortest-circular-distance errors; raw errors feed summary aggregates and pass/fail decisions. An
error equal to its tolerance passes. For an even number of values, the raw median is the arithmetic
mean of the two middle values after ascending sort.

The publication boundary applies `MidpointRounding.AwayFromZero` once to PathFinder output fields:

- calculated longitudes: seven decimal degrees;
- angular row errors and angular summary aggregates: three decimal arcseconds;
- delta-T metadata: three decimal seconds;
- timing row errors and timing summary aggregates: three decimal minutes.

External reference values, case inputs, tolerances, calculated event timestamps, and kernel APIs are
unchanged. The seven-decimal-degree unit is 0.00036 arcsecond; the observed macOS/Linux drift that
motivated this contract was at most about 0.00000107 arcsecond, giving a margin greater than 300x.
The angular-error publication unit gives a margin greater than 900x. These units are a deterministic
reporting contract, not a claim that the underlying calculations are accurate to those precisions.
After publication rounding, finite JSON numbers are serialized by .NET 10 `System.Text.Json` using
its invariant, round-trip representation.

Canonical output never contains a wall-clock generation time, local path, branch name, access token,
or live-service response metadata. Immutable release identity is recorded outside the result bytes;
the result records calculation, dataset, source-manifest, reference-manifest, dependency, and TZDB
revisions without creating a circular Git-commit input.
