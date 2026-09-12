# 0002: Macro-Zoning and Terminal Scheduling Strategy

## Context and Decision
Maximizing Level 1 score requires maximizing coverage, Shannon entropy across the 5 available species ($p_i = 0.2$), and survival longevity at tick $T$. A naive unconstrained action search space is intractable ($\gg 10^{25}$). We adopt a hybrid constructive zoning and backward-scheduled local search architecture:
1. **Geometric Macro-Zoning**: Divide the greenhouse into 5 balanced sectors with Oak Trees restricted to perimeter sectors isolated by $\ge 4$ cells of shade buffer from Grass and Sunflowers.
2. **Soil Priming & Backward Scheduling**: If $T > 100$, use sacrificial early crops to prime cells with `DeadMatter`, enabling terminal crops planted at $T - 190$ to survive with $0.5$ nutrient consumption, achieving high longevity scores without starving before tick $T$.
3. **Local Repair**: Fine-tune placement timing and final-tick balancing using hill climbing / simulated annealing over the forward simulation engine.

## Consequences
- Guaranteed separation of Oak shade radius 4 from shade-intolerant Grass and Sunflowers.
- Prevents the 100-tick starvation cliff on the final scoring tick $T$.
- Eliminates the astronomical search space by constraining action generation to structured spatial blueprints.
