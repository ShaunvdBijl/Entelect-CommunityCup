# Photospheria Greenhouse Context

Biological sample simulation and plant cultivation domain for Level 1 Greenhouse Study.

## Language

**Cell**:
A discrete unit of greenhouse terrain characterized by soil type, nutrient capacity, and plant occupancy.
_Avoid_: Tile, square, plot

**Nutrient**:
A consumable soil resource starting at 100 that depletes per tick under plant occupancy and recharges in dead matter.
_Avoid_: Fertilizer, energy, food

**Dead Matter**:
The decaying biological residue left behind when a plant perishes from nutrient starvation, halving nutrient consumption for subsequent plants.
_Avoid_: Compost, decay, corpse

**Maturity**:
The state of a plant that has survived for its species-specific gestation duration, unlocking its ability to spread or cast shade.
_Avoid_: Fully grown, adult

**Shade**:
An environmental occlusion zone cast in a radius of 4 cells by mature Oak Trees, lethal to Grass and suppressive to Sunflower spreading.
_Avoid_: Shadow, darkness

**Tick**:
A single discrete time step in the simulation spanning from 0 to T-1.
_Avoid_: Turn, cycle, round

**Planting Action**:
A manual player command scheduled at a specific tick to place a plant species onto a target cell, capped at 20 actions per tick.
_Avoid_: Move, placement, planting step
