# Calculation benchmark

This directory is the public, offline-reproducible evidence for PathFinder's calculation kernel.
It includes the frozen inputs, exact external reference responses, request contracts, normalizer,
calculation runner, tolerances, results, schemas, and methodology. The runner source is public, so
another person can inspect and execute the public kernel and runner, replay the archived external
evidence offline, and separately reissue the live JPL and Swiss calculations without access to a
private PathFinder repository. We call this fully independent-source reproducibility.

That phrase describes source and evidence availability, not agreement between independent
algorithms in every row. Positions use JPL Horizons and houses use Swiss Ephemeris as external
references. Timing reference instants are derived from archived JPL longitude samples by public
interpolation code. Historical civil-time rows are explicitly compatibility cases using the same
Noda Time dependency as the kernel; they are not independent accuracy evidence.

## Reproduce without network access

From the repository root:

```sh
dotnet restore PathFinder.CalculationKernel.slnx --locked-mode
dotnet run --project tools/PathFinder.AccuracyBenchmark -c Release -- \
  reproduce --offline --output artifacts/reproduction/local
```

The output path must not already exist and must be outside `benchmark/`. The command performs no
HTTP calls and does not execute Swiss Ephemeris. It verifies all 78 archived reference artifacts
(58 JPL response/header files, nineteen Swiss raw house outputs, and one minimal derived Swiss version
identification) against the frozen manifest before parsing them and writes:

- `results.json`
- `results.sha256`
- `normalized-references.json`
- `calculation-source-manifest.json`

The generated files must be byte-identical to the published counterparts:

- `benchmark/results/results.json`
- `benchmark/results/results.sha256`
- `benchmark/references/normalized.json`
- `benchmark/provenance/calculation-source-manifest.json`

The published `results.json` SHA-256 is
`d3622e983007e1854c6928be2a6e11dae7939a33a4c3725eae4829dac8be87df`.

## Refresh candidates

Refresh commands never overwrite `benchmark/`; they require a new output path outside the frozen
tree. A candidate becomes published evidence only after review, integrity validation, a dataset
revision decision, and explicit promotion.
Each successful refresh writes raw artifacts, canonical normalized data, and a drift report against
the frozen provider data. A Swiss refresh records the observed version, executable hash,
environment, arguments, and outputs. It deliberately marks source/build provenance unverified
instead of inferring a repository, commit, or build command from the executable.

```sh
dotnet run --project tools/PathFinder.AccuracyBenchmark -c Release -- \
  refresh-jpl --output artifacts/candidates/jpl

LC_ALL=C TZ=UTC dotnet run --project tools/PathFinder.AccuracyBenchmark -c Release -- \
  refresh-swiss --swetest /absolute/path/to/swetest --output artifacts/candidates/swiss
```

Swiss Ephemeris itself is not distributed here. The frozen manifest records its source repository,
tag, exact commit, build command, executable hash, environment, arguments, a derived one-line
version-identification artifact, and nineteen exact house standard-output files. Full `swetest -h`
output is validated in memory during refresh but is not archived. See `spec/benchmark-spec.md` for
the measurement contract and limitations.
