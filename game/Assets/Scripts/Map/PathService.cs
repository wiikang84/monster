using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower.Map
{
    /// <summary>
    /// 맵의 스폰(S)에서 기지(G)까지 걷는 칸(#/S/G)을 BFS로 추적해 월드 좌표 웨이포인트를 만든다.
    /// 스폰이 여러 개면 경로도 여러 개(다중 경로). 폭 1 통로면 경로는 유일.
    /// </summary>
    public sealed class PathService
    {
        public readonly List<List<Vector3>> Paths = new List<List<Vector3>>();

        static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1)
        };

        /// <summary>경로 산출. cellToWorld: (c,r)→월드좌표(높이 포함). 모든 스폰이 기지에 닿으면 true.</summary>
        public bool Build(MapService map, Func<int, int, Vector3> cellToWorld)
        {
            Paths.Clear();
            bool allValid = true;

            foreach (var spawn in map.SpawnCells)
            {
                var cells = Bfs(map, spawn);
                if (cells == null)
                {
                    Debug.LogError($"[PathService] 스폰 {spawn} 에서 기지(G)로 가는 경로가 없습니다. 그리드 연결 확인 필요.");
                    allValid = false;
                    continue;
                }
                var world = new List<Vector3>(cells.Count);
                foreach (var cl in cells) world.Add(cellToWorld(cl.x, cl.y));
                Paths.Add(world);
            }

            if (map.SpawnCells.Count == 0) { Debug.LogError("[PathService] 스폰(S)이 없습니다."); return false; }
            return allValid;
        }

        /// <summary>spawn → 가장 가까운 Base 까지의 칸 경로(순서대로). 없으면 null.</summary>
        static List<Vector2Int> Bfs(MapService map, Vector2Int start)
        {
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            Vector2Int goal = new Vector2Int(-1, -1);
            bool found = false;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (map.At(cur.x, cur.y) == CellType.Base)
                {
                    goal = cur; found = true; break;
                }
                foreach (var d in Dirs)
                {
                    var nx = cur + d;
                    if (visited.Contains(nx) || !map.IsWalkable(nx.x, nx.y)) continue;
                    visited.Add(nx);
                    cameFrom[nx] = cur;
                    queue.Enqueue(nx);
                }
            }

            if (!found) return null;

            var path = new List<Vector2Int>();
            var node = goal;
            path.Add(node);
            while (node != start && cameFrom.TryGetValue(node, out var prev))
            {
                node = prev;
                path.Add(node);
            }
            path.Reverse();   // start → goal 순서
            return path;
        }
    }
}
