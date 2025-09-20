using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Stunl0ck.TLS.ModKit.DSL
{
    /// <summary>
    /// Very small, dependency-free selector helper for simple element/attribute paths.
    /// Supported:
    ///  - Element segments:  A/B/C
    ///  - Wildcard element:  *
    ///  - Attribute equals predicate:  Elem[@Key='Value']
    ///  - Attribute terminal:  .../Elem/@Attr   (use SelectAttributes)
    /// Notes:
    ///  - No namespaces, uses LocalName match.
    ///  - Not a full XPath implementation; good enough for v1 patching.
    /// </summary>
    internal static class XPathHelpers
    {
        private static readonly Regex AttrPredicate =
            new Regex(@"^(?<name>[^[]+)\[@(?<attr>\w+)\s*=\s*'(?<val>[^']*)'\]$",
                      RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Select elements relative to <paramref name="root"/> for a simple path like "A/B[@Key='X']/C".
        /// If the last segment is "@Attr", use SelectAttributes instead.
        /// </summary>
        public static IEnumerable<XElement> SelectElements(XElement root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return Enumerable.Empty<XElement>();

            var segments = path.Trim().TrimStart('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<XElement> current = new[] { root };

            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];

                // If terminal attribute, stop (callers should have used SelectAttributes)
                if (seg.Length > 0 && seg[0] == '@')
                    return Enumerable.Empty<XElement>();

                // Parse predicate form: Name[@Attr='Val']
                string name = seg;
                string predAttr = null;
                string predVal = null;

                var m = AttrPredicate.Match(seg);
                if (m.Success)
                {
                    name = m.Groups["name"].Value;
                    predAttr = m.Groups["attr"].Value;
                    predVal = m.Groups["val"].Value;
                }

                // Wildcard "*"
                IEnumerable<XElement> next;
                if (name == "*")
                {
                    next = current.SelectMany(e => e.Elements());
                }
                else
                {
                    next = current.SelectMany(e => e.Elements().Where(x =>
                        string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)));
                }

                if (predAttr != null)
                {
                    next = next.Where(x => string.Equals((string)x.Attribute(predAttr), predVal, StringComparison.Ordinal));
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Select attribute targets. If select is ".../Elem/@Attr", returns (Elem, "Attr") for each matched Elem.
        /// </summary>
        public static IEnumerable<(XElement element, string attributeName)> SelectAttributes(XElement root, string select)
        {
            if (root == null || string.IsNullOrWhiteSpace(select))
                return Enumerable.Empty<(XElement, string)>();

            var parts = select.Trim().Split('/');
            if (parts.Length == 0) return Enumerable.Empty<(XElement, string)>();

            var last = parts[parts.Length - 1];
            if (last.Length == 0 || last[0] != '@')
                return Enumerable.Empty<(XElement, string)>();

            var attrName = last.Substring(1);
            var parentPath = string.Join("/", parts.Take(parts.Length - 1));
            if (string.IsNullOrWhiteSpace(parentPath))
                return Enumerable.Empty<(XElement, string)>();

            var parents = SelectElements(root, parentPath);
            return parents.Select(p => (p, attrName));
        }
    }
}
