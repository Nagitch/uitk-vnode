using System.Collections.Generic;

namespace RuntimeUIVDOM.VDom
{
    /// <summary>
    /// Represents a single logical UI element in the virtual DOM tree.
    /// </summary>
    public class VNode
    {
        public string Id = string.Empty;
        public string Type = string.Empty;
        public Dictionary<string, string> Props = new();
        public Dictionary<string, string> Dataset = new();
        public List<VNode> Children = new();
    }
}
