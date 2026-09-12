using System;

namespace Photospheria.Core.Model;

[Flags]
public enum CellFlags : byte
{
    None = 0,
    HasDeadMatter = 1 << 0,
    IsShaded = 1 << 1
}
