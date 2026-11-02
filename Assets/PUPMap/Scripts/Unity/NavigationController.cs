// Connects the UI to the route finder.
// Phase 1: "From" is picked in a dropdown (or by typing QR text) to fake a scan.
// Phase 2 will call SetStartFromQr() from the real camera QR scanner.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PUPMap.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PUPMap
{
    public class NavigationController : MonoBehaviour
    {
        public MapLoader loader;
        public MapView2D view;
        public MapCameraController mapCamera;

        [Header("UI")]
        public Dropdown startDropdown;       // "From" (simulated scan for now)
        public Dropdown destinationDropdown; // "To"
        public Button swapButton;            // swap From and To
        public Toggle accessibleToggle;      // optional: "Avoid stairs" (only used without a walk grid)
        public InputField qrInput;           // paste e.g. WAYFIND:1:pup-main:lagoon
        public Button qrButton;
        public Text summaryText;             // big line: destination · distance · time
        public Text stepsText;               // numbered directions

        [Header("Defaults")]
        public string defaultStartId = "main-gate";
        public string defaultDestinationId = "library";

        MapGraph graph;
        List<MapNode> places = new List<MapNode>();

        public string StartId { get; private set; }
        public string DestinationId { get; private set; }

        void Awake()
        {
            if (loader != null) loader.MapLoaded += OnMapLoaded;
            if (view != null) view.MarkerTapped += SetDestination;
        }

        void OnMapLoaded(MapGraph g)
        {
            graph = g;
            // Sorted by the number on the map, so the lists match the printed legend.
            places = g.Nodes.Where(n => n.isDestination)
                .OrderBy(n => n.label > 0 ? n.label : int.MaxValue).ThenBy(n => n.name).ToList();
            var names = places.Select(DisplayName).ToList();
            startDropdown.ClearOptions(); startDropdown.AddOptions(names);
            destinationDropdown.ClearOptions(); destinationDropdown.AddOptions(names);

            startDropdown.onValueChanged.AddListener(i => { StartId = places[i].id; Recalculate(); });
            destinationDropdown.onValueChanged.AddListener(i => { DestinationId = places[i].id; Recalculate(); });
            if (swapButton) swapButton.onClick.AddListener(Swap);
            if (accessibleToggle) accessibleToggle.onValueChanged.AddListener(_ => Recalculate());
            if (qrButton) qrButton.onClick.AddListener(() => SetStartFromQr(qrInput ? qrInput.text : ""));

            StartId = Pick(defaultStartId, null);
            DestinationId = Pick(defaultDestinationId, StartId);
            SyncDropdowns();
            Recalculate();
        }

        string Pick(string wanted, string avoid)
        {
            var n = places.FirstOrDefault(p => p.id == wanted && p.id != avoid) ?? places.FirstOrDefault(p => p.id != avoid);
            return n?.id;
        }

        void SyncDropdowns()
        {
            startDropdown.SetValueWithoutNotify(Mathf.Max(0, places.FindIndex(p => p.id == StartId)));
            destinationDropdown.SetValueWithoutNotify(Mathf.Max(0, places.FindIndex(p => p.id == DestinationId)));
        }

        static string DisplayName(MapNode n) => n.label > 0 ? $"{n.label}. {n.name}" : n.name;

        /// <summary>Tapping a marker on the map makes it the destination.</summary>
        public void SetDestination(string id)
        {
            if (graph == null || graph.Get(id) == null) return;
            if (id == StartId) { Swap(); return; }
            DestinationId = id;
            SyncDropdowns();
            Recalculate();
        }

        public void Swap()
        {
            (StartId, DestinationId) = (DestinationId, StartId);
            SyncDropdowns();
            Recalculate();
        }

        /// <summary>Call this with the text inside a scanned QR code.</summary>
        public bool SetStartFromQr(string qrText)
        {
            if (graph == null) return false;
            string error = null;
            if (!QrPayload.TryParse(qrText, out var payload)) error = "That QR code isn't a PUP Map checkpoint.";
            else if (payload.CampusId != graph.Map.campusId) error = $"This QR code is for a different campus ({payload.CampusId}).";
            else if (graph.Get(payload.NodeId) == null) error = $"Unknown checkpoint '{payload.NodeId}'. Is the map data up to date?";
            if (error != null) { ShowMessage("Couldn't use that QR code", error); return false; }

            StartId = payload.NodeId;
            if (DestinationId == StartId) DestinationId = Pick(defaultDestinationId, StartId);
            SyncDropdowns();
            Recalculate();
            return true;
        }

        void ShowMessage(string title, string body)
        {
            if (summaryText) summaryText.text = title;
            if (stepsText) stepsText.text = body;
        }

        int calcVersion; // ignore old results if the user changed their mind mid-calculation

        async void Recalculate()
        {
            if (graph == null || StartId == null || DestinationId == null) return;
            var dest = graph.Get(DestinationId);

            if (DestinationId == StartId)
            {
                view.ShowRoute(null, StartId);
                ShowMessage($"You're at {dest.name}", "Pick another place in \"To\", or tap a number on the map.");
                return;
            }

            int version = ++calcVersion;
            string start = StartId, goal = DestinationId;
            var grid = loader != null ? loader.Grid : null;
            Route route;
            if (grid != null)
            {
                ShowMessage("Finding the best route…", "");
                // Route finding on the walk grid can take a moment on phones, so it runs in the background.
                route = await System.Threading.Tasks.Task.Run(() => GridRouter.FindRoute(graph, grid, start, goal));
                if (version != calcVersion || this == null) return;
            }
            else route = Pathfinder.FindRoute(graph, start, goal, accessibleToggle != null && accessibleToggle.isOn);

            view.ShowRoute(route, start);

            if (!route.Found)
            {
                ShowMessage($"No route to {dest.name}", "There's no walkable path between these two places on the map.");
                return;
            }

            if (mapCamera != null && view.TryGetRouteBounds(out var b)) mapCamera.FocusOn(b);

            string via = route.goesIndoors ? "  ·  through the Main Building" : "";
            if (summaryText)
                summaryText.text = $"<b>{dest.name}</b>\n<size=30>{route.EstimatedMinutes} min walk  ·  {route.TotalDistance:0} m{via}</size>";
            var sb = new StringBuilder();
            for (int i = 0; i < route.steps.Count; i++)
                sb.AppendLine($"<b>{i + 1}.</b>  {route.steps[i].Instruction}");
            if (stepsText) stepsText.text = sb.ToString().TrimEnd();
        }
    }
}
