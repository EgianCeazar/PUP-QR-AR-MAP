// Loads the map JSON from StreamingAssets (works on Android, where files are inside the APK).
using System;
using System.Collections;
using System.IO;
using PUPMap.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace PUPMap
{
    public class MapLoader : MonoBehaviour
    {
        [Tooltip("Path inside Assets/StreamingAssets")]
        public string mapFile = "Maps/pup_main.json";

        public MapGraph Graph { get; private set; }
        /// <summary>Where people can walk (from the walk-mask picture). Null if the map has none.</summary>
        public WalkGrid Grid { get; private set; }
        public event Action<MapGraph> MapLoaded;

        /// <summary>Full URL for a file inside StreamingAssets (works on Android too).</summary>
        public static string StreamingUrl(string relativePath)
        {
            string path = Path.Combine(Application.streamingAssetsPath, relativePath);
            // On Android the path is a jar:file:// URL, so we must use UnityWebRequest.
            if (!path.Contains("://")) path = "file://" + path;
            return path;
        }

        IEnumerator LoadWalkMask(string file)
        {
            using (var req = UnityWebRequestTexture.GetTexture(StreamingUrl(file), false)) // false = keep pixels readable
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[MapLoader] No walk mask ({file}): {req.error}. Using straight-line paths.");
                    yield break;
                }
                var tex = DownloadHandlerTexture.GetContent(req);
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                var cells = new byte[w * h];
                // Unity stores rows bottom-up; the grid wants row 0 = top of the picture.
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        cells[y * w + x] = px[(h - 1 - y) * w + x].r;
                Grid = new WalkGrid(w, h, cells);
                Destroy(tex);
                Debug.Log($"[MapLoader] Walk mask loaded: {w}x{h}");
            }
        }

        IEnumerator Start()
        {
            string path = StreamingUrl(mapFile);

            using (var req = UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[MapLoader] Couldn't load {path}: {req.error}");
                    yield break;
                }

                var map = JsonUtility.FromJson<CampusMap>(req.downloadHandler.text);
                Graph = new MapGraph(map);

                var problems = Graph.Validate();
                foreach (var p in problems) Debug.LogWarning("[MapLoader] Map problem: " + p);
                Debug.Log($"[MapLoader] Loaded '{map.campusName}': {map.nodes.Count} spots, {map.edges.Count} paths.");

                if (!string.IsNullOrEmpty(map.walkMask))
                    yield return LoadWalkMask(map.walkMask);

                MapLoaded?.Invoke(Graph);
            }
        }
    }
}
