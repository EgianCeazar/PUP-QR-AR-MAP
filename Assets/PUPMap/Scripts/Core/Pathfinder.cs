// A* route finding between two map nodes.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PUPMap.Core
{
    public class RouteStep
    {
        public MapNode from;
        public MapNode to;
        public double distance;   // meters
        public double bearing;    // compass degrees
        public string text;       // ready-made instruction (used by the walk-grid router)
        public string Instruction => text ?? (to.isDestination
            ? $"Walk {Geo.CompassWord(bearing)} about {Math.Round(distance)} m to {to.name}"
            : $"Walk {Geo.CompassWord(bearing)} about {Math.Round(distance)} m");
    }

    public class Route
    {
        public List<MapNode> nodes = new List<MapNode>();
        public List<RouteStep> steps = new List<RouteStep>();
        /// <summary>Exact path on the map picture, in pixels (walk-grid routes only).</summary>
        public List<(int x, int y)> pathPx;
        /// <summary>True if the route walks through a building's hallways (e.g. the Main Building).</summary>
        public bool goesIndoors;
        public double TotalDistance => steps.Sum(s => s.distance);
        /// <summary>Rough walking time at ~1.2 m/s.</summary>
        public int EstimatedMinutes => (int)Math.Ceiling(TotalDistance / 1.2 / 60.0);
        public bool Found => nodes.Count > 0;
    }

    public static class Pathfinder
    {
        /// <param name="accessibleOnly">Skip paths marked as having stairs.</param>
        public static Route FindRoute(MapGraph graph, string startId, string goalId, bool accessibleOnly = false)
        {
            var route = new Route();
            var start = graph.Get(startId);
            var goal = graph.Get(goalId);
            if (start == null || goal == null) return route;

            var cameFrom = new Dictionary<string, string>();
            var gScore = new Dictionary<string, double> { [start.id] = 0 };
            var open = new SortedSet<(double f, string id)> { (Geo.Haversine(start.Position, goal.Position), start.id) };
            var closed = new HashSet<string>();

            while (open.Count > 0)
            {
                var current = open.Min;
                open.Remove(current);
                string cur = current.id;
                if (cur == goal.id) return Build(graph, cameFrom, cur);
                if (!closed.Add(cur)) continue;

                foreach (var nb in graph.NeighborsOf(cur))
                {
                    if (accessibleOnly && !nb.edge.accessible) continue;
                    if (closed.Contains(nb.node.id)) continue;
                    double tentative = gScore[cur] + nb.distance;
                    if (gScore.TryGetValue(nb.node.id, out var old) && tentative >= old) continue;

                    if (gScore.ContainsKey(nb.node.id))
                        open.Remove((old + Geo.Haversine(nb.node.Position, goal.Position), nb.node.id));
                    gScore[nb.node.id] = tentative;
                    cameFrom[nb.node.id] = cur;
                    // Heuristic = straight-line distance (never overestimates, so A* stays correct).
                    open.Add((tentative + Geo.Haversine(nb.node.Position, goal.Position), nb.node.id));
                }
            }
            return route; // no route
        }

        static Route Build(MapGraph graph, Dictionary<string, string> cameFrom, string endId)
        {
            var route = new Route();
            var ids = new List<string> { endId };
            while (cameFrom.TryGetValue(ids[ids.Count - 1], out var prev)) ids.Add(prev);
            ids.Reverse();
            route.nodes = ids.Select(graph.Get).ToList();

            for (int i = 0; i < route.nodes.Count - 1; i++)
            {
                var a = route.nodes[i];
                var b = route.nodes[i + 1];
                var nb = graph.NeighborsOf(a.id).First(n => n.node.id == b.id);
                route.steps.Add(new RouteStep
                {
                    from = a, to = b, distance = nb.distance,
                    bearing = Geo.Bearing(a.Position, b.Position)
                });
            }
            // Join steps that keep going the same direction, so directions stay short.
            var merged = new List<RouteStep>();
            foreach (var st in route.steps)
            {
                var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                if (last != null && !last.to.isDestination &&
                    Geo.CompassWord(last.bearing) == Geo.CompassWord(st.bearing))
                {
                    last.distance += st.distance;
                    last.to = st.to;
                }
                else merged.Add(st);
            }
            route.steps = merged;
            return route;
        }
    }
}
