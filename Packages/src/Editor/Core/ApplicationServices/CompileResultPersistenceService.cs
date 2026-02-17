using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Persist compile responses for delayed retrieval after domain reload.
    /// </summary>
    public static class CompileResultPersistenceService
    {
        private static string ProjectRootPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string CompileResultDirectoryPath => Path.Combine(
            ProjectRootPath,
            McpConstants.TEMP_DIR,
            McpConstants.ULOOPMCP_DIR,
            McpConstants.COMPILE_RESULTS_DIR
        );

        public static void ClearAllStoredResults()
        {
            if (!Directory.Exists(CompileResultDirectoryPath))
            {
                return;
            }

            string searchPattern = $"*{McpConstants.JSON_FILE_EXTENSION}";
            string[] resultFiles = Directory.GetFiles(CompileResultDirectoryPath, searchPattern);
            foreach (string resultFilePath in resultFiles)
            {
                File.Delete(resultFilePath);
            }
        }

        public static void SaveResult(string requestId, CompileResponse response)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(requestId), "requestId must not be null or empty");
            Debug.Assert(response != null, "response must not be null");

            if (!Directory.Exists(CompileResultDirectoryPath))
            {
                Directory.CreateDirectory(CompileResultDirectoryPath);
            }

            string resultJson = JsonConvert.SerializeObject(response, Formatting.None);
            string fileName = $"{requestId}{McpConstants.JSON_FILE_EXTENSION}";
            string filePath = Path.Combine(CompileResultDirectoryPath, fileName);
            File.WriteAllText(filePath, resultJson, Encoding.UTF8);
        }
    }
}
