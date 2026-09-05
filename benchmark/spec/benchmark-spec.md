# Calculation benchmark methodology

## Frozen scope

Dataset revision `calculation-benchmark-5` contains:

- 44 geocentric longitude comparisons: 11 bodies at four epochs;
- 228 house-cusp comparisons: 12 cusps at 19 location/epoch/system cases;
- six event-time comparisons: three longitude crossings and three stations;
- three historical civil-time compatibility cases;
- one JPL Chiron state-vector binding.

Cases, tolerances, target identities, request parameters, expected row times, and tool invocations
are frozen before results are calculated. The offline runner never reads the published results as
an input. It verifies the raw bundle, normalizes raw rows, calculates PathFinder outputs, computes
errors and pass/fail values, and serializes canonical JSON.

## JPL position references

The 23 position/seed requests and six timing requests use NASA/JPL Horizons API 1.2. Each position
request is geocentric (`CENTER='500@399'`), Gregorian, ICRF, airless, apparent, and quantity 31:
observer-centered IAU76/80 ecliptic-of-date longitude and latitude including light-time,
gravitational light deflection, and stellar aberration. The exact resolved target/solution header is
part of the contract; a response from a different solution is rejected.

Each position has two comparisons:

1. `nominalUtcErrorArcsec` compares the kernel at the case UTC instant with a Horizons `UT` calendar
   row using the same displayed calendar fields.
2. `matchedTtErrorArcsec` compares the same kernel result with a Horizons `TT` calendar row shifted
   by the kernel's recorded Astronomy Engine 2.1.19 Espenak-Meeus delta-T value.

This separates visible time-scale effects from model differences. It does not make Horizons and
the kernel identical reference-frame or ephemeris implementations.

### UT, UTC, and the 1950 case

Horizons states in every archived `UT` response that times before 1962 are UT1 and later times are
UTC. Therefore the 2000, 2024, and 2050 nominal rows are UTC under the Horizons contract. The 1950
case begins with a proleptic-UTC calendar in the case file, but Horizons interprets that calendar as
UT1. The benchmark supplies the same calendar fields and applies no Earth-orientation-parameter
correction; this is an explicit DUT1=0 approximation, not a claim that UTC and UT1 are equal in
1950. The exact Horizons explanation and EOP metadata remain in each raw response.

## Timing references

Horizons returns longitudes, not event timestamps. The six reference timestamps are derived only
from archived quantity-31 rows:

- direct/retrograde crossings: first interval moving in the requested direction that brackets the
  target, with linear interpolation between hourly samples;
- stations: requested local minimum for retrograde-to-direct or maximum for direct-to-retrograde,
  with three-point parabolic interpolation between ten-minute samples.

Both the case method label and requested post-station motion are validated. PathFinder uses its
public crossing and station finders independently. Absolute timestamp difference is reported in
minutes.

## Swiss house references

The frozen Swiss evidence was produced by `swetest` 2.10.03 built from commit
`175e1fcb3108bcd5c0d146c803f51dcf23508012` with `make swetest`. The reviewed executable SHA-256 is
`8cb956985f8619174377a8aaa17245e5035838f867a1ec3ac284adf4821a8cf0`. The executable is not bundled.

The exact versioned [`swetest.c` source at commit 175e1fcb](https://github.com/aloistr/swisseph/blob/175e1fcb3108bcd5c0d146c803f51dcf23508012/swetest.c#L90-L92)
defines `-ut` as a UT1 input, and its
[house-option contract](https://github.com/aloistr/swisseph/blob/175e1fcb3108bcd5c0d146c803f51dcf23508012/swetest.c#L152)
states that houses require `-ut`. The frozen manifest independently records each exact invocation.
Each invocation supplies a UTC-derived Julian date through `-bj... -ut`, east-positive longitude,
latitude, and the frozen house code. No DUT1 or EOP correction is introduced, so the calendar/Julian
date is used as UT1 for Swiss sidereal-time house calculation. This approximation is explicit,
including for the 1950 row where UTC did not yet exist. Tromsø intentionally compares the kernel's
documented Placidus polar fallback with Swiss Porphyry (`O`).

Refresh executes `swetest -h`, requires exactly one `Version: 2.10.03` identification in memory,
and archives only the deterministic one-line identification. It does not archive the help text.
The nineteen house artifacts remain exact, unmodified standard output from their recorded invocations.

## House engine provenance

Placidus is calculated by the kernel's own semi-arc iteration, audit-cleared and restored unchanged. Koch, Regiomontanus, and Campanus are calculated from geometric definitions: house circles through the horizon's north and south points intersected with the ecliptic, and Koch as ascendants at thirds of the MC's diurnal semi-arc. Swiss Ephemeris output values are their only reference. The benchmark archives swetest-backed Koch, Regiomontanus, and Campanus rows at the four published locations plus in-threshold high-latitude cases (Koch at Gothenburg; Regiomontanus and Campanus at Reykjavík). The Tromsø Koch, Regiomontanus, and Campanus rows test the documented polar fallback against Swiss Porphyry rather than K, R, or C accuracy. Oracle unit tests remain additional coverage and are not a substitute for those archived rows.

## Chiron identity

Chiron is not an Astronomy Engine built-in body in this kernel. The archived JPL Horizons JPL#171
geometric ICRF/J2000 state at JD 2451545.0 TDB, centered on the Sun and expressed in AU/day, must
exactly match the public kernel metadata. A changed JPL solution or vector fails closed rather than
silently changing the benchmark.

## Historical civil-time cases

These rows exercise PathFinder's Noda Time lenient mapping policy for a skipped day, British Double
Summer Time, and historical Amsterdam local time. The manifest pins Noda Time 3.3.3 and TZDB
`2026c`. Because the runner and kernel use the same provider, these are deterministic compatibility
checks, not independent validation of TZDB history.

## Canonicalization and pass criteria

Errors use shortest circular longitude distance. Raw binary64 values feed errors, summary aggregates,
and pass/fail decisions; an error equal to its tolerance passes. At the publication boundary only,
PathFinder longitudes are rounded to seven decimal degrees, angular errors and summaries to three
decimal arcseconds, delta-T to three decimal seconds, and timing errors and summaries to three decimal
minutes, all away from zero at midpoints. External references and timestamps are preserved. The
closed result schema enforces these units. This reporting policy removes insignificant platform drift
without changing kernel APIs or claiming accuracy at the reporting precision; the exact rationale is
in [`canonicalization.md`](canonicalization.md).

Result JSON is UTF-8, LF-terminated, culture-independent, declaration ordered, and contains no wall
clock, branch, machine path, token, or live response metadata. The closed result schema rejects
unknown nested fields. CI reproduces twice from source and once from the packed kernel and requires
byte identity with the published result, normalized references, and source manifest.

## Interpretation of the current result

All 44 position rows, 228 cusp rows, six timing rows, and three compatibility rows satisfy their
predeclared tolerances. The declared limits are 60 arcseconds for ordinary positions, 1,800
arcseconds for Chiron, 3,600 arcseconds for cusps, 90 minutes for crossings, and 360 minutes for
stations. These deliberately conservative limits are regression and compatibility bounds, not
precision targets. Pass counts alone are therefore weak evidence; the published per-row errors,
reported to 0.001 arcsecond or 0.001 minute as applicable, are the accuracy evidence.

The median nominal position error is 1.861 arcseconds. The largest position error is Chiron at the
1950 epoch, 74.756 arcseconds; Chiron is propagated by the public gravity simulation rather than a
JPL DE/SPICE backend. Excluding that custom-body limitation would make the headline misleading. The
largest cusp error is 12.035 arcseconds, and the largest event-time error is 15.258 minutes.

The result is a reproducible measurement of the published algorithms and scope. It is not a claim
of universal sub-arcsecond planetary accuracy, independent timezone accuracy, or equivalence to
JPL DE440/DE441/SPICE across all bodies and epochs.
