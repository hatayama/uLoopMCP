using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace io.github.hatayama.uLoopMCP
{
    public class GetHierarchyToolTests
    {
        private GetHierarchyTool tool;
        private GameObject testRoot;
        
        [SetUp]
        public void SetUp()
        {
            tool = new GetHierarchyTool();
            testRoot = new GameObject("TestRoot");
        }
        
        [TearDown]
        public void TearDown()
        {
            if (testRoot != null)
                Object.DestroyImmediate(testRoot);
        }
        
        [Test]
        public void ToolName_ReturnsCorrectName()
        {
            Assert.That(tool.ToolName, Is.EqualTo("get-hierarchy"));
        }
        
        [Test]
        public async Task ExecuteAsync_WithDefaultParameters_ReturnsValidResponse()
        {
            // Arrange
            JObject paramsJson = new JObject();
            
            // Act
            BaseToolResponse baseResponse = await tool.ExecuteAsync(paramsJson);
            GetHierarchyResponse response = baseResponse as GetHierarchyResponse;
            
            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.hierarchy, Is.Not.Null);
            Assert.That(response.context, Is.Not.Null);
            Assert.That(response.context.sceneType, Is.Not.Null);
        }
        
        [Test]
        public async Task ExecuteAsync_WithMaxDepthParameter_LimitsDepth()
        {
            // Arrange
            GameObject child = new GameObject("Child");
            GameObject grandChild = new GameObject("GrandChild");
            child.transform.SetParent(testRoot.transform);
            grandChild.transform.SetParent(child.transform);
            
            JObject paramsJson = new JObject
            {
                ["MaxDepth"] = 1
            };
            
            // Act
            BaseToolResponse baseResponse = await tool.ExecuteAsync(paramsJson);
            GetHierarchyResponse response = baseResponse as GetHierarchyResponse;
            
            // Assert
            Assert.That(response, Is.Not.Null);
            // Find nodes in nested structure
            HierarchyNodeNested rootNode = response.hierarchy.Find(n => n.name == "TestRoot");
            Assert.That(rootNode, Is.Not.Null, "MaxDepth=1 should include depth 0 objects");
            
            HierarchyNodeNested childNode = rootNode?.children.Find(n => n.name == "Child");
            Assert.That(childNode, Is.Not.Null, "MaxDepth=1 should include depth 1 objects");
            
            HierarchyNodeNested grandChildNode = childNode?.children.Find(n => n.name == "GrandChild");
            Assert.That(grandChildNode, Is.Null, "MaxDepth=1 should not include depth 2 objects");
        }
        
        [Test]
        public async Task ExecuteAsync_WithIncludeComponentsFalse_ExcludesComponents()
        {
            // Arrange
            testRoot.AddComponent<BoxCollider>();
            
            JObject paramsJson = new JObject
            {
                ["IncludeComponents"] = false
            };
            
            // Act
            BaseToolResponse baseResponse = await tool.ExecuteAsync(paramsJson);
            GetHierarchyResponse response = baseResponse as GetHierarchyResponse;
            
            // Assert
            Assert.That(response, Is.Not.Null);
            HierarchyNodeNested rootNode = response.hierarchy.Find(n => n.name == "TestRoot");
            if (rootNode != null)
            {
                Assert.That(rootNode.components.Length, Is.EqualTo(0));
            }
        }
        
        [Test]
        public void ParameterSchema_HasCorrectProperties()
        {
            // Act
            ToolParameterSchema schema = tool.ParameterSchema;
            
            // Assert
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.Properties, Is.Not.Null);
            Assert.That(schema.Properties.ContainsKey("IncludeInactive"), Is.True);
            Assert.That(schema.Properties.ContainsKey("MaxDepth"), Is.True);
            Assert.That(schema.Properties.ContainsKey("RootPath"), Is.True);
            Assert.That(schema.Properties.ContainsKey("IncludeComponents"), Is.True);
        }
    }
}