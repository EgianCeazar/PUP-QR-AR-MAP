// Draws the 2D map: the campus picture, numbered place markers, start/destination pins, and the route.
// Positions are in meters (x = east, y = north). The map is turned so the picture shows upright.
// Markers, pins, labels and the route line keep the same size on screen when you zoom.
using System.Collections;
using System.Collections.Generic;
using PUPMap.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace PUPMap
{
    public class MapView2D : MonoBehaviour
    {
        public MapLoader loader;
        public Camera mapCamera;

        [Header("Colors")]
        public Color markerColor = new Color(0.86f, 0.74f, 0.30f);   // gold, like the printed map
        public Color markerText = new Color(0.20f, 0.13f, 0.05f);
        public Color routeColor = new Color(1f, 0.76f, 0.03f);       // PUP gold
        public Color routeCasing = new Color(0.33f, 0f, 0f);         // PUP maroon outline
        public Color startColor = new Color(0.12f, 0.53f, 0.95f);    // blue = you are here
        public Color goalColor = new Color(0.10f, 0.70f, 0.32f);     // green = destination

        [Header("Size on screen (fraction of screen height)")]
        public float markerSize = 0.032f;
        public float pinSize = 0.05f;
        public float routeWidth = 0.011f;

        /// <summary>Fired when the user taps a place marker.</summary>
        public event System.Action<string> MarkerTapped;
        public Bounds MapBounds { get; private set; }
        public event System.Action Drawn;

        readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
        Transform startPin, goalPin;
        TextMesh startLabel, goalLabel;
        GameObject goalLabelBox, startLabelBox;
        LineRenderer routeLine, casingLine;
        MapGraph graph;
        Material spriteMat, circleMat;
        Texture2D circleTex;
        Font font;
        float rotation;
        Route pendingRoute; string pendingStart;
        string startId, goalId;

        void Awake()
        {
            spriteMat = new Material(Shader.Find("Sprites/Default"));
            circleTex = MakeCircleTexture(128);
            circleMat = new Material(spriteMat) { mainTexture = circleTex };
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (mapCamera == null) mapCamera = Camera.main;
            if (loader != null)
            {
                loader.MapLoaded += Draw;
                if (loader.Graph != null) Draw(loader.Graph);
            }
        }

        static bool HasImage(CampusMap m) => m.image != null && !string.IsNullOrEmpty(m.image.file);

        public Vector3 ToLocal(MapNode n)
        {
            var (x, y) = Geo.ToLocalMeters(graph.Map.origin, n.Position);
            return new Vector3((float)x, (float)y, 0f);
        }

        // ---------- building the map ----------

        void Draw(MapGraph g)
        {
            graph = g;
            bool hasImage = HasImage(g.Map);
            rotation = hasImage ? g.Map.image.upBearing : 0f;
            transform.localRotation = Quaternion.Euler(0, 0, rotation);

            // Numbered markers for every place (tap to pick as destination).
            foreach (var n in g.Nodes)
            {
                if (!n.isDestination) continue;
                var m = MakeCircle("Marker " + n.id, ToLocal(n), markerColor, 5, new Color(0.25f, 0.18f, 0.05f));
                if (n.label > 0) MakeText(m, n.label.ToString(), 0.55f, markerText, 7, TextAnchor.MiddleCenter, Vector3.zero);
                markers[n.id] = m;
            }

            // Start + destination pins with name tags (start tag below, destination tag above, so they never overlap).
            startPin = MakeCircle("Start Pin", Vector3.zero, startColor, 10, Color.white);
            goalPin = MakeCircle("Goal Pin", Vector3.zero, goalColor, 10, Color.white);
            startLabel = MakeLabel(startPin, out startLabelBox, below: true);
            goalLabel = MakeLabel(goalPin, out goalLabelBox, below: false);
            startPin.gameObject.SetActive(false); goalPin.gameObject.SetActive(false);

            casingLine = MakeLine("Route Outline", routeCasing, 8);
            routeLine = MakeLine("Route", routeColor, 9);

            if (hasImage) StartCoroutine(LoadBackground(g.Map));
            else
            {
                var pts = new List<Vector3>();
                foreach (var n in g.Nodes) pts.Add(ToLocal(n));
                SetBounds(pts, 40f);
                Drawn?.Invoke();
            }
            if (pendingStart != null) ShowRoute(pendingRoute, pendingStart);
        }

        IEnumerator LoadBackground(CampusMap m)
        {
            var img = m.image;
            var (cx, cy) = Geo.ImageToLocalMeters(img, img.widthPx / 2f, img.heightPx / 2f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Map Picture";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3((float)cx, (float)cy, 1f);
            quad.transform.localRotation = Quaternion.Euler(0, 0, -rotation);
            quad.transform.localScale = new Vector3(img.widthPx * img.metersPerPixel, img.heightPx * img.metersPerPixel, 1);
            var r = quad.GetComponent<Renderer>();
            r.material = new Material(spriteMat) { color = new Color(0.92f, 0.93f, 0.9f) };
            r.sortingOrder = 0;

            var corners = new List<Vector3>();
            foreach (var (px, py) in new (float, float)[] { (0f, 0f), (img.widthPx, 0f), (0f, img.heightPx), (img.widthPx, img.heightPx) })
            {
                var (x, y) = Geo.ImageToLocalMeters(img, px, py);
                corners.Add(new Vector3((float)x, (float)y, 0));
            }
            SetBounds(corners, 5f);
            Drawn?.Invoke();

            using (var req = UnityWebRequestTexture.GetTexture(MapLoader.StreamingUrl(img.file)))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[MapView2D] Couldn't load map picture '{img.file}': {req.error}");
                    yield break;
                }
                var tex = DownloadHandlerTexture.GetContent(req);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Trilinear;
                r.material.mainTexture = tex;
                r.material.color = Color.white;
            }
        }

        void SetBounds(List<Vector3> localPoints, float pad)
        {
            var b = new Bounds(transform.TransformPoint(localPoints[0]), Vector3.zero);
            foreach (var p in localPoints) b.Encapsulate(transform.TransformPoint(p));
            b.Expand(pad);
            MapBounds = b;
        }

        // ---------- route ----------

        public void ShowRoute(Route route, string fromId)
        {
            pendingRoute = route; pendingStart = fromId;
            if (graph == null || routeLine == null) return;

            startId = fromId;
            goalId = route != null && route.Found ? route.nodes[route.nodes.Count - 1].id : null;
            PlacePin(startPin, startLabel, startLabelBox, startId, "You are here");
            PlacePin(goalPin, goalLabel, goalLabelBox, goalId, null);
            foreach (var kv in markers) kv.Value.gameObject.SetActive(kv.Key != startId && kv.Key != goalId);

            var pts = new List<Vector3>();
            if (route != null && route.Found)
            {
                if (route.pathPx != null && HasImage(graph.Map))
                    foreach (var p in route.pathPx)
                    {
                        var (x, y) = Geo.ImageToLocalMeters(graph.Map.image, p.x + 0.5, p.y + 0.5);
                        pts.Add(new Vector3((float)x, (float)y, -0.1f));
                    }
                else foreach (var n in route.nodes) pts.Add(ToLocal(n) + new Vector3(0, 0, -0.1f));
            }
            foreach (var line in new[] { routeLine, casingLine })
            {
                line.positionCount = pts.Count;
                line.SetPositions(pts.ToArray());
            }
        }

        /// <summary>World-space box around the current route (plus its pins), for the camera to zoom to.</summary>
        public bool TryGetRouteBounds(out Bounds b)
        {
            b = default;
            if (routeLine == null || routeLine.positionCount == 0) return false;
            b = new Bounds(transform.TransformPoint(routeLine.GetPosition(0)), Vector3.zero);
            for (int i = 1; i < routeLine.positionCount; i++) b.Encapsulate(transform.TransformPoint(routeLine.GetPosition(i)));
            return true;
        }

        void PlacePin(Transform pin, TextMesh label, GameObject box, string id, string overrideText)
        {
            var n = id != null ? graph.Get(id) : null;
            pin.gameObject.SetActive(n != null);
            if (n == null) return;
            pin.localPosition = ToLocal(n) + new Vector3(0, 0, -0.5f);
            label.text = Wrap(overrideText ?? n.name, 26);
            SizeLabelBox(label, box);
        }

        // ---------- taps ----------

        /// <summary>Called by the camera when the user taps the map (not a drag).</summary>
        public void HandleTap(Vector3 worldPoint)
        {
            if (mapCamera == null) return;
            float radius = mapCamera.orthographicSize * 2f * markerSize; // generous tap target
            string best = null; float bestD = radius;
            foreach (var kv in markers)
            {
                if (!kv.Value.gameObject.activeSelf) continue;
                var p = kv.Value.position; p.z = worldPoint.z;
                float d = Vector3.Distance(p, worldPoint);
                if (d < bestD) { bestD = d; best = kv.Key; }
            }
            if (best != null) MarkerTapped?.Invoke(best);
        }

        // ---------- keep things the same size on screen ----------

        void LateUpdate()
        {
            if (mapCamera == null || graph == null) return;
            float worldPerScreen = mapCamera.orthographicSize * 2f;
            float m = worldPerScreen * markerSize, p = worldPerScreen * pinSize;
            foreach (var kv in markers) kv.Value.localScale = new Vector3(m, m, 1);
            if (startPin) startPin.localScale = new Vector3(p, p, 1);
            if (goalPin) goalPin.localScale = new Vector3(p, p, 1);
            if (routeLine)
            {
                routeLine.widthMultiplier = worldPerScreen * routeWidth;
                casingLine.widthMultiplier = worldPerScreen * routeWidth * 1.7f;
            }
        }

        // ---------- small helpers ----------

        Transform MakeCircle(string name, Vector3 localPos, Color fill, int order, Color ring)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = localPos + new Vector3(0, 0, -0.3f);
            root.localRotation = Quaternion.Euler(0, 0, -rotation); // keep upright
            Disc(root, "Ring", ring, 1.0f, order);
            Disc(root, "Fill", fill, 0.8f, order + 1);
            return root;
        }

        void Disc(Transform parent, string name, Color c, float size, int order)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(parent, false);
            q.transform.localScale = new Vector3(size, size, 1);
            var r = q.GetComponent<Renderer>();
            r.material = new Material(circleMat) { color = c };
            r.sortingOrder = order;
        }

        TextMesh MakeText(Transform parent, string text, float height, Color color, int order, TextAnchor anchor, Vector3 offset)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset + new Vector3(0, 0, -0.01f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = height / 8f; // TextMesh line height is about characterSize * fontSize / 8
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.material = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        TextMesh MakeLabel(Transform pin, out GameObject box, bool below)
        {
            var anchor = below ? TextAnchor.UpperCenter : TextAnchor.LowerCenter;
            var offset = new Vector3(0, below ? -0.75f : 0.75f, 0);
            var label = MakeText(pin, "", 0.42f, new Color(0.15f, 0.1f, 0.1f), 14, anchor, offset);
            box = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(box.GetComponent<Collider>());
            box.name = "Label Box";
            box.transform.SetParent(label.transform, false);
            var r = box.GetComponent<Renderer>();
            r.material = new Material(spriteMat) { color = new Color(1, 1, 1, 0.93f) };
            r.sortingOrder = 13;
            box.transform.localPosition = new Vector3(0, 0, 0.02f);
            return label;
        }

        void SizeLabelBox(TextMesh label, GameObject box)
        {
            var lines = label.text.Split('\n');
            int longest = 0; foreach (var l in lines) longest = Mathf.Max(longest, l.Length);
            float lineH = label.characterSize * label.fontSize / 8f;
            float w = longest * lineH * 0.52f + 0.35f, h = lines.Length * lineH + 0.2f;
            bool below = label.anchor == TextAnchor.UpperCenter;
            box.transform.localScale = new Vector3(w, h, 1);
            box.transform.localPosition = new Vector3(0, below ? -h / 2f + 0.08f : h / 2f - 0.08f, 0.02f);
        }

        LineRenderer MakeLine(string name, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.material = spriteMat;
            lr.startColor = lr.endColor = c;
            lr.widthMultiplier = 3f;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
            lr.sortingOrder = order;
            lr.positionCount = 0;
            return lr;
        }

        static Texture2D MakeCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255); // soft edge
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static string Wrap(string text, int maxChars)
        {
            var words = text.Split(' ');
            var sb = new System.Text.StringBuilder();
            int lineLen = 0;
            foreach (var w in words)
            {
                if (lineLen > 0 && lineLen + 1 + w.Length > maxChars) { sb.Append('\n'); lineLen = 0; }
                else if (lineLen > 0) { sb.Append(' '); lineLen++; }
                sb.Append(w); lineLen += w.Length;
            }
            return sb.ToString();
        }
    }
}
