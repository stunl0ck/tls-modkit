using System.Collections.Generic;
using System.Xml.Linq;

namespace Stunl0ck.TLS.ModKit.DSL
{
    internal enum PatchOpKind
    {
        Set,
        AddNode,
        RemoveNode
    }

    /// <summary>
    /// Minimal operation model for v1 edit patches.
    /// </summary>
    internal sealed class PatchOperation
    {
        public PatchOpKind Kind { get; private set; }

        /// <summary>XPath-like selector (relative to the definition root).</summary>
        public string Select { get; private set; }

        /// <summary>For Set: value to assign (for attributes or element inner text).</summary>
        public string Value { get; private set; }

        /// <summary>For AddNode: the new element to insert.</summary>
        public XElement NewNode { get; private set; }

        private PatchOperation() { }

        public static PatchOperation Set(string select, string value)
        {
            return new PatchOperation
            {
                Kind = PatchOpKind.Set,
                Select = select,
                Value = value
            };
        }

        public static PatchOperation AddNode(string select, XElement newNode)
        {
            return new PatchOperation
            {
                Kind = PatchOpKind.AddNode,
                Select = select,
                NewNode = new XElement(newNode)
            };
        }

        public static PatchOperation RemoveNode(string select)
        {
            return new PatchOperation
            {
                Kind = PatchOpKind.RemoveNode,
                Select = select
            };
        }
    }
}
