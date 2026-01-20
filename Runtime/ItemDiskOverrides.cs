// 1) Registry: resourcePath -> Sprite (lowercase, no extension).
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Stunl0ck.TLS.ModKit.Runtime
{
    internal static class ItemDiskOverrides
    {
        private static readonly ConcurrentDictionary<string, Sprite> _byPath =
            new(StringComparer.Ordinal); // path already normalized to lowercase

        public static void Set(string resourcePath, Sprite sprite)
        {
            if (string.IsNullOrWhiteSpace(resourcePath) || !sprite) return;
            _byPath[Normalize(resourcePath)] = sprite;
        }

        public static bool TryGet(string resourcePath, out Sprite sprite)
            => _byPath.TryGetValue(Normalize(resourcePath), out sprite) && sprite;

        public static string Normalize(string raw)
        {
            // Unity Resources key rules: no leading slash, no extension. We store lowercase.
            var p = raw.Trim().Replace('\\', '/').TrimStart('/');
            var dot = p.LastIndexOf('.');
            if (dot > 0) p = p.Substring(0, dot);
            return p.ToLowerInvariant();
        }
    }
}
