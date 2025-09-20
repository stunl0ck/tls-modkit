using System.Collections.Generic;
using System.Xml.Linq;
using Stunl0ck.TLS.ModKit.DSL;

namespace Stunl0ck.TLS.ModKit.Targets
{
    internal interface ITargetAdapter
    {
        /// <summary>Logical target id, e.g. "GlyphDefinition".</summary>
        string TargetId { get; }

        /// <summary>On-disk category under ModKit/, e.g. "Glyphs".</summary>
        string DataFolderName { get; }

        /// <summary>Add or replace a full native XML definition.</summary>
        void ApplyAdd(XElement definitionElement, string sourceFile, bool replace);

        /// <summary>Edit an existing definition by id (v1: can be stubbed/no-op).</summary>
        void ApplyEdit(string id, IReadOnlyList<PatchOperation> operations, string sourceFile);

        /// <summary>Remove a definition by id.</summary>
        void ApplyRemove(string id, string sourceFile);
    }
}
