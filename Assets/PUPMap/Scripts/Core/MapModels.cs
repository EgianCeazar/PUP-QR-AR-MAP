// Plain data classes for the map JSON.
// No UnityEngine here on purpose: this folder ("Core") is pure C#, so it can be
// unit-tested outside Unity and reused later for the campus wayfinding kit.
using System;
using System.Collections.Generic;

namespace PUPMap.Core
{
    [Serializable]
    public class LatLng
    {
        public double lat;
        public double lng;

        public LatLng() { }
        public LatLng(double lat, double lng) { this.lat = lat; this.lng = lng; }
    }

    [Serializable]
    public class MapNode
    {
        public string id;
        public string name;
        public double lat;
        public double lng;
        public int floor;            // 0 = ground. Multi-floor comes later.
        public float facing;         // compass degrees the scanner looks toward (0 = N, 90 = E)
        public bool isDestination;   // false = junction/checkpoint only, hidden from destination list
        public string[] tags;
        public float px;             // position on the map image, in pixels (used when lat/lng are 0)
        public float py;
        public int label;            // number shown on the map image legend (0 = none)

        public LatLng Position => new LatLng(lat, lng);
    }

    [Serializable]
    public class MapEdge
    {
        public string from;
        public string to;
        public bool accessible = true;  // false = has stairs or other barrier
        public float distance;          // meters; 0 = auto-calculate from coordinates
        public string note;
    }

    /// <summary>
    /// A picture of the campus map, plus how to line it up with the real world.
    /// Spots can be placed by pixel (px, py) on this image instead of GPS.
    /// </summary>
    [Serializable]
    public class MapImage
    {
        public string file;            // inside StreamingAssets, e.g. "Maps/pup_main_map.png"
        public int widthPx;
        public int heightPx;
        public float metersPerPixel = 1f;
        public float originPx;         // the pixel that sits at CampusMap.origin (lat/lng)
        public float originPy;
        public float upBearing;        // compass direction the TOP of the image points to (0 = N, 270 = W)
    }

    [Serializable]
    public class CampusMap
    {
        public int version;
        public string campusId;
        public string campusName;
        public string note;
        public LatLng origin;
        public MapImage image;
        public string walkMask;        // inside StreamingAssets; white = walk, gray = grass, black = blocked
        public List<MapNode> nodes = new List<MapNode>();
        public List<MapEdge> edges = new List<MapEdge>();
    }
}
