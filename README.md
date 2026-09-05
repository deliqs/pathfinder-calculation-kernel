# PathFinder Calculation Kernel

This repository is the public source boundary for PathFinder's calculation kernel and accuracy benchmark. Its purpose is narrow: a pinned release must let an unauthenticated reader rebuild the same calculation package PathFinder consumes and reproduce the published benchmark from versioned cases and archived reference evidence.

## Releases

Each release is an immutable Git tag `vX.Y.Z`. Its GitHub release page, `https://github.com/deliqs/pathfinder-calculation-kernel/releases/tag/vX.Y.Z`, carries the certification bundle (`packages/`, `reproduction/`, `benchmark/`, SBOM, `SHA256SUMS`) and the NuGet package `PathFinder.CalculationKernel` at the same version. The current package version is declared in `Directory.Build.props`.

Reproduce against a tag:

```bash
git clone --branch <tag> --depth 1 https://github.com/deliqs/pathfinder-calculation-kernel.git
```

Compare `benchmark/results/results.sha256` with the certification bundle. Verify a downloaded nupkg SHA-256 against `SHA256SUMS`.

## Boundary

The public package covers benchmark-exercised calculations, including Placidus, Koch, Regiomontanus, and Campanus house calculations. It does not publish PathFinder's APIs, authentication, databases, interpretation and RAG systems, prompts, user data, billing, UI, infrastructure, or deployment configuration.

Runtime calculation dependencies are deliberately limited to:

- [Astronomy Engine](https://github.com/cosinekitty/astronomy), MIT licensed.
- [Noda Time](https://nodatime.org/), Apache-2.0 licensed.

JPL Horizons, IANA time-zone data, and Swiss Ephemeris are external references, not PathFinder components. Swiss software is not distributed here. A live Swiss refresh requires the user to obtain `swetest` separately and comply with Swiss Ephemeris licensing. See `THIRD_PARTY_NOTICES.md` before redistributing benchmark evidence.

## Clean build

Prerequisites:

- .NET SDK `10.0.300`, pinned by `global.json`.
- Network access to `https://api.nuget.org` for the initial locked package restore.
- No private feed, credential, secret, PathFinder service, or Swiss binary is required to build and test the repository.

```bash
dotnet restore PathFinder.CalculationKernel.slnx --locked-mode
dotnet build PathFinder.CalculationKernel.slnx --no-restore -c Release -warnaserror
dotnet test PathFinder.CalculationKernel.slnx --no-build -c Release
dotnet pack src/PathFinder.CalculationKernel/PathFinder.CalculationKernel.csproj --no-build -c Release
```

## Reproduce the frozen benchmark

After the clean build, run the benchmark without network access:

```bash
dotnet run --project tools/PathFinder.AccuracyBenchmark --no-build -c Release -- \
  reproduce --offline --output artifacts/reproduction/local
```

The output path must be new and outside `benchmark/`. The command verifies all 78 archived reference artifacts against the frozen manifest: 58 JPL response/header files, nineteen Swiss raw stdout files, and one minimal derived version-identification file. It performs no HTTP calls and does not execute Swiss Ephemeris. It writes canonical results, their checksum, normalized references, and a manifest binding the calculation output to the public kernel source. See `benchmark/README.md` for the exact published counterparts and current checksum.

## Refresh reference candidates

Live refresh writes review candidates only. It requires a new output directory outside `benchmark/` and never rewrites the frozen revision:

```bash
dotnet run --project tools/PathFinder.AccuracyBenchmark -c Release -- \
  refresh-jpl --output artifacts/candidates/jpl

LC_ALL=C TZ=UTC dotnet run --project tools/PathFinder.AccuracyBenchmark -c Release -- \
  refresh-swiss --swetest /absolute/path/to/swetest --output artifacts/candidates/swiss
```

Successful candidates contain raw provider output, provider-specific reference and normalized JSON, and a drift report. A Swiss candidate leaves source and build claims unverified until they are manually reviewed during explicit frozen-dataset promotion.

## Claims discipline

The intended release claim is benchmark-scoped: the published calculation benchmark is reproducible from public source at an immutable release, and private PathFinder consumes that exact package. Within that boundary, the public inputs, external evidence, runner, kernel source, methodology, tolerances, outputs, and errors support the phrase *fully independent-source reproducibility*.

This is not a prediction-accuracy claim, a claim that every PathFinder component is open source, or a guarantee that mutable third-party services will always return identical live responses. Passing a declared tolerance is a conservative regression and compatibility result, not proof of sub-arcsecond accuracy. In particular, house rows allow 3,600 arcseconds and station rows allow 360 minutes; an all-pass summary must not be presented as universal sub-arcsecond agreement.

## License and security

First-party software and documentation are MIT licensed. Third-party material retains its own terms; see `THIRD_PARTY_NOTICES.md` and `LICENSES/`. Report vulnerabilities according to `SECURITY.md`.
