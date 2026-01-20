using System;
using System.Collections.Concurrent;

namespace Stunl0ck.TLS.ModKit.Runtime
{
    /// <summary>
    /// Runtime registry for glyph icon overrides: glyphId -> Resources path.
    /// The path should be a Unity Resources key (no file extension),
    /// e.g. "view/sprites/ui/perks/Specialist".
    /// (Not recommended) - Consider decomissioning
    /// </summary>
    internal static class GlyphIconOverrides
    {
        private static readonly ConcurrentDictionary<string, string> _map =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Set(string glyphId, string path)
        {
            if (string.IsNullOrWhiteSpace(glyphId) || string.IsNullOrWhiteSpace(path))
                return;

            _map[glyphId.Trim()] = Normalize(path);
        }

        public static bool Remove(string glyphId)
        {
            if (string.IsNullOrWhiteSpace(glyphId))
                return false;

            return _map.TryRemove(glyphId.Trim(), out _);
        }

        public static void Clear() => _map.Clear();

        public static bool TryGet(string glyphId, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(glyphId))
                return false;

            return _map.TryGetValue(glyphId.Trim(), out path);
        }

        /// <summary>
        /// Normalize common author inputs to a Unity Resources key:
        ///  - strip leading slashes
        ///  - unify separators to '/'
        ///  - strip "Assets/Resources/" prefix if present
        ///  - keep casing as given (Unity's Resources is case-sensitive on some targets)
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var p = raw.Trim().Replace('\\', '/').TrimStart('/');

            const string prefix = "assets/resources/";
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                p = p.Substring(prefix.Length);

            // Also tolerate a leading "Resources/" someone might include.
            const string alt = "resources/";
            if (p.StartsWith(alt, StringComparison.OrdinalIgnoreCase))
                p = p.Substring(alt.Length);

            // No file extension in Resources keys.
            if (p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                var dot = p.LastIndexOf('.');
                if (dot > 0) p = p.Substring(0, dot);
            }

            return p;
        }
    }
}
