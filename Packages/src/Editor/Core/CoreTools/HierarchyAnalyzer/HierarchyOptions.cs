namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Options for hierarchy traversal
    /// </summary>
    public class HierarchyOptions
    {
        /// <summary>
        /// Include inactive GameObjects in the result
        /// </summary>
        public bool IncludeInactive { get; set; } = true;
        
        /// <summary>
        /// Maximum depth to traverse (-1 for unlimited)
        /// </summary>
        public int MaxDepth { get; set; } = -1;
        
        /// <summary>
        /// Root path to start traversal from (null for all root objects)
        /// </summary>
        public string RootPath { get; set; }
        
        /// <summary>
        /// Include component information in the result
        /// </summary>
        public bool IncludeComponents { get; set; } = true;
    }
}