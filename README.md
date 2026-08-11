# StealthEye World Kernel — Build 001

This repository contains the isolated, noncanonical implementation and measured experiment for StealthEye World Kernel Build 001.

It is a research repository, not an approved product name or a change to Eye, CODEeye, or eyeBROWSE. Native providers remain authoritative for material state. The kernel persists only evidence, observations, epistemic claims, conservative correspondence, action-conditioned predictions, evaluated outcomes, and measured transition episodes.

## Safety boundary

- No sibling Eye repository is modified.
- No permanent Windows service or daemon is installed.
- PostgreSQL runs on demand from experiment-owned directories.
- GitHub actions target only `StealthEyeLLC/world-kernel-build-001-fixture`.
- Evaluator answer keys and arm assignment are stored separately from operator-visible kernel data.
- A prediction must be durable before dispatch or it is ineligible by construction.

## Quick start on STEALTHEYELLC

```powershell
./scripts/provision-postgres.ps1
./scripts/start-postgres.ps1
./scripts/test.ps1
./scripts/stop-postgres.ps1
```

See `docs/04-EXPERIMENT-PROTOCOL.md` for the complete frozen sequence and `docs/05-BUILD-001-RESULTS.md` for the measured conclusion.

