using System.Collections.Generic;
using NUnit.Framework;

namespace io.github.hatayama.uLoopMCP
{
    public class HierarchySerializerTests
    {
        private HierarchySerializer serializer;
        
        [SetUp]
        public void SetUp()
        {
            serializer = new HierarchySerializer();
        }
        
        [Test]
        public void SerializeHierarchy_WithValidNodes_ReturnsCorrectResponse()
        {
            // Arrange
            List<HierarchyNode> nodes = new List<HierarchyNode>
            {
                new HierarchyNode(1, "Root", null, 0, true, new[] { "Transform" }),
                new HierarchyNode(2, "Child", 1, 1, true, new[] { "Transform", "MeshRenderer" })
            };
            
            HierarchyContext context = new HierarchyContext("editor", "TestScene", 0, 0);
            
            // Act
            GetHierarchyResponse response = serializer.SerializeHierarchy(nodes, context);
            
            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.hierarchy, Is.Not.Null);
            Assert.That(response.hierarchy.Count, Is.EqualTo(2));
            Assert.That(response.context, Is.Not.Null);
            Assert.That(response.context.nodeCount, Is.EqualTo(2));
            Assert.That(response.context.maxDepth, Is.EqualTo(1));
        }
        
        [Test]
        public void SerializeHierarchy_WithEmptyNodes_ReturnsEmptyResponse()
        {
            // Arrange
            List<HierarchyNode> nodes = new List<HierarchyNode>();
            HierarchyContext context = new HierarchyContext("editor", "EmptyScene", 0, 0);
            
            // Act
            GetHierarchyResponse response = serializer.SerializeHierarchy(nodes, context);
            
            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.hierarchy, Is.Not.Null);
            Assert.That(response.hierarchy.Count, Is.EqualTo(0));
            Assert.That(response.context.nodeCount, Is.EqualTo(0));
            Assert.That(response.context.maxDepth, Is.EqualTo(0));
        }
        
        [Test]
        public void SerializeHierarchy_CalculatesCorrectMaxDepth()
        {
            // Arrange
            List<HierarchyNode> nodes = new List<HierarchyNode>
            {
                new HierarchyNode(1, "Root", null, 0, true, new string[0]),
                new HierarchyNode(2, "Level1", 1, 1, true, new string[0]),
                new HierarchyNode(3, "Level2", 2, 2, true, new string[0]),
                new HierarchyNode(4, "Level3", 3, 3, true, new string[0])
            };
            
            HierarchyContext context = new HierarchyContext("editor", "DeepScene", 0, 0);
            
            // Act
            GetHierarchyResponse response = serializer.SerializeHierarchy(nodes, context);
            
            // Assert
            Assert.That(response.context.maxDepth, Is.EqualTo(3));
        }
    }
}