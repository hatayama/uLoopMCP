using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP.Tests
{
    public class HierarchyServiceTests
    {
        private GameObject testRoot;
        private HierarchyService service;
        
        [SetUp]
        public void SetUp()
        {
            service = new HierarchyService();
            testRoot = new GameObject("TestRoot");
        }
        
        [TearDown]
        public void TearDown()
        {
            if (testRoot != null)
                Object.DestroyImmediate(testRoot);
        }
        
        [Test]
        public void GetHierarchyNodes_WithSingleObject_ReturnsOneNode()
        {
            // Arrange
            HierarchyOptions options = new HierarchyOptions();
            
            // Act
            List<HierarchyNode> nodes = service.GetHierarchyNodes(options);
            
            // Assert
            Assert.That(nodes, Is.Not.Null);
            Assert.That(nodes.Count, Is.GreaterThan(0));
        }
        
        [Test]
        public void GetHierarchyNodes_WithNestedObjects_ReturnsCorrectDepth()
        {
            // Arrange
            GameObject child = new GameObject("Child");
            GameObject grandChild = new GameObject("GrandChild");
            child.transform.SetParent(testRoot.transform);
            grandChild.transform.SetParent(child.transform);
            
            HierarchyOptions options = new HierarchyOptions();
            
            // Act
            List<HierarchyNode> nodes = service.GetHierarchyNodes(options);
            
            // Assert
            HierarchyNode grandChildNode = nodes.Find(n => n.name == "GrandChild");
            Assert.That(grandChildNode, Is.Not.Null);
            Assert.That(grandChildNode.depth, Is.EqualTo(2));
        }
        
        [Test]
        public void GetHierarchyNodes_WithMaxDepth_LimitsDepth()
        {
            // Arrange
            GameObject child = new GameObject("Child");
            GameObject grandChild = new GameObject("GrandChild");
            GameObject greatGrandChild = new GameObject("GreatGrandChild");
            child.transform.SetParent(testRoot.transform);
            grandChild.transform.SetParent(child.transform);
            greatGrandChild.transform.SetParent(grandChild.transform);
            
            HierarchyOptions options = new HierarchyOptions { MaxDepth = 1 };
            
            // Act
            List<HierarchyNode> nodes = service.GetHierarchyNodes(options);
            
            // Assert
            Assert.That(nodes.Find(n => n.name == "Child"), Is.Not.Null);
            Assert.That(nodes.Find(n => n.name == "GrandChild"), Is.Null);
            Assert.That(nodes.Find(n => n.name == "GreatGrandChild"), Is.Null);
        }
        
        [Test]
        public void GetHierarchyNodes_WithInactiveFilter_ExcludesInactive()
        {
            // Arrange
            GameObject activeChild = new GameObject("ActiveChild");
            GameObject inactiveChild = new GameObject("InactiveChild");
            activeChild.transform.SetParent(testRoot.transform);
            inactiveChild.transform.SetParent(testRoot.transform);
            inactiveChild.SetActive(false);
            
            HierarchyOptions options = new HierarchyOptions { IncludeInactive = false };
            
            // Act
            List<HierarchyNode> nodes = service.GetHierarchyNodes(options);
            
            // Assert
            Assert.That(nodes.Find(n => n.name == "ActiveChild"), Is.Not.Null);
            Assert.That(nodes.Find(n => n.name == "InactiveChild"), Is.Null);
        }
        
        [Test]
        public void GetHierarchyNodes_WithComponents_IncludesComponentNames()
        {
            // Arrange
            testRoot.AddComponent<BoxCollider>();
            testRoot.AddComponent<Rigidbody>();
            
            HierarchyOptions options = new HierarchyOptions { IncludeComponents = true };
            
            // Act
            List<HierarchyNode> nodes = service.GetHierarchyNodes(options);
            
            // Assert
            HierarchyNode rootNode = nodes.Find(n => n.name == "TestRoot");
            Assert.That(rootNode, Is.Not.Null);
            Assert.That(rootNode.components, Contains.Item("BoxCollider"));
            Assert.That(rootNode.components, Contains.Item("Rigidbody"));
            Assert.That(rootNode.components, Contains.Item("Transform"));
        }
        
        [Test]
        public void GetCurrentContext_InEditor_ReturnsEditorContext()
        {
            // Act
            HierarchyContext context = service.GetCurrentContext();
            
            // Assert
            Assert.That(context, Is.Not.Null);
            Assert.That(context.sceneType, Is.EqualTo("editor").Or.EqualTo("runtime"));
            Assert.That(context.sceneName, Is.Not.Null);
        }
    }
}