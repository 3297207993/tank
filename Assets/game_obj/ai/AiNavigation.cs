using System.Collections.Generic;
using UnityEngine;

//服务器端 A* 导航：直接使用 MapGenerator 生成的格子数据
class AiNavigation
{
    struct Node
    {
        public Vector2Int cell;
        public float g;
        public float f;
    }

    static readonly (Vector2Int delta, MapDataContainer.BlockDirction dir)[] Directions =
    {
        (new Vector2Int(0, 1), MapDataContainer.BlockDirction.Up),
        (new Vector2Int(0, -1), MapDataContainer.BlockDirction.Down),
        (new Vector2Int(-1, 0), MapDataContainer.BlockDirction.Left),
        (new Vector2Int(1, 0), MapDataContainer.BlockDirction.Right),
    };

    readonly MapGenerator mapGenerator;
    readonly List<Vector3> path = new List<Vector3>();

    public AiNavigation(MapGenerator mapGenerator)
    {
        this.mapGenerator = mapGenerator;
    }

    public bool HasPath => path.Count > 0;
    public IReadOnlyList<Vector3> Points => path;

    public void Clear()
    {
        path.Clear();
    }

    public Vector3 CurrentWaypoint(Vector3 position, float reachDistance)
    {
        while (path.Count > 0 && Vector2.Distance(position, path[0]) <= reachDistance)
            path.RemoveAt(0);
        return path.Count > 0 ? path[0] : position;
    }

    /// <summary>
    /// 重新寻路。clear 用来把折线拉直，传 null 则保留原始格子路径。
    /// </summary>
    public bool Repath(Vector3 from, Vector3 to, System.Func<Vector3, Vector3, bool> clear = null)
    {
        path.Clear();
        var map = mapGenerator.CurrentMap;
        if (map == null) return false;

        Vector2Int start = mapGenerator.WorldToCell(from);
        Vector2Int goal = mapGenerator.WorldToCell(to);
        if (start == goal) return true;

        int width = map.xCount;
        int height = map.yCount;
        int total = width * height;
        var gScore = new Dictionary<Vector2Int, float>(total);
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>(total);
        var closed = new HashSet<Vector2Int>();
        var open = new List<Node>();

        gScore[start] = 0f;
        open.Add(new Node { cell = start, g = 0f, f = Heuristic(start, goal) });
        Vector2Int best = start;
        float bestH = Heuristic(start, goal);

        while (open.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].f < open[bestIndex].f) bestIndex = i;
            }
            Node current = open[bestIndex];
            open.RemoveAt(bestIndex);

            if (current.cell == goal)
            {
                best = goal;
                bestH = 0f;
                break;
            }
            if (!closed.Add(current.cell)) continue;

            float h = Heuristic(current.cell, goal);
            if (h < bestH)
            {
                bestH = h;
                best = current.cell;
            }

            foreach (var (delta, dir) in Directions)
            {
                Vector2Int next = current.cell + delta;
                if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height) continue;
                if (map.GetData(current.cell, dir)) continue;
                if (closed.Contains(next)) continue;

                float tentative = current.g + 1f;
                if (gScore.TryGetValue(next, out float old) && tentative >= old) continue;

                gScore[next] = tentative;
                cameFrom[next] = current.cell;
                open.Add(new Node { cell = next, g = tentative, f = tentative + Heuristic(next, goal) });
            }
        }

        //目标不可达时走到离目标最近的位置
        var cells = new List<Vector2Int>();
        Vector2Int cursor = best;
        int guard = 0;
        while (cursor != start && guard++ < total)
        {
            cells.Add(cursor);
            if (!cameFrom.TryGetValue(cursor, out cursor)) break;
        }
        cells.Reverse();
        foreach (var cell in cells) path.Add(mapGenerator.CellCenter(cell));

        if (clear != null && path.Count > 0)
        {
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (!clear(from, path[i])) continue;
                path.RemoveRange(0, i);
                break;
            }
        }
        return path.Count > 0;
    }

    static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
