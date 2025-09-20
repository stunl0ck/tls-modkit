using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;

namespace Stunl0ck.TLS.ModKit.DSL
{
    /// <summary>
    /// In-memory representation of a single .patch.xml file.
    /// </summary>
    internal sealed class PatchDocument
    {
        /// <summary>Original file path (for logging).</summary>
        public string SourcePath { get; private set; }

        /// <summary>Target type id, e.g., "GlyphDefinition". Optional when discovered by category.</summary>
        public string Target { get; private set; }

        /// <summary>Action string: add | replace | edit | remove.</summary>
        public string Action { get; private set; }

        /// <summary>Logical id of the target definition for edit/remove. Optional for add/replace if provided inside the definition.</summary>
        public string Id { get; private set; }

        /// <summary>For add/replace: the native XML element (e.g., &lt;GlyphDefinition .../&gt;).</summary>
        public XElement DefinitionElement { get; private set; }

        /// <summary>For edit: sequence of operations (Set/AddNode/RemoveNode).</summary>
        public IReadOnlyList<PatchOperation> Operations { get; private set; }

        public static PatchDocument Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is null/empty.", nameof(filePath));

            XDocument doc;
            try
            {
                doc = XDocument.Load(filePath, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to load XML: {filePath}", ex);
            }

            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "Patch", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Root element must be <Patch>. File: {filePath}");

            var result = new PatchDocument
            {
                SourcePath = filePath,
                Target     = (string)root.Attribute("target"),
                Action     = ((string)root.Attribute("action"))?.Trim(),
                Id         = (string)root.Attribute("id"),
            };

            // Definition block (for add/replace)
            var def = root.Element("Definition");
            if (def != null)
            {
                // Take the first child element inside <Definition>
                var payload = def.Elements().FirstOrDefault();
                if (payload != null)
                {
                    result.DefinitionElement = new XElement(payload); // defensive copy
                    // If an explicit Id was not given, try to read it from the element's Id attribute.
                    if (string.IsNullOrWhiteSpace(result.Id))
                        result.Id = (string)result.DefinitionElement.Attribute("Id");
                }
            }

            // Operations (for edit)
            var ops = new List<PatchOperation>();
            foreach (var child in root.Elements())
            {
                var name = child.Name.LocalName;
                if (string.Equals(name, "Definition", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(name, "Set", StringComparison.OrdinalIgnoreCase))
                {
                    var select = (string)child.Attribute("select");
                    var value  = (string)child.Attribute("value");
                    if (string.IsNullOrWhiteSpace(select))
                        throw new InvalidDataException(Where(filePath, child, "Set requires a 'select' attribute."));
                    ops.Add(PatchOperation.Set(select, value));
                }
                else if (string.Equals(name, "AddNode", StringComparison.OrdinalIgnoreCase))
                {
                    var select = (string)child.Attribute("select");
                    if (string.IsNullOrWhiteSpace(select))
                        throw new InvalidDataException(Where(filePath, child, "AddNode requires a 'select' attribute."));

                    var newNode = child.Elements().FirstOrDefault();
                    if (newNode == null)
                        throw new InvalidDataException(Where(filePath, child, "AddNode must contain a single child element payload."));

                    ops.Add(PatchOperation.AddNode(select, new XElement(newNode)));
                }
                else if (string.Equals(name, "RemoveNode", StringComparison.OrdinalIgnoreCase))
                {
                    var select = (string)child.Attribute("select");
                    if (string.IsNullOrWhiteSpace(select))
                        throw new InvalidDataException(Where(filePath, child, "RemoveNode requires a 'select' attribute."));
                    ops.Add(PatchOperation.RemoveNode(select));
                }
                else
                {
                    // Unknown tag in the patch — ignore for forward-compat, or throw if you prefer strict.
                    // For v1 we ignore with a soft error.
                    // You can switch to a throw later if needed.
                    // throw new InvalidDataException(Where(filePath, child, $"Unknown patch directive <{name}>."));
                }
            }
            result.Operations = ops;

            return result;
        }

        private static string Where(string file, XObject node, string message)
        {
            var li = node as IXmlLineInfo;
            if (li != null && li.HasLineInfo())
                return $"{message} ({Path.GetFileName(file)} @ line {li.LineNumber}, col {li.LinePosition})";
            return $"{message} ({Path.GetFileName(file)})";
        }
    }
}
