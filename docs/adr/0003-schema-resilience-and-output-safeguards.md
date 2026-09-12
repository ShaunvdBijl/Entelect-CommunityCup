# 0003: Schema Resilience and Output Safeguards

## Context and Decision
The competition materials contain an intentional adversarial discrepancy between the human overview (`plant_index`) and the formal schema specification (`index`). Submissions failing schema validation receive a score of zero.

To bulletproof the submission pipeline:
1. **Adaptive Action Serialization**: The C# action DTO dynamically inspects the official `schema.json` if present at startup to detect whether `"index"` or `"plant_index"` is strictly required. If schema inspection is unavailable and additional properties are allowed, both fields are serialized (`"index"` and `"plant_index"`).
2. **Hard Invariant Gate**: Before serializing to disk, an invariant validator enforces:
   - $\le 20$ planting actions per tick.
   - Species restricted to Level 1 whitelist `{1, 2, 5, 6, 12}`.
   - Spatial coordinates within $[0, H-1]$ and $[0, W-1]$.
   - Ticks within $[0, T-1]$.
3. **Differential Test Harness**: A verification suite validates the C# engine's state transitions against the official evaluation script or sample tick replays before running optimization passes.

## Consequences
- Protects against zero-score submission rejections caused by schema confusion traps.
- Guarantees strict adherence to game rules and action budget limits.
