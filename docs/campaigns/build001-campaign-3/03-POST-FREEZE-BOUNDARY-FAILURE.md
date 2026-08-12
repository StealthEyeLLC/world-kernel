# Campaign 3 Post-Freeze Boundary Failure

Campaign 3 is terminal: **ABORTED AFTER PROSPECTIVE FREEZE, BEFORE SCIENTIFIC ACQUISITION**.

The provider-authoritative single prospective freeze is commit `648f1343c3355bd4c1f60529c7366055004b2d27` (tree `c4c956e6259719dd7074776da65ce70b676e196c`), manifest SHA-256 `4c39cf3ae6b57e2fd4143650513dc6f24bd1c81b6f528af1bb57a870c6cf23ac`. All 89 normalized execution-file hashes and all 7 authority hashes were verified. No scientific subject was invoked and a post-freeze observation remained literal zero.

## Frozen defect

`Campaign3Boundary.PassesScienceAuthorization` derives its required `provider_head/local_commit` and tree values from `freeze.implementation`: `336894b8245c4b21ece26b90e15ed643f7b0f32b` / `221463a1b6beb684741f7857b1f19fd2d334e69c`. After publication, the truthful local/provider identity is the freeze commit/tree `648f1343...` / `c4c956e6...`. The frozen handoff requires authorization only after provider publication/fetch-back.

A disposable helper invoked the actual frozen boundary method. With all other fields valid, truthful published-freeze identity was rejected and the counterfactual pre-freeze implementation identity was accepted. A truthful science authorization is therefore impossible under the frozen Campaign 3 boundary.

Campaign 3 must not be repaired after freeze. Its scientific counts remain zero. The required continuation is a new campaign boundary; no Campaign 3 scientific result exists.
