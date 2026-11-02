// What's written inside each QR code.
// Format: WAYFIND:1:<campusId>:<nodeId>    e.g. WAYFIND:1:pup-main:lagoon
// Including the campus id now means the same app can support other campuses later.
using System;

namespace PUPMap.Core
{
    public readonly struct QrPayload
    {
        public const string Prefix = "WAYFIND";
        public const int Version = 1;

        public readonly string CampusId;
        public readonly string NodeId;

        public QrPayload(string campusId, string nodeId) { CampusId = campusId; NodeId = nodeId; }

        public override string ToString() => $"{Prefix}:{Version}:{CampusId}:{NodeId}";

        public static bool TryParse(string text, out QrPayload payload)
        {
            payload = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Trim().Split(':');
            if (parts.Length != 4) return false;
            if (!string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase)) return false;
            if (!int.TryParse(parts[1], out int v) || v != Version) return false;
            if (parts[2].Length == 0 || parts[3].Length == 0) return false;
            payload = new QrPayload(parts[2], parts[3]);
            return true;
        }
    }
}
