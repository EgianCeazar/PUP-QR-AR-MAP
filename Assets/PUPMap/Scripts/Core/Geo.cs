// GPS math helpers.
using System;

namespace PUPMap.Core
{
    public static class Geo
    {
        const double EarthRadius = 6371000.0; // meters

        /// <summary>Real-world distance in meters between two GPS points.</summary>
        public static double Haversine(LatLng a, LatLng b)
        {
            double dLat = ToRad(b.lat - a.lat);
            double dLng = ToRad(b.lng - a.lng);
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(a.lat)) * Math.Cos(ToRad(b.lat)) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * EarthRadius * Math.Asin(Math.Sqrt(h));
        }

        /// <summary>
        /// Converts a GPS point to flat x/y meters from the map origin.
        /// x = east, y = north. Accurate enough for a campus-sized area.
        /// </summary>
        public static (double x, double y) ToLocalMeters(LatLng origin, LatLng p)
        {
            double x = ToRad(p.lng - origin.lng) * EarthRadius * Math.Cos(ToRad(origin.lat));
            double y = ToRad(p.lat - origin.lat) * EarthRadius;
            return (x, y);
        }

        /// <summary>Opposite of ToLocalMeters: flat x/y meters (east/north) back to GPS.</summary>
        public static LatLng FromLocalMeters(LatLng origin, double x, double y)
        {
            double lat = origin.lat + ToDeg(y / EarthRadius);
            double lng = origin.lng + ToDeg(x / (EarthRadius * Math.Cos(ToRad(origin.lat))));
            return new LatLng(lat, lng);
        }

        /// <summary>Pixel on the map image -> meters east/north of the origin.</summary>
        public static (double x, double y) ImageToLocalMeters(MapImage img, double px, double py)
        {
            double up = (img.originPy - py) * img.metersPerPixel;    // meters toward image top
            double right = (px - img.originPx) * img.metersPerPixel; // meters toward image right
            double b = ToRad(img.upBearing);
            double east = up * Math.Sin(b) + right * Math.Cos(b);
            double north = up * Math.Cos(b) - right * Math.Sin(b);
            return (east, north);
        }

        /// <summary>Compass bearing from a to b (0 = north, 90 = east).</summary>
        public static double Bearing(LatLng a, LatLng b)
        {
            double lat1 = ToRad(a.lat), lat2 = ToRad(b.lat);
            double dLng = ToRad(b.lng - a.lng);
            double y = Math.Sin(dLng) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLng);
            return (ToDeg(Math.Atan2(y, x)) + 360.0) % 360.0;
        }

        /// <summary>Turns a bearing into a simple word like "northeast".</summary>
        public static string CompassWord(double bearing)
        {
            string[] dirs = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
            int i = (int)Math.Round(((bearing % 360) + 360) % 360 / 45.0) % 8;
            return dirs[i];
        }

        static double ToRad(double deg) => deg * Math.PI / 180.0;
        static double ToDeg(double rad) => rad * 180.0 / Math.PI;
    }
}
