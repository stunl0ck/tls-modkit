using System;
using System.Collections.Generic;
using Stunl0ck.TLS.ModKit.Targets.Glyphs;
using Stunl0ck.TLS.ModKit.Targets.Perks;  

namespace Stunl0ck.TLS.ModKit.Targets
{
    internal static class Registry
    {
        private static readonly Dictionary<string, ITargetAdapter> _byTargetId =
            new Dictionary<string, ITargetAdapter>(StringComparer.OrdinalIgnoreCase)
            {
                ["GlyphDefinition"] = new GlyphTargetAdapter(),
                ["PerkDefinition"] = new PerkTargetAdapter(),
            };

        public static ITargetAdapter Resolve(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return null;
            return _byTargetId.TryGetValue(targetId, out var a) ? a : null;
        }
    }
}
