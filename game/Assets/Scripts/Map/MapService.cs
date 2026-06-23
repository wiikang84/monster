using System.Collections.Generic;
using UnityEngine;
using Tower.Data;

namespace Tower.Map
{
    public enum CellType { Empty, Path, Slot, Spawn, Base }

    /// <summary>
    /// StageDef.grid(ASCII) 를 파싱해 셀 타입/특수셀을 제공한다. GameObject는 만들지 않음(데이터만).
    /// 그리드는 위(북)부터 적으며, grid[0] = 가장 위 = r(Rows-1) 로 매핑(행 뒤집기).
    ///   . 빈 바닥  # 길  o 설치 슬롯  S 스폰  G 기지
    /// </summary>
    public sealed class MapService
    {
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        CellType[,] cells;

        public readonly List<Vector2Int> SlotCells = new List<Vector2Int>();
        public readonly List<Vector2Int> SpawnCells = new List<Vector2Int>();
        public readonly List<Vector2Int> BaseCells = new List<Vector2Int>();

        public void Parse(StageDef stage)
        {
            SlotCells.Clear(); SpawnCells.Clear(); BaseCells.Clear();

            var grid = stage.grid;
            Rows = grid.Length;
            Cols = 0;
            foreach (var line in grid) if (line != null && line.Length > Cols) Cols = line.Length;

            cells = new CellType[Cols, Rows];

            for (int i = 0; i < Rows; i++)
            {
                int r = Rows - 1 - i;          // grid 첫 줄이 가장 위(r 최대)
                string line = grid[i] ?? "";
                for (int c = 0; c < Cols; c++)
                {
                    char ch = c < line.Length ? line[c] : '.';
                    CellType t = ch switch
                    {
                        '#' => CellType.Path,
                        'o' => CellType.Slot,
                        'S' => CellType.Spawn,
                        'G' => CellType.Base,
                        _   => CellType.Empty,
                    };
                    cells[c, r] = t;
                    if (t == CellType.Slot) SlotCells.Add(new Vector2Int(c, r));
                    else if (t == CellType.Spawn) SpawnCells.Add(new Vector2Int(c, r));
                    else if (t == CellType.Base) BaseCells.Add(new Vector2Int(c, r));
                }
            }
        }

        public CellType At(int c, int r) => cells[c, r];

        public bool InBounds(int c, int r) => c >= 0 && c < Cols && r >= 0 && r < Rows;

        /// <summary>적이 지나갈 수 있는 칸(길/스폰/기지).</summary>
        public bool IsWalkable(int c, int r)
        {
            if (!InBounds(c, r)) return false;
            var t = cells[c, r];
            return t == CellType.Path || t == CellType.Spawn || t == CellType.Base;
        }
    }
}
