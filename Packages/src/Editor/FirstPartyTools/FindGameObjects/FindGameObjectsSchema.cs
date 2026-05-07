using System.ComponentModel;

using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.FirstPartyTools
{
    public class FindGameObjectsSchema : UnityCliLoopToolSchema
    {
        // Search criteria
        public string NamePattern { get; set; } = "";
        [Description("Search mode. Use Selected(4) to inspect the GameObject(s) currently selected in the Unity Hierarchy. Other modes are Exact(0), Path(1), Regex(2), Contains(3).")]
        public SearchMode SearchMode { get; set; } = SearchMode.Exact;
        public string[] RequiredComponents { get; set; } = new string[0];
        public string Tag { get; set; } = "";
        public int? Layer { get; set; } = null;
        public bool IncludeInactive { get; set; } = false;
        
        // Result control
        public int MaxResults { get; set; } = 20;  // Reduced from 100 to prevent performance issues
        public bool IncludeInheritedProperties { get; set; } = false;
    }
    
}
