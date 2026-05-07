using UnityEngine;

namespace io.github.hatayama.UnityCliLoop
{
    /// <summary>
    /// Provides the CLI readiness version payload without registering an extension-facing tool.
    /// </summary>
    internal static class GetVersionBridgeCommand
    {
        public static GetVersionResponse Execute()
        {
            GetVersionResponse response = new()            {
                UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString(),
                DataPath = Application.dataPath,
                PersistentDataPath = Application.persistentDataPath,
                TemporaryCachePath = Application.temporaryCachePath,
                IsEditor = Application.isEditor,
                ProductName = Application.productName,
                CompanyName = Application.companyName
            };

            Debug.Assert(!string.IsNullOrWhiteSpace(response.UnityVersion), "Unity version must be available.");
            return response;
        }
    }
}
