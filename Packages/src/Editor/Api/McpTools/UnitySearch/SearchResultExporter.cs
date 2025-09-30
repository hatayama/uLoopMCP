using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utility class for exporting Unity search results to external files
    /// Supports multiple output formats to avoid massive token consumption
    /// Related classes:
    /// - SearchResultItem: Data structure being exported
    /// - UnitySearchResponse: Contains export metadata
    /// - NUnitXmlResultExporter: Similar pattern for test results
    /// </summary>
    public static class SearchResultExporter
    {
        private static readonly string EXPORT_DIRECTORY = Path.Combine(McpConstants.OUTPUT_ROOT_DIR, McpConstants.SEARCH_RESULTS_DIR);
        private const string FILE_PREFIX = "unity_search_results";

        /// <summary>
        /// Export search results to file in the specified format
        /// </summary>
        /// <param name="results">Search results to export</param>
        /// <param name="format">Output format (JSON, CSV, TSV)</param>
        /// <param name="searchQuery">Original search query for metadata</param>
        /// <param name="providersUsed">Search providers that were used</param>
        /// <returns>Path to the exported file</returns>
        public static string ExportSearchResults(SearchResultItem[] results, SearchOutputFormat format, 
                                                string searchQuery, string[] providersUsed)
        {
            if (results == null || results.Length == 0)
            {
                throw new ArgumentException("Cannot export empty search results");
            }

            // Ensure export directory exists
            string exportDir = Path.Combine(Application.dataPath, "..", EXPORT_DIRECTORY);
            Directory.CreateDirectory(exportDir);

            // Generate unique filename with timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileExtension = GetFileExtension(format);
            string fileName = $"{FILE_PREFIX}_{timestamp}.{fileExtension}";
            string filePath = Path.Combine(exportDir, fileName);

            // Export based on format
            switch (format)
            {
                case SearchOutputFormat.JSON:
                    ExportAsJson(results, filePath, searchQuery, providersUsed);
                    break;
                case SearchOutputFormat.CSV:
                    ExportAsCsv(results, filePath, searchQuery, providersUsed);
                    break;
                case SearchOutputFormat.TSV:
                    ExportAsTsv(results, filePath, searchQuery, providersUsed);
                    break;
                default:
                    throw new ArgumentException($"Unsupported export format: {format}");
            }

            // Return relative path from project root
            return Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), filePath);
        }

        /// <summary>
        /// Export search results as JSON format
        /// </summary>
        private static void ExportAsJson(SearchResultItem[] results, string filePath, 
                                        string searchQuery, string[] providersUsed)
        {
            SearchResultsExport export = new SearchResultsExport
            {
                ExportMetadata = new ExportMetadata
                {
                    ExportTimestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    SearchQuery = searchQuery,
                    ProvidersUsed = providersUsed,
                    ResultCount = results.Length,
                    ExportFormat = "JSON"
                },
                Results = results
            };

            string json = JsonConvert.SerializeObject(export, Formatting.Indented);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// Export search results as CSV format
        /// </summary>
        private static void ExportAsCsv(SearchResultItem[] results, string filePath, 
                                       string searchQuery, string[] providersUsed)
        {
            StringBuilder csv = new StringBuilder();

            // Add metadata header
            csv.AppendLine($"# Unity Search Results Export");
            csv.AppendLine($"# Export Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}");
            csv.AppendLine($"# Search Query: {EscapeCsvValue(searchQuery)}");
            csv.AppendLine($"# Providers Used: {string.Join(", ", providersUsed)}");
            csv.AppendLine($"# Result Count: {results.Length}");
            csv.AppendLine();

            // Collect all property keys for the header
            List<string> propertyKeys = GetAllPropertyKeys(results);

            // Add CSV header
            StringBuilder headerBuilder = new StringBuilder();
            headerBuilder.Append("Id,Label,Description,Provider,Type,Path,Score,FileSize,LastModified,IsSelectable,Tags");
            foreach (string key in propertyKeys)
            {
                headerBuilder.Append($",{EscapeCsvValue(key)}");
            }
            csv.AppendLine(headerBuilder.ToString());

            // Add data rows
            foreach (SearchResultItem result in results)
            {
                StringBuilder rowBuilder = new StringBuilder();
                rowBuilder.Append($"{EscapeCsvValue(result.Id)}," +
                                 $"{EscapeCsvValue(result.Label)}," +
                                 $"{EscapeCsvValue(result.Description)}," +
                                 $"{EscapeCsvValue(result.Provider)}," +
                                 $"{EscapeCsvValue(result.Type)}," +
                                 $"{EscapeCsvValue(result.Path)}," +
                                 $"{result.Score}," +
                                 $"{result.FileSize}," +
                                 $"{EscapeCsvValue(result.LastModified)}," +
                                 $"{result.IsSelectable}," +
                                 $"{EscapeCsvValue(string.Join(";", result.Tags))}");

                // Append properties
                foreach (string key in propertyKeys)
                {
                    if (result.Properties != null && result.Properties.TryGetValue(key, out object value))
                    {
                        rowBuilder.Append($",{EscapeCsvValue(value?.ToString() ?? "")}");
                    }
                    else
                    {
                        rowBuilder.Append(",");
                    }
                }

                csv.AppendLine(rowBuilder.ToString());
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Export search results as TSV format
        /// </summary>
        private static void ExportAsTsv(SearchResultItem[] results, string filePath, 
                                       string searchQuery, string[] providersUsed)
        {
            StringBuilder tsv = new StringBuilder();

            // Add metadata header
            tsv.AppendLine($"# Unity Search Results Export");
            tsv.AppendLine($"# Export Timestamp: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}");
            tsv.AppendLine($"# Search Query: {EscapeTsvValue(searchQuery)}");
            tsv.AppendLine($"# Providers Used: {string.Join(", ", providersUsed)}");
            tsv.AppendLine($"# Result Count: {results.Length}");
            tsv.AppendLine();

            // Collect all property keys for the header
            List<string> propertyKeys = GetAllPropertyKeys(results);

            // Add TSV header
            StringBuilder headerBuilder = new StringBuilder();
            headerBuilder.Append("Id\tLabel\tDescription\tProvider\tType\tPath\tScore\tFileSize\tLastModified\tIsSelectable\tTags");
            foreach (string key in propertyKeys)
            {
                headerBuilder.Append($"\t{EscapeTsvValue(key)}");
            }
            tsv.AppendLine(headerBuilder.ToString());

            // Add data rows
            foreach (SearchResultItem result in results)
            {
                StringBuilder rowBuilder = new StringBuilder();
                rowBuilder.Append($"{EscapeTsvValue(result.Id)}\t" +
                                 $"{EscapeTsvValue(result.Label)}\t" +
                                 $"{EscapeTsvValue(result.Description)}\t" +
                                 $"{EscapeTsvValue(result.Provider)}\t" +
                                 $"{EscapeTsvValue(result.Type)}\t" +
                                 $"{EscapeTsvValue(result.Path)}\t" +
                                 $"{result.Score}\t" +
                                 $"{result.FileSize}\t" +
                                 $"{EscapeTsvValue(result.LastModified)}\t" +
                                 $"{result.IsSelectable}\t" +
                                 $"{EscapeTsvValue(string.Join(";", result.Tags))}");

                // Append properties
                foreach (string key in propertyKeys)
                {
                    if (result.Properties != null && result.Properties.TryGetValue(key, out object value))
                    {
                        rowBuilder.Append($"\t{EscapeTsvValue(value?.ToString() ?? "")}");
                    }
                    else
                    {
                        rowBuilder.Append("\t");
                    }
                }

                tsv.AppendLine(rowBuilder.ToString());
            }

            File.WriteAllText(filePath, tsv.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Collect all property keys from every result
        /// </summary>
        private static List<string> GetAllPropertyKeys(SearchResultItem[] results)
        {
            HashSet<string> keySet = new HashSet<string>();
            foreach (SearchResultItem result in results)
            {
                if (result.Properties != null)
                {
                    foreach (string key in result.Properties.Keys)
                    {
                        keySet.Add(key);
                    }
                }
            }
            return keySet.OrderBy(k => k).ToList();
        }

        /// <summary>
        /// Get file extension for the specified format
        /// </summary>
        private static string GetFileExtension(SearchOutputFormat format)
        {
            return format switch
            {
                SearchOutputFormat.JSON => "json",
                SearchOutputFormat.CSV => "csv",
                SearchOutputFormat.TSV => "tsv",
                _ => "txt"
            };
        }

        /// <summary>
        /// Escape CSV value to handle commas, quotes, and newlines
        /// </summary>
        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        /// <summary>
        /// Escape TSV value to handle tabs and newlines
        /// </summary>
        private static string EscapeTsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        /// <summary>
        /// Clean up old export files to prevent disk space issues
        /// </summary>
        public static void CleanupOldExports(int maxAgeHours = 24)
        {
            string exportDir = Path.Combine(Application.dataPath, "..", EXPORT_DIRECTORY);
            if (!Directory.Exists(exportDir))
                return;

            DateTime cutoffTime = DateTime.Now.AddHours(-maxAgeHours);
            string[] files = Directory.GetFiles(exportDir, $"{FILE_PREFIX}_*.*");

            foreach (string file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffTime)
                {
                    File.Delete(file);
                }
            }
        }
    }

    /// <summary>
    /// Data structure for JSON export with metadata
    /// </summary>
    [Serializable]
    public class SearchResultsExport
    {
        public ExportMetadata ExportMetadata { get; set; }
        public SearchResultItem[] Results { get; set; }
    }

    /// <summary>
    /// Metadata for exported search results
    /// </summary>
    [Serializable]
    public class ExportMetadata
    {
        public string ExportTimestamp { get; set; }
        public string SearchQuery { get; set; }
        public string[] ProvidersUsed { get; set; }
        public int ResultCount { get; set; }
        public string ExportFormat { get; set; }
    }
} 
