# Disposable provider fixture

Only `StealthEyeLLC/world-kernel-build-001-fixture` may be mutated by Build 001 provider experiments. It contains no production code or user data.

`repository-template/` is the deterministic seed. Evaluator reset code creates opaque `wk-b001-*` branches/worktrees from the committed seed, verifies exact refs and file hashes, and records a generation UUID. Rules/checks are configured through evaluator-only provider access and never serialized into operator packages.

The fixture can be deleted after the research campaign. Its existence and name have no canonical architectural meaning.

