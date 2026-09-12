using System;
using Photospheria.Core.Model;

namespace Photospheria.Solver.Zoning;

public class SectorZoning
{
    private readonly int _width;
    private readonly int _height;
    private readonly byte[,] _gridSpecies;

    public int Width => _width;
    public int Height => _height;

    public SectorZoning(int width, int height)
    {
        _width = width;
        _height = height;
        _gridSpecies = new byte[height, width];

        InitializeZoning();
    }

    private void InitializeZoning()
    {
        int midR = _height / 2;
        int midC = _width / 2;
        int thirdC = _width / 3;
        int twoThirdC = (2 * _width) / 3;

        // Top half:
        // Top-Left: Grass (1)
        // Top-Right: Dwarf Sunflower (5)
        for (int r = 0; r < midR; r++)
        {
            for (int c = 0; c < _width; c++)
            {
                if (c < midC)
                {
                    _gridSpecies[r, c] = Level1Catalogue.GrassIndex;
                }
                else
                {
                    _gridSpecies[r, c] = Level1Catalogue.SunflowerIndex;
                }
            }
        }

        // Bottom half:
        // Bottom-Left (0 to thirdC): Rose Bush (2)
        // Bottom-Center (thirdC to twoThirdC): Lavender (6)
        // Bottom-Right (twoThirdC to _width):
        // Rows midR to midR + 3: Lavender buffer (immune to shade, protecting Sunflower above)
        // Rows midR + 4 to _height - 1: Oak Tree (12)
        int oakSafeRow = Math.Min(_height - 1, midR + 4);

        for (int r = midR; r < _height; r++)
        {
            for (int c = 0; c < _width; c++)
            {
                if (c < thirdC)
                {
                    _gridSpecies[r, c] = Level1Catalogue.RoseBushIndex;
                }
                else if (c < twoThirdC)
                {
                    _gridSpecies[r, c] = Level1Catalogue.LavenderIndex;
                }
                else
                {
                    // Bottom-Right sector:
                    if (r < oakSafeRow)
                    {
                        // Buffer strip using Lavender (shade tolerant)
                        _gridSpecies[r, c] = Level1Catalogue.LavenderIndex;
                    }
                    else
                    {
                        _gridSpecies[r, c] = Level1Catalogue.OakTreeIndex;
                    }
                }
            }
        }
    }

    public byte GetSpeciesForCell(int r, int c)
    {
        if (r < 0 || r >= _height || c < 0 || c >= _width)
        {
            throw new ArgumentOutOfRangeException();
        }
        return _gridSpecies[r, c];
    }

    public int GetMinDistanceBetweenSpecies(byte speciesA, byte speciesB)
    {
        int minDistance = int.MaxValue;

        for (int r1 = 0; r1 < _height; r1++)
        {
            for (int c1 = 0; c1 < _width; c1++)
            {
                if (_gridSpecies[r1, c1] != speciesA) continue;

                for (int r2 = 0; r2 < _height; r2++)
                {
                    for (int c2 = 0; c2 < _width; c2++)
                    {
                        if (_gridSpecies[r2, c2] != speciesB) continue;

                        int dist = Math.Max(Math.Abs(r1 - r2), Math.Abs(c1 - c2));
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                        }
                    }
                }
            }
        }

        return minDistance;
    }
}
