using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Photospheria.Core.Model;

namespace Photospheria.Core.Simulation;

public class SimulationEngine
{
    private readonly SimulationConfig _config;
    private readonly int _width;
    private readonly int _height;
    private readonly int _totalCells;

    private CellState[] _currentGrid;
    private CellState[] _nextGrid;
    private readonly List<PlantingAction>[] _actionsByTick;

    private int _currentTick;
    private Season _currentSeason;

    public int CurrentTick => _currentTick;
    public Season CurrentSeason => _currentSeason;
    public int Width => _width;
    public int Height => _height;
    public int TotalTicks => _config.TotalTicks;

    public SimulationEngine(SimulationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _width = config.Width;
        _height = config.Height;
        _totalCells = _width * _height;

        _currentGrid = new CellState[_totalCells];
        _nextGrid = new CellState[_totalCells];
        _actionsByTick = new List<PlantingAction>[config.TotalTicks + 1];

        for (int i = 0; i < _actionsByTick.Length; i++)
        {
            _actionsByTick[i] = new List<PlantingAction>();
        }

        Reset();
    }

    public void Reset()
    {
        _currentTick = 0;
        _currentSeason = _config.SeasonProvider(0);

        for (int r = 0; r < _height; r++)
        {
            for (int c = 0; c < _width; c++)
            {
                int i = GetIndex(r, c);
                byte soil = _config.SoilMap != null ? _config.SoilMap[r, c] : (byte)0;
                byte terrain = _config.TerrainMap != null ? _config.TerrainMap[r, c] : (byte)0;

                _currentGrid[i] = CellState.CreateDefault(soil, terrain);
                _nextGrid[i] = CellState.CreateDefault(soil, terrain);
            }
        }
    }

    public void ScheduleAction(PlantingAction action)
    {
        if (action.Tick < 0 || action.Tick >= _actionsByTick.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(action.Tick), $"Tick {action.Tick} is out of bounds [0, {_actionsByTick.Length - 1}].");
        }
        _actionsByTick[action.Tick].Add(action);
    }

    public void ScheduleActions(IEnumerable<PlantingAction> actions)
    {
        foreach (var action in actions)
        {
            ScheduleAction(action);
        }
    }

    public void ClearScheduledActions()
    {
        for (int i = 0; i < _actionsByTick.Length; i++)
        {
            _actionsByTick[i].Clear();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int row, int col) => row * _width + col;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly CellState GetCell(int row, int col)
    {
        return ref _currentGrid[GetIndex(row, col)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref CellState GetCellMutable(int row, int col)
    {
        return ref _currentGrid[GetIndex(row, col)];
    }

    /// <summary>
    /// Executes all ticks until TotalTicks is reached.
    /// </summary>
    public void RunToEnd()
    {
        while (_currentTick < _config.TotalTicks)
        {
            Step();
        }
    }

    /// <summary>
    /// Executes a single simulation tick through the strict 7-stage deterministic pipeline.
    /// Stage 1: Manual actions
    /// Stage 2: Seasonal update
    /// Stage 3: Aging and Maturation
    /// Stage 4: Shade lethality check
    /// Stage 5: Deterministic row-major spread resolution onto empty/immature cells
    /// Stage 6: Nutrient metabolism
    /// Stage 7: Starvation cull
    /// </summary>
    public void Step()
    {
        if (_currentTick >= _config.TotalTicks)
        {
            return;
        }

        // --- STAGE 1: MANUAL ACTIONS ---
        if (_currentTick < _actionsByTick.Length)
        {
            var actions = _actionsByTick[_currentTick];
            int count = Math.Min(actions.Count, 20); // Hard rule: max 20 plants per tick
            for (int i = 0; i < count; i++)
            {
                var action = actions[i];
                if (action.Row >= 0 && action.Row < _height && action.Col >= 0 && action.Col < _width)
                {
                    int idx = GetIndex(action.Row, action.Col);
                    ref var cell = ref _currentGrid[idx];

                    // Cannot plant on uninhabitable terrain
                    if (!cell.IsHabitableSoil) continue;

                    // Manual planting overwrites existing plant immediately
                    cell.PlantIndex = action.PlantIndex;
                    cell.Age = 0;
                    // Retain DeadMatter flag if it was already present!
                    // Clear temporary flags like IsShaded until re-evaluated
                    cell.IsShaded = false;
                }
            }
        }

        // --- STAGE 2: SEASONAL UPDATE ---
        _currentSeason = _config.SeasonProvider(_currentTick);

        // --- STAGE 3: AGING AND MATURATION ---
        for (int i = 0; i < _totalCells; i++)
        {
            ref var cell = ref _currentGrid[i];
            if (cell.IsOccupied)
            {
                cell.Age++;
            }
        }

        // --- STAGE 4: SHADE LETHALITY CHECK ---
        // Clear shaded flags
        for (int i = 0; i < _totalCells; i++)
        {
            _currentGrid[i].IsShaded = false;
        }

        // Oak trees cast shade with radius 4 once mature (Age >= 20)
        for (int r = 0; r < _height; r++)
        {
            for (int c = 0; c < _width; c++)
            {
                int idx = GetIndex(r, c);
                ref readonly var cell = ref _currentGrid[idx];

                if (cell.PlantIndex == Level1Catalogue.OakTreeIndex && cell.Age >= Level1Catalogue.OakTree.TimeToMaturity)
                {
                    int minR = Math.Max(0, r - 4);
                    int maxR = Math.Min(_height - 1, r + 4);
                    int minC = Math.Max(0, c - 4);
                    int maxC = Math.Min(_width - 1, c + 4);

                    for (int nr = minR; nr <= maxR; nr++)
                    {
                        for (int nc = minC; nc <= maxC; nc++)
                        {
                            _currentGrid[GetIndex(nr, nc)].IsShaded = true;
                        }
                    }
                }
            }
        }

        // Apply shade lethality to intolerant species (Grass)
        for (int i = 0; i < _totalCells; i++)
        {
            ref var cell = ref _currentGrid[i];
            if (cell.IsOccupied && cell.IsShaded)
            {
                ref readonly var def = ref Level1Catalogue.Get(cell.PlantIndex);
                if ((def.Weaknesses & PlantWeaknesses.NoShadeSurvival) != 0)
                {
                    // Dies immediately from shade lethality
                    cell.PlantIndex = 0;
                    cell.Age = 0;
                    cell.HasDeadMatter = true;
                }
            }
        }

        // --- STAGE 5: DETERMINISTIC ROW-MAJOR SPREAD RESOLUTION ---
        // Copy current grid into next grid buffer
        Array.Copy(_currentGrid, _nextGrid, _totalCells);

        for (int r = 0; r < _height; r++)
        {
            for (int c = 0; c < _width; c++)
            {
                int srcIdx = GetIndex(r, c);
                ref readonly var srcCell = ref _currentGrid[srcIdx];

                if (!srcCell.IsOccupied) continue;

                ref readonly var plantDef = ref Level1Catalogue.Get(srcCell.PlantIndex);

                // Check maturity and spread timing
                if (srcCell.Age < plantDef.TimeToMaturity) continue;

                if (plantDef.SpreadRate == 0 || srcCell.Age % plantDef.SpreadRate != 0) continue;

                // Check season weakness
                if (_currentSeason == Season.Winter && (plantDef.Weaknesses & PlantWeaknesses.NoWinterSpread) != 0)
                {
                    continue;
                }

                // Check shade spread weakness
                if (srcCell.IsShaded && (plantDef.Weaknesses & PlantWeaknesses.NoShadeSpread) != 0)
                {
                    continue;
                }

                // Generate spread targets according to spread type
                ResolveSpread(r, c, in plantDef);
            }
        }

        // Swap buffers: nextGrid becomes currentGrid
        var temp = _currentGrid;
        _currentGrid = _nextGrid;
        _nextGrid = temp;

        // --- STAGE 6: NUTRIENT METABOLISM ---
        for (int i = 0; i < _totalCells; i++)
        {
            _currentGrid[i].MetabolizeNutrients();
        }

        // --- STAGE 7: STARVATION CULL ---
        for (int i = 0; i < _totalCells; i++)
        {
            _currentGrid[i].CheckAndCullStarved();
        }

        _currentTick++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResolveSpread(int r, int c, in PlantDefinition plantDef)
    {
        int range = plantDef.SpreadRange;

        switch (plantDef.SpreadType)
        {
            case SpreadType.VonNeumann:
                for (int d = 1; d <= range; d++)
                {
                    TrySpreadTo(r - d, c, in plantDef);
                    TrySpreadTo(r + d, c, in plantDef);
                    TrySpreadTo(r, c - d, in plantDef);
                    TrySpreadTo(r, c + d, in plantDef);
                }
                break;

            case SpreadType.Row:
                for (int d = 1; d <= range; d++)
                {
                    TrySpreadTo(r, c - d, in plantDef);
                    TrySpreadTo(r, c + d, in plantDef);
                }
                break;

            case SpreadType.Column:
                for (int d = 1; d <= range; d++)
                {
                    TrySpreadTo(r - d, c, in plantDef);
                    TrySpreadTo(r + d, c, in plantDef);
                }
                break;

            case SpreadType.Moore:
                for (int dr = -range; dr <= range; dr++)
                {
                    for (int dc = -range; dc <= range; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        TrySpreadTo(r + dr, c + dc, in plantDef);
                    }
                }
                break;

            case SpreadType.CrossHatch:
                // Diagonal multi-axis pattern
                for (int d = 1; d <= range; d++)
                {
                    TrySpreadTo(r + d, c + d, in plantDef);
                    TrySpreadTo(r + d, c - d, in plantDef);
                    TrySpreadTo(r - d, c + d, in plantDef);
                    TrySpreadTo(r - d, c - d, in plantDef);
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrySpreadTo(int tr, int tc, in PlantDefinition plantDef)
    {
        if (tr < 0 || tr >= _height || tc < 0 || tc >= _width) return;

        int targetIdx = GetIndex(tr, tc);
        ref readonly var currentTarget = ref _currentGrid[targetIdx];

        // Rule: Spreading requires habitable soil
        if (!currentTarget.IsHabitableSoil) return;

        // Rule: Spreading cannot overwrite an existing mature plant!
        if (currentTarget.IsOccupied)
        {
            ref readonly var targetDef = ref Level1Catalogue.Get(currentTarget.PlantIndex);
            if (currentTarget.Age >= targetDef.TimeToMaturity)
            {
                return; // Protected by maturity
            }
        }

        // Rule: Shade blocks spread for plants with NoShadeSpread
        if (currentTarget.IsShaded && (plantDef.Weaknesses & PlantWeaknesses.NoShadeSpread) != 0)
        {
            return;
        }

        // Immature plant or empty cell is overwritten by this spread
        ref var nextTarget = ref _nextGrid[targetIdx];
        nextTarget.PlantIndex = plantDef.Index;
        nextTarget.Age = 0;
        // Retain DeadMatter and nutrients of the target cell!
    }
}
