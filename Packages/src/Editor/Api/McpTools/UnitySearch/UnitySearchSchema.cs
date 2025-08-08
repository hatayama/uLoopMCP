using System.ComponentModel;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Output format options for search results when saved to file
    /// </summary>
    public enum SearchOutputFormat
    {
        JSON,
        CSV,
        TSV
    }

    /// <summary>
    /// Unity Search flags for controlling search behavior
    /// </summary>
    [System.Flags]
    public enum UnitySearchFlags
    {
        Default = 0,
        Synchronous = 1,
        WantsMore = 2,
        Packages = 4,
        Sorted = 8
    }

    /// <summary>
    /// Schema for UnitySearch command parameters
    /// Provides type-safe parameter access for Unity Search functionality
    /// Related classes:
    /// - SearchResultItem: Individual search result data structure
    /// - UnitySearchResponse: Response containing search results or file path
    /// - UnitySearchService: Service layer for Unity Search API integration
    /// 
    /// Unity Search Documentation:
    /// - Search Expressions: https://docs.unity3d.com/6000.1/Documentation/Manual/search-expressions.html
    /// - Query Operators: https://docs.unity3d.com/6000.0/Documentation/Manual/search-query-operators.html
    /// - Practical Guide (Japanese): https://light11.hatenadiary.com/entry/2022/12/12/193119
    /// 
    /// Query Syntax Examples:
    /// - Basic file search: "*.cs" (find all C# files)
    /// - Type filter: "t:Texture2D" (find Texture2D assets)
    /// - Reference search: "ref:MyScript" (find assets referencing MyScript)
    /// - Package search: "p:MyPackage" (search within specific package)
    /// - Combined queries: "t:MonoScript *.cs" (C# scripts only)
    /// - Path filter: "Assets/Scripts/*.cs" (C# files in specific folder)
    /// 
    /// Providers:
    /// - asset: Search project assets
    /// - scene: Search in scenes
    /// - menu: Search Unity menu items
    /// - settings: Search project settings
    /// - packages: Search in packages
    /// </summary>
    public class UnitySearchSchema : BaseToolSchema
    {
        /// <summary>
        /// Search query string (supports Unity Search syntax)
        /// Examples: "*.cs", "t:Texture2D", "ref:MyScript", "p:MyPackage"
        /// </summary>
        [Description("Search query string (supports Unity Search syntax). Examples: '*.cs', 't:Texture2D', 'ref:MyScript', 'p:MyPackage'. For detailed Unity Search documentation see: https://docs.unity3d.com/6000.1/Documentation/Manual/search-expressions.html and https://docs.unity3d.com/6000.0/Documentation/Manual/search-query-operators.html. Common queries: '*.cs' (all C# files), 't:Texture2D' (Texture2D assets), 'ref:MyScript' (assets referencing MyScript), 'p:MyPackage' (search in package), 't:MonoScript *.cs' (C# scripts only), 'Assets/Scripts/*.cs' (C# files in specific folder). Japanese guide: https://light11.hatenadiary.com/entry/2022/12/12/193119")]
        public string SearchQuery { get; set; } = "";

        /// <summary>
        /// Specific search providers to use (empty = all active providers)
        /// Common providers: "asset", "scene", "menu", "settings", "packages"
        /// </summary>
        [Description("(Optional) Specific search providers to use (empty = all active providers). Common providers: 'asset', 'scene', 'menu', 'settings', 'packages'")]
        public string[] Providers { get; set; } = new string[0];

        /// <summary>
        /// Maximum number of search results to return
        /// </summary>
        [Description("(Optional) Maximum number of search results to return (default: 50)")]
        public int MaxResults { get; set; } = 50;

        /// <summary>
        /// Whether to include detailed descriptions in results
        /// </summary>
        [Description("(Optional) Whether to include detailed descriptions in results (default: true)")]
        public bool IncludeDescription { get; set; } = true;

        /// <summary>
        /// Whether to include file metadata (size, modified date)
        /// </summary>
        [Description("(Optional) Whether to include file metadata like file size and last modified date (default: false)")]
        public bool IncludeMetadata { get; set; } = false;

        /// <summary>
        /// Search flags for controlling Unity Search behavior
        /// </summary>
        [Description("(Optional) Search flags for controlling Unity Search behavior (default: Default(0), Synchronous(1), WantsMore(2), Packages(4), Sorted(8))")]
        public UnitySearchFlags SearchFlags { get; set; } = UnitySearchFlags.Default;

        /// <summary>
        /// Whether to save search results to external file to avoid massive token consumption
        /// When enabled, results are saved as JSON/CSV files and only the file path is returned
        /// </summary>
        [Description("(Optional) Whether to save search results to external file to avoid massive token consumption when dealing with large result sets. Results are saved as JSON/CSV files for external reading (default: false)")]
        public bool SaveToFile { get; set; } = false;

        /// <summary>
        /// Output file format when SaveToFile is enabled (JSON, CSV, TSV)
        /// </summary>
        [Description("(Optional) Output file format when SaveToFile is enabled (default: JSON(0), CSV(1), TSV(2))")]
        public SearchOutputFormat OutputFormat { get; set; } = SearchOutputFormat.JSON;

        /// <summary>
        /// Threshold for automatic file saving (if result count exceeds this, automatically save to file)
        /// Set to 0 to disable automatic file saving
        /// </summary>
        [Description("(Optional) Threshold for automatic file saving (if result count exceeds this, automatically save to file). Set to 0 to disable automatic file saving (default: 100)")]
        public int AutoSaveThreshold { get; set; } = 100;

        /// <summary>
        /// Filter results by file extension (e.g., "cs", "prefab", "mat")
        /// </summary>
        [Description("(Optional) Filter results by file extension (e.g., 'cs', 'prefab', 'mat')")]
        public string[] FileExtensions { get; set; } = new string[0];

        /// <summary>
        /// Filter results by asset type (e.g., "Texture2D", "GameObject", "MonoScript")
        /// </summary>
        [Description("(Optional) Filter results by asset type (e.g., 'Texture2D', 'GameObject', 'MonoScript')")]
        public string[] AssetTypes { get; set; } = new string[0];

        /// <summary>
        /// Filter results by path pattern (supports wildcards)
        /// </summary>
        [Description("(Optional) Filter results by path pattern (supports wildcards)")]
        public string PathFilter { get; set; } = "";
    }
} 