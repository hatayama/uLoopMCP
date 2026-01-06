using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    public class UnitySearchToolTests
    {
        private const string FixtureFolder = "Assets/Tests/Editor/UnitySearchFixtures";
        private const string TextureAssetPath = FixtureFolder + "/UnitySearchTestTexture.asset";
        private const string MeshAssetPath = FixtureFolder + "/UnitySearchTestMesh.asset";
        private const string TextureName = "UnitySearchTestTexture";
        private const string MeshName = "UnitySearchTestMesh";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureAssetPath);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);

            Assert.That(texture, Is.Not.Null, $"Fixture texture missing at {TextureAssetPath}. Create it before running tests.");
            Assert.That(mesh, Is.Not.Null, $"Fixture mesh missing at {MeshAssetPath}. Create it before running tests.");

            AssetDatabase.ImportAsset(TextureAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(MeshAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void ExecuteSearchAsync_ShouldFindFixtureTexture()
        {
            UnitySearchSchema schema = new UnitySearchSchema
            {
                SearchQuery = TextureName,
                Providers = new[] { "asset" },
                IncludeDescription = false,
                IncludeMetadata = true,
                MaxResults = 20,
                AutoSaveThreshold = 0,
                SaveToFile = false,
                SearchFlags = UnitySearchFlags.Synchronous,
                PathFilter = FixtureFolder + "/*"
            };

            UnitySearchResponse response = UnitySearchService.ExecuteSearchAsync(schema).GetAwaiter().GetResult();

            Assert.That(response.Success, Is.True, $"Search failed: {response.ErrorMessage}");
            bool containsTexture = response.Results.Any(item => string.Equals(item.Path, TextureAssetPath, StringComparison.OrdinalIgnoreCase));

            string debugInfo = BuildResultDebugInfo(response);
            Assert.That(containsTexture, Is.True, $"Search results missing fixture texture. Details: {debugInfo}");

            SearchResultItem matched = response.Results.First(item => string.Equals(item.Path, TextureAssetPath, StringComparison.OrdinalIgnoreCase));
            Assert.That(matched.Provider, Is.Not.Empty, "Provider should not be empty");
        }

        [Test]
        public void ExecuteSearchAsync_ShouldFindFixtureMesh()
        {
            UnitySearchSchema schema = new UnitySearchSchema
            {
                SearchQuery = MeshName,
                Providers = new[] { "asset" },
                IncludeDescription = false,
                IncludeMetadata = true,
                MaxResults = 20,
                AutoSaveThreshold = 0,
                SaveToFile = false,
                SearchFlags = UnitySearchFlags.Synchronous,
                PathFilter = FixtureFolder + "/*"
            };

            UnitySearchResponse response = UnitySearchService.ExecuteSearchAsync(schema).GetAwaiter().GetResult();

            Assert.That(response.Success, Is.True, $"Search failed: {response.ErrorMessage}");
            bool containsMesh = response.Results.Any(item => string.Equals(item.Path, MeshAssetPath, StringComparison.OrdinalIgnoreCase));

            string debugInfo = BuildResultDebugInfo(response);
            Assert.That(containsMesh, Is.True, $"Search results missing fixture mesh. Details: {debugInfo}");

            SearchResultItem matched = response.Results.First(item => string.Equals(item.Path, MeshAssetPath, StringComparison.OrdinalIgnoreCase));
            Assert.That(matched.Provider, Is.Not.Empty, "Provider should not be empty");
        }

        private string BuildResultDebugInfo(UnitySearchResponse response)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append($"Success={response.Success}");
            builder.Append($", SavedToFile={response.ResultsSavedToFile}");
            builder.Append($", ResultCount={response.Results.Length}");
            builder.Append(", Paths=[");

            bool first = true;
            foreach (SearchResultItem item in response.Results)
            {
                if (!first)
                {
                    builder.Append("; ");
                }

                builder.Append(item.Path);
                builder.Append(" (Type=");
                builder.Append(item.Type);
                builder.Append(")");
                first = false;
            }

            builder.Append("]");
            return builder.ToString();
        }
    }
}
