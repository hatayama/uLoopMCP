using System;
using System.Collections.Generic;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Represents a single Unity search result item
    /// Contains comprehensive metadata about the found item
    /// </summary>
    [Serializable]
    public class SearchResultItem
    {
        /// <summary>
        /// Unique identifier of the search result item
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display label/name of the item
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Detailed description of the item
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Search provider that found this item (e.g., "asset", "scene", "menu")
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Type/category of the item (e.g., "Texture2D", "GameObject", "Script")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// File path or location of the item
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Thumbnail path or preview information
        /// </summary>
        public string Thumbnail { get; set; }

        /// <summary>
        /// Search score/relevance (higher = more relevant)
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Additional metadata as key-value pairs
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// File size in bytes (if applicable)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Last modified timestamp (if applicable)
        /// </summary>
        public string LastModified { get; set; }

        /// <summary>
        /// Whether this item can be selected/opened
        /// </summary>
        public bool IsSelectable { get; set; }

        /// <summary>
        /// Asset-specific detailed property information
        /// For example, textures expose Width/Height, meshes expose VertexCount/TriangleCount
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public SearchResultItem()
        {
            Id = string.Empty;
            Label = string.Empty;
            Description = string.Empty;
            Provider = string.Empty;
            Type = string.Empty;
            Path = string.Empty;
            Thumbnail = string.Empty;
            Tags = Array.Empty<string>();
            LastModified = string.Empty;
            IsSelectable = true;
            Properties = new Dictionary<string, object>();
        }

        /// <summary>
        /// Constructor with essential properties
        /// </summary>
        public SearchResultItem(string id, string label, string provider, string type, string path)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Description = string.Empty;
            Provider = provider ?? string.Empty;
            Type = type ?? string.Empty;
            Path = path ?? string.Empty;
            Thumbnail = string.Empty;
            Tags = Array.Empty<string>();
            LastModified = string.Empty;
            IsSelectable = true;
            Score = 0f;
            FileSize = 0L;
            Properties = new Dictionary<string, object>();
        }

        public override string ToString()
        {
            return $"SearchResultItem: {Label} ({Provider}/{Type}) - {Path}";
        }
    }
} 
