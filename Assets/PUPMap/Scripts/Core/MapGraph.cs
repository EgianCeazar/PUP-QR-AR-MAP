// Turns the raw CampusMap into a graph you can search, and checks it for mistakes.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PUPMap.Core
{
    public class Neighbor
    {
        public MapNode node;
        public MapEdge edge;
        public double distance;
    }

    public class MapGraph
    {
        public CampusMap Map { get; }
        readonly Dictionary<string, MapNode> nodes = new Dictionary<string, MapNode>();
        readonly Dictionary<string, List<Neighbor>> adjacency = new Dictionary<string, List<Neighbor>>();

        public MapGraph(CampusMap map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            foreach (var n in map.nodes)
            {
                // No GPS yet? Work it out from the spot's pixel position on the map image.
                if (n.lat == 0 && n.lng == 0 && map.image != null && map.origin != null)
                {
                    var (x, y) = Geo.ImageToLocalMeters(map.image, n.px, n.py);
                    var ll = Geo.FromLocalMeters(map.origin, x, y);
                    n.lat = ll.lat; n.lng = ll.lng;
                }
                nodes[n.id] = n;
                adjacency[n.id] = new List<Neighbor>();
            }

            foreach (var e in map.edges)
            {
                if (!nodes.ContainsKey(e.from) || !nodes.ContainsKey(e.to)) continue; // reported by Validate()
                double d = e.distance > 0 ? e.distance : Geo.Haversine(nodes[e.from].Position, nodes[e.to].Position);
                // All paths are two-way for now (you can walk both directions).
                adjacency[e.from].Add(new Neighbor { node = nodes[e.to], edge = e, distance = d });
                adjacency[e.to].Add(new Neighbor { node = nodes[e.from], edge = e, distance = d });
            }
        }

        public IEnumerable<MapNode> Nodes => Map.nodes;
        public IEnumerable<MapNode> Destinations => Map.nodes.Where(n => n.isDestination).OrderBy(n => n.name);

        public MapNode Get(string id) => id != null && nodes.TryGetValue(id, out var n) ? n : null;
        public IReadOnlyList<Neighbor> NeighborsOf(string id) =>
            adjacency.TryGetValue(id, out var list) ? list : (IReadOnlyList<Neighbor>)Array.Empty<Neighbor>();

        /// <summary>Returns a list of problems in the map data. Empty list = all good.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (var n in Map.nodes)
            {
                if (string.IsNullOrWhiteSpace(n.id)) problems.Add($"A node named '{n.name}' has no id.");
                else if (!seen.Add(n.id)) problems.Add($"Duplicate node id '{n.id}'.");
                if (Math.Abs(n.lat) > 90 || Math.Abs(n.lng) > 180) problems.Add($"Node '{n.id}' has invalid coordinates.");
            }
            foreach (var e in Map.edges)
            {
                if (!nodes.ContainsKey(e.from)) problems.Add($"Edge points to unknown node '{e.from}'.");
                if (!nodes.ContainsKey(e.to)) problems.Add($"Edge points to unknown node '{e.to}'.");
                if (e.from == e.to) problems.Add($"Edge from '{e.from}' to itself.");
            }
            // Every node should be reachable from the first one (only checked when paths are listed by hand).
            if (Map.nodes.Count > 0 && Map.edges.Count > 0)
            {
                var reached = new HashSet<string> { Map.nodes[0].id };
                var stack = new Stack<string>(reached);
                while (stack.Count > 0)
                    foreach (var nb in NeighborsOf(stack.Pop()))
                        if (reached.Add(nb.node.id)) stack.Push(nb.node.id);
                foreach (var n in Map.nodes.Where(n => !reached.Contains(n.id)))
                    problems.Add($"Node '{n.id}' can't be reached from '{Map.nodes[0].id}'.");
            }
            return problems;
        }
    }
}
