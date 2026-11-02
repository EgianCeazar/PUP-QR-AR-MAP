// Route finding on a "walk mask": a picture where
//   white (255) = walkable path, gray (~128) = grass (allowed but avoided), black (0) = blocked,
//   light gray (~200) = indoor hallway (e.g. inside the Main Building).
// Each pixel of the mask lines up with a pixel of the map picture.
using System;
using System.Collections.Generic;

namespace PUPMap.Core
{
    public class WalkGrid
    {
        public readonly int Width, Height;
        readonly byte[] cells; // row 0 = TOP of the picture

        const float GrassCost = 4f;  // walking on grass "costs" 4x, so paths prefer paving
        const float IndoorCost = 1.3f; // hallways are fine, but outdoor paths win if they're about as short

        /// <param name="topDownCells">Width*Height values, row 0 = top of the picture.</param>
        public WalkGrid(int width, int height, byte[] topDownCells)
        {
            if (topDownCells == null || topDownCells.Length != width * height)
                throw new ArgumentException("cells must have width*height values");
            Width = width; Height = height; cells = topDownCells;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool Walkable(int x, int y) => InBounds(x, y) && cells[y * Width + x] >= 64;
        public bool IsGrass(int x, int y) => InBounds(x, y) && cells[y * Width + x] >= 64 && cells[y * Width + x] < 192;
        public bool IsIndoor(int x, int y) => InBounds(x, y) && cells[y * Width + x] >= 192 && cells[y * Width + x] < 250;
        float Cost(int x, int y) => IsGrass(x, y) ? GrassCost : IsIndoor(x, y) ? IndoorCost : 1f;

        /// <summary>True if the straight line from a to b passes through an indoor hallway.</summary>
        public bool LineTouchesIndoor((int x, int y) a, (int x, int y) b)
        {
            int steps = Math.Max(Math.Abs(b.x - a.x), Math.Abs(b.y - a.y));
            for (int s = 0; s <= steps; s++)
            {
                float t = steps == 0 ? 0 : (float)s / steps;
                if (IsIndoor((int)Math.Round(a.x + (b.x - a.x) * t), (int)Math.Round(a.y + (b.y - a.y) * t))) return true;
            }
            return false;
        }

        /// <summary>Closest walkable pixel to (x, y). Spots inside buildings snap to the nearest path.</summary>
        public bool NearestWalkable(int x, int y, out int nx, out int ny, int maxRadius = 80)
        {
            nx = x; ny = y;
            if (Walkable(x, y)) return true;
            for (int r = 1; r <= maxRadius; r++)
            {
                int best = int.MaxValue;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue; // ring only
                        int d2 = dx * dx + dy * dy;
                        if (d2 < best && Walkable(x + dx, y + dy)) { best = d2; nx = x + dx; ny = y + dy; }
                    }
                if (best != int.MaxValue) return true;
            }
            return false;
        }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>A* over the pixels. Returns the path as pixel points (start to goal), or null.</summary>
        public List<(int x, int y)> FindPath(int sx, int sy, int gx, int gy)
        {
            if (!NearestWalkable(sx, sy, out sx, out sy) || !NearestWalkable(gx, gy, out gx, out gy)) return null;
            int n = Width * Height;
            var g = new float[n];
            var from = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.MaxValue; from[i] = -1; }

            int start = sy * Width + sx, goal = gy * Width + gx;
            g[start] = 0;
            var open = new MinHeap();
            open.Push(start, Heuristic(sx, sy, gx, gy));

            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (cur == goal) break;
                if (closed[cur]) continue;
                closed[cur] = true;
                int cx = cur % Width, cy = cur / Width;

                for (int k = 0; k < 8; k++)
                {
                    int x = cx + DX[k], y = cy + DY[k];
                    if (!Walkable(x, y)) continue;
                    // No cutting corners past walls.
                    if (k >= 4 && (!Walkable(cx + DX[k], cy) || !Walkable(cx, cy + DY[k]))) continue;
                    int ni = y * Width + x;
                    if (closed[ni]) continue;
                    float step = (k < 4 ? 1f : 1.41421f) * Math.Max(Cost(x, y), Cost(cx, cy));
                    float ng = g[cur] + step;
                    if (ng < g[ni])
                    {
                        g[ni] = ng; from[ni] = cur;
                        open.Push(ni, ng + Heuristic(x, y, gx, gy));
                    }
                }
            }
            if (from[goal] == -1 && goal != start) return null;

            var path = new List<(int, int)>();
            for (int c = goal; c != -1; c = from[c]) path.Add((c % Width, c / Width));
            path.Reverse();
            return Smooth(path);
        }

        static float Heuristic(int x, int y, int gx, int gy)
        {
            int dx = Math.Abs(x - gx), dy = Math.Abs(y - gy);
            return (dx + dy) + (1.41421f - 2f) * Math.Min(dx, dy); // octile distance
        }

        /// <summary>
        /// Removes zig-zags: skips ahead in a straight line when nothing is in the way
        /// AND the shortcut doesn't cross more grass than the original path did.
        /// </summary>
        List<(int x, int y)> Smooth(List<(int x, int y)> path)
        {
            if (path.Count <= 2) return path;
            // Running cost along the A* path, so any piece of it can be compared to a shortcut.
            var acc = new float[path.Count];
            for (int k = 1; k < path.Count; k++)
            {
                var p0 = path[k - 1]; var p1 = path[k];
                float len = (p0.x != p1.x && p0.y != p1.y) ? 1.41421f : 1f;
                acc[k] = acc[k - 1] + len * Math.Max(Cost(p0.x, p0.y), Cost(p1.x, p1.y));
            }
            var result = new List<(int, int)> { path[0] };
            int i = 0;
            while (i < path.Count - 1)
            {
                int j = Math.Min(path.Count - 1, i + 400); // look ahead a limited distance
                while (j > i + 1 && !(LineCost(path[i], path[j], out float c) && c <= (acc[j] - acc[i]) * 1.02f)) j--;
                result.Add(path[j]);
                i = j;
            }
            return result;
        }

        /// <summary>Cost of walking straight from a to b; false if a wall is in the way.</summary>
        bool LineCost((int x, int y) a, (int x, int y) b, out float cost)
        {
            cost = 0;
            int steps = Math.Max(Math.Abs(b.x - a.x), Math.Abs(b.y - a.y)) * 2;
            if (steps == 0) return true;
            float stepLen = (float)Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y)) / steps;
            for (int s = 1; s <= steps; s++)
            {
                float t = (float)s / steps;
                int x = (int)Math.Round(a.x + (b.x - a.x) * t);
                int y = (int)Math.Round(a.y + (b.y - a.y) * t);
                if (!Walkable(x, y)) return false;
                cost += stepLen * Cost(x, y);
            }
            return true;
        }

        /// <summary>Simple binary heap (priority queue) for A*.</summary>
        class MinHeap
        {
            readonly List<(float p, int v)> a = new List<(float, int)>();
            public int Count => a.Count;
            public void Push(int v, float p)
            {
                a.Add((p, v));
                int i = a.Count - 1;
                while (i > 0) { int par = (i - 1) / 2; if (a[par].p <= a[i].p) break; (a[par], a[i]) = (a[i], a[par]); i = par; }
            }
            public int Pop()
            {
                int top = a[0].v;
                a[0] = a[a.Count - 1]; a.RemoveAt(a.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < a.Count && a[l].p < a[m].p) m = l;
                    if (r < a.Count && a[r].p < a[m].p) m = r;
                    if (m == i) break;
                    (a[m], a[i]) = (a[i], a[m]); i = m;
                }
                return top;
            }
        }
    }

    /// <summary>Builds a Route (with simple left/right directions) from the walk grid.</summary>
    public static class GridRouter
    {
        public static Route FindRoute(MapGraph graph, WalkGrid grid, string startId, string goalId)
        {
            var route = new Route();
            var a = graph.Get(startId); var b = graph.Get(goalId);
            var img = graph.Map.image;
            if (a == null || b == null || grid == null || img == null) return route;

            var px = grid.FindPath((int)a.px, (int)a.py, (int)b.px, (int)b.py);
            if (px == null) return route;

            route.nodes = new List<MapNode> { a, b };
            route.pathPx = px;
            for (int i = 0; i < px.Count - 1 && !route.goesIndoors; i++)
                route.goesIndoors = grid.LineTouchesIndoor(px[i], px[i + 1]);

            // Pixel points -> meters, then group into straight legs and turns.
            double m = img.metersPerPixel;
            var legs = new List<(double len, double dx, double dy)>();
            for (int i = 0; i < px.Count - 1; i++)
            {
                double dx = px[i + 1].x - px[i].x, dy = px[i + 1].y - px[i].y;
                double len = Math.Sqrt(dx * dx + dy * dy) * m;
                if (len < 0.01) continue;
                legs.Add((len, dx, dy));
            }

            double walk = 0; string turn = null;
            (double dx, double dy) prev = (0, 0);
            for (int i = 0; i < legs.Count; i++)
            {
                var L = legs[i];
                if (i > 0)
                {
                    // Angle between this leg and the last one (picture coords: y points down).
                    double cross = prev.dx * L.dy - prev.dy * L.dx;
                    double dot = prev.dx * L.dx + prev.dy * L.dy;
                    double ang = Math.Atan2(cross, dot) * 180 / Math.PI; // + = right turn
                    if (Math.Abs(ang) >= 30)
                    {
                        AddStep(route, turn, walk, a, b, false);
                        turn = Math.Abs(ang) < 60 ? (ang > 0 ? "Bear right" : "Bear left")
                             : Math.Abs(ang) > 135 ? "Turn around" : (ang > 0 ? "Turn right" : "Turn left");
                        walk = 0;
                    }
                }
                walk += L.len;
                prev = (L.dx, L.dy);
            }
            AddStep(route, turn, walk, a, b, true);

            // A tiny first step ("walk 3 m, then turn") is just noise: fold it into the next one.
            if (route.steps.Count > 1 && route.steps[0].distance < 5)
            {
                var first = route.steps[0]; var next = route.steps[1];
                next.distance += first.distance;
                next.text = System.Text.RegularExpressions.Regex.Replace(next.text, @"about \d+ m",
                    $"about {Math.Round(next.distance)} m", System.Text.RegularExpressions.RegexOptions.None);
                route.steps.RemoveAt(0);
            }
            return route;
        }

        static void AddStep(Route r, string turn, double meters, MapNode a, MapNode b, bool last)
        {
            // Tiny wiggles (under 5 m) aren't worth their own instruction: add them to the previous step.
            if (meters < 5 && r.steps.Count > 0)
            {
                var prev = r.steps[r.steps.Count - 1];
                prev.distance += meters;
                prev.text = System.Text.RegularExpressions.Regex.Replace(prev.text, @"about \d+ m",
                    $"about {Math.Round(prev.distance)} m");
                if (last) prev.text += $" to arrive at {b.name}";
                return;
            }
            string walk = $"walk about {Math.Max(1, Math.Round(meters))} m";
            string text = turn == null ? char.ToUpper(walk[0]) + walk.Substring(1) : $"{turn}, then {walk}";
            if (last) text += $" to arrive at {b.name}";
            r.steps.Add(new RouteStep { from = a, to = b, distance = meters, text = text });
        }
    }
}
