# Fixed verification vectors

These fixtures contain only disposable public roots and signed demonstration licenses, never private keys. The signed payload bytes are frozen; parsers verify the exact transported base64url segment. Both the net10.0 and netstandard2.0 Core assemblies verify these same fixtures in LicenseCompatibilityTests.

The embedded leaf issuance period is historical. Verification must continue to work after that period because it checks the signed issuance instant, not today's clock.

To deliberately replace the vectors after a format revision, run the sample with `--write-vectors MyApp.Tests/Fixtures/Licensing` and review the resulting format changes. Do not regenerate these during ordinary test runs.
