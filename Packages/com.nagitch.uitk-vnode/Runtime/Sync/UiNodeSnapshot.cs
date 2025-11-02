namespace RuntimeUIVDOM.Sync
{
    /// <summary>
    /// Represents the resolved state of a UI element as reported by UI Toolkit.
    /// </summary>
    public struct UiNodeSnapshot
    {
        public string Id;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public bool Visible;
        public string ParentId;
        public int OrderInParent;
    }
}
