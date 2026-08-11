# Build 001 live baseline

Captured 2026-08-11 before implementation mutations. A live process/provider probe was required; descriptor files alone were not accepted as proof of liveness.

## Repository authority

| Repository | GitHub default-branch HEAD | Local state used |
|---|---|---|
| `StealthEyeLLC/eye` | `53948b74701f51c29c9322dfa9f017ba6b45f4a4` | `X:\Repos\eye` was on unrelated dirty branch `build/phase-7-blackboard-relay` at `92e4a9af...`; protected and not modified |
| `StealthEyeLLC/CODEeye` | `1ca0f93d64bc20bccb3b96dbcda43a2232783609` | `X:\CODEeye\repo`, `main`, clean, exact HEAD |
| `StealthEyeLLC/eyebrowse` | `2e27f44ebd3522d0d26b036dc57f790535df3533` | `X:\AgentBrowser\repo`, `main`, clean, exact HEAD |

The live Eye contract is v1 (`eye_inspect` and raw `eye_run`); repository v2 is a target contract, not the active machine surface. CODEeye and eyeBROWSE expose their existing Program Host named-pipe SDKs. Neither sibling repository is changed for this build.

## Machine and runtimes

| Item | Observed value |
|---|---|
| OS | Windows 11 Home, build `10.0.26200`, 64-bit |
| Machine | `STEALTHEYELLC` |
| Eye service | `StealthEye`, running as `NT AUTHORITY\SYSTEM`, Automatic, process observed live |
| Interactive user | `STEALTHEYELLC\StealthEye` |
| Eye control | live v1 run/inspect contract |
| `X:` | label `Eye Dev`, 322,122,547,200 bytes total, 316,866,809,856 bytes free at baseline |
| `C:` | 700,747,608,064 bytes total, 604,043,415,552 bytes free at baseline |
| Git | `C:\Program Files\Git\cmd\git.exe`, `2.55.0.windows.3` |
| .NET | `C:\Program Files\dotnet\dotnet.exe`, SDK `10.0.302` |
| Node | `C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe`, `24.18.1` |
| Chrome | `C:\Program Files\Google\Chrome\Application\chrome.exe`, `151.0.7922.109` |
| PostgreSQL | absent at baseline; portable PostgreSQL 18.4 provisioning selected |
| GitHub owner type | live API reports `StealthEyeLLC` as GitHub `User`, not `Organization`; namespace and requested repository paths are unchanged |

## Live provider probes

- CODEeye kernel and Roslyn provider were started through their existing development tasks; `runtime.info` succeeded over `\\.\pipe\codeeye-dev`.
- eyeBROWSE was already live under SYSTEM; its SDK reported kernel PID, browser incarnation, Chrome version, target list, and CDP port 62022 over `\\.\pipe\eyebrowse-dev`.
- The eyeBROWSE Chrome profile reached GitHub but was redirected to the sign-in page. Therefore authenticated browser-side GitHub mutation is not yet a passing preflight.
- The connected GitHub app has admin/push permissions on the Eye-line repositories. Its surface does not expose repository creation; the machine's configured Git Credential Manager authenticated as the same `StealthEyeLLC` owner and was used only for the two authorized Build 001 repository creations.
- A separate cloud browser reached ChatGPT but was signed out. Fresh isolated cognitive invocation is therefore not yet attested.

## Experiment-owned topology

- Source/workspace root: `X:\WorldKernel\Build001\repo` after deployment.
- PostgreSQL binaries/data/logs: under `X:\WorldKernel\Build001\runtime`, explicitly started/stopped, never registered with Windows SCM.
- Kernel Evidence blobs: `X:\WorldKernel\Build001\evidence\blobs`.
- Evaluator hidden state: separate database and separate filesystem ACL/path under `X:\WorldKernel\Build001\evaluator`.
- Fixture only: `StealthEyeLLC/world-kernel-build-001-fixture`.
