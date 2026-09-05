# Repository working agreement

## Scope

- This repository contains the public calculation kernel and the evidence needed to reproduce PathFinder's published calculation benchmark.
- Keep private application hosts, authentication, persistence, interpretation, prompts, user data, infrastructure, and deployment details outside this repository.
- The kernel may depend at runtime only on Astronomy Engine and Noda Time. The benchmark runner may use the kernel and Noda Time. Do not add a dependency without an explicit dependency-boundary review.
- Swiss Ephemeris is an external reference tool. Never link, bundle, download automatically, or relicense Swiss source or binaries. A live Swiss refresh requires a separately obtained `swetest` executable.

## Change discipline

- Treat every checkout as shared. Preserve unexpected work and never revert, delete, clean, or overwrite files outside the assigned scope.
- Plan non-trivial changes before editing. Give concurrent writers disjoint ownership.
- Follow strict red-green-refactor TDD for behavior changes, including one-line changes and bug fixes.
- Use the smallest maintainable change, preserve valid comments, keep files at or below 300 lines, and use evergreen names and comments.
- Never skip, weaken, or disable a test to get green. Report commands actually run and distinguish code failures from environment blockers.
- Do not run mutating Git, GitHub, release, or package-publication commands unless the repository owner explicitly assigned that operation.

## Reproducibility boundaries

- Cases are inputs. Published results are generated outputs and must never be used as an oracle by the runner.
- Frozen external responses and manifests are immutable within a benchmark dataset revision. Refresh commands write candidate outputs and drift reports to a separate caller-selected directory.
- Canonical output contains no wall-clock timestamp, machine path, secret, or mutable branch name.
- Every archived response, normalized reference set, source manifest, and published result must be checksum verified.
- Document time scales, coordinate conventions, observer, target, tool version, invocation, normalization, and tolerances explicitly.
- Do not fabricate missing raw provenance. Publish a new dataset revision when a legacy value cannot be derived honestly from archived evidence.

## Validation

Run the narrowest relevant command first. Before declaring repository work complete, run:

```text
dotnet restore PathFinder.CalculationKernel.slnx --locked-mode
dotnet build PathFinder.CalculationKernel.slnx --no-restore -c Release
dotnet test PathFinder.CalculationKernel.slnx --no-build -c Release
dotnet pack src/PathFinder.CalculationKernel/PathFinder.CalculationKernel.csproj --no-build -c Release
```

The benchmark reproduction and package-equivalence commands become additional mandatory gates once their public CLI contract is implemented.
