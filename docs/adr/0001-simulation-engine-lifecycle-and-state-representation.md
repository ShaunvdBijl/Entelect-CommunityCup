# 0001: Simulation Engine Lifecycle and State Representation

## Context and Decision
The Level 1 Photospheria simulator must execute millions of rollout ticks without memory churn or floating-point discrepancies. We use a flat 1D contiguous array of unmanaged `CellState` structs double-buffered with pre-allocated swap pointers. Nutrients are tracked using fixed-point integer scaling ($100.0 = 200 \text{ units}$, where $1 \text{ unit} = 0.5$). Each tick follows a strict deterministic pipeline: (1) manual actions, (2) seasonal update, (3) aging/maturation, (4) shade lethality check, (5) deterministic row-major spread resolution onto empty/immature cells, (6) nutrient metabolism, and (7) starvation cull.

## Consequences
- Zero garbage collection allocation during forward rollouts.
- Strict determinism across simulation passes.
- Spreading cannot overwrite existing mature plants.
