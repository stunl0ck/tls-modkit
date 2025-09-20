using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Stunl0ck.TLS.ModKit.Runtime
{
    // In-memory glyphId -> Sprite store (for disk-loaded icons).
    internal static class GlyphDiskOverrides
    {
        private static readonly ConcurrentDictionary<string, Sprite> _byId =
            new(StringComparer.OrdinalIgnoreCase);

        public static void Set(string glyphId, Sprite sprite)
        {
            if (string.IsNullOrWhiteSpace(glyphId) || !sprite) return;
            _byId[glyphId.Trim()] = sprite;
        }

        public static bool TryGet(string glyphId, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(glyphId)) return false;
            return _byId.TryGetValue(glyphId.Trim(), out sprite) && sprite;
        }

        public static int Count => _byId.Count;
    }
}
