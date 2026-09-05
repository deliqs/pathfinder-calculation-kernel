# Third-party notices

The repository-root MIT license covers first-party source code and documentation contributed to this project. It does not relicense third-party software, services, data, or archived reference output.

## Astronomy Engine

The calculation kernel references `CosineKitty.AstronomyEngine` version `2.1.19`, distributed under the MIT License. Its license is reproduced in `LICENSES/AstronomyEngine-MIT.txt`.

Project: <https://github.com/cosinekitty/astronomy>

## Noda Time and IANA time-zone data

The calculation kernel and benchmark reference `NodaTime` version `3.3.3`, distributed under the Apache License 2.0. Its license and NOTICE are reproduced in `LICENSES/NodaTime-Apache-2.0.txt` and `LICENSES/NodaTime-NOTICE.txt`.

Noda Time distributes compiled time-zone data derived from the IANA Time Zone Database. The IANA provenance is retained through Noda Time's data and documentation. This repository does not claim authorship of IANA zone rules.

Projects: <https://nodatime.org/> and <https://www.iana.org/time-zones>

## JPL Horizons

Archived JPL Horizons responses and normalized values, when present in a benchmark dataset, are included as external factual reference evidence with query manifests and checksums. They are not covered by the repository's MIT license. JPL and NASA do not endorse PathFinder.

Service: <https://ssd.jpl.nasa.gov/horizons/>

## Swiss Ephemeris

Swiss Ephemeris is dual-licensed under the GNU Affero General Public License and a professional license. No Swiss Ephemeris source code or executable is linked, bundled, or automatically downloaded by this repository. A live refresh requires a separately obtained `swetest` executable and remains subject to the user's Swiss Ephemeris license obligations.

The nineteen archived Swiss stdout files contain generated numeric table output, and one minimal derived version-identification line records the program identity; the first-party manifest records the corresponding invocations. They contain no Swiss source code, executable, or ephemeris data files. They remain attributed to Swiss Ephemeris and are not relicensed under MIT.

The three CSV fixtures under `tests/PathFinder.CalculationKernel.Tests/TestData/` contain Swiss Ephemeris 2.10.03 numeric output obtained through the pyswisseph 2.10.3.2 binding. Swiss Ephemeris is dual-licensed under AGPL-3.0 or the Swiss Ephemeris professional licence; pyswisseph 2.10.3.2 declares AGPL-3.0 only. They are archived as external factual reference values under the same output-not-source reasoning as the archived stdout files, contain no Swiss or pyswisseph source, executable, or ephemeris files, are test-only fixtures outside the frozen benchmark reference bundle, are not relicensed under MIT, and this remains a scoped project assessment, not legal advice; reassess before any release.

This limited publication decision follows [AGPL-3.0 section 2](https://www.gnu.org/licenses/agpl-3.0.html), which conditions coverage of program output on whether the output's content constitutes a covered work, and the [GNU license FAQ on program output](https://www.gnu.org/licenses/gpl-faq.html#WhatCaseIsOutputGPL), which explains that output is generally not covered unless it contains covered material copied from the program. On the contents currently archived, the maintainers do not identify covered Swiss software material in those generated outputs. This is a scoped project assessment, not legal advice or a general conclusion about other Swiss Ephemeris output; reassess the boundary if the archived artifacts or integration model change.

Project and terms: <https://www.astro.com/swisseph/sweph_e.htm>
