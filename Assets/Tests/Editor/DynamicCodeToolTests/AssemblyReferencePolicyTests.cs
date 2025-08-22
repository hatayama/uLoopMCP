using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP.DynamicCodeToolTests
{
    /// <summary>
    /// AssemblyReferencePolicyのテスト
    /// セキュリティレベル別のアセンブリ参照ポリシーを確認
    /// </summary>
    [TestFixture]
    public class AssemblyReferencePolicyTests
    {
        [Test]
        public void Level0_GetAssembliesが空のリストを返すか確認()
        {
            // Act
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(DynamicCodeSecurityLevel.Disabled);
            
            // Assert
            Assert.IsNotNull(assemblies);
            Assert.AreEqual(0, assemblies.Count);
        }

        [Test]
        public void Level1_GetAssembliesにUnityEngineが含まれるか確認()
        {
            // Act
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(DynamicCodeSecurityLevel.Restricted);
            
            // Assert
            Assert.IsNotNull(assemblies);
            Assert.Greater(assemblies.Count, 0);
            Assert.IsTrue(assemblies.Any(a => a.StartsWith("UnityEngine")));
        }

        [Test]
        public void Level1_GetAssembliesにUnityEditorが含まれるか確認()
        {
            // Act
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(DynamicCodeSecurityLevel.Restricted);
            
            // Assert
            Assert.IsNotNull(assemblies);
            Assert.IsTrue(assemblies.Any(a => a.StartsWith("UnityEditor")));
        }

        [Test]
        public void Level1_GetAssembliesにSystemIOも含まれるか確認()
        {
            // 新仕様: Restrictedモードでも全アセンブリを含む
            // Act
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(DynamicCodeSecurityLevel.Restricted);
            
            // Assert
            Assert.IsNotNull(assemblies);
            // System系のアセンブリが含まれていることを確認
            Assert.IsTrue(assemblies.Any(a => a.StartsWith("System")), "System assemblies should be included in Restricted mode");
        }

        [Test]
        public void Level1_GetAssembliesにAssemblyCSharpが含まれるか確認()
        {
            // Arrange & Act
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(DynamicCodeSecurityLevel.Restricted);
            
            // Assert
            Assert.IsNotNull(assemblies);
            // 新仕様: RestrictedモードでもAssembly-CSharpを許可（ユーザー定義クラス実行機能）
            // Assembly-CSharpが存在する場合は含まれるはず
            Assembly assemblyCSharp = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            
            if (assemblyCSharp != null)
            {
                Assert.IsTrue(assemblies.Any(a => a == "Assembly-CSharp"), 
                    "Level 1 (Restricted) should include Assembly-CSharp when it exists");
            }
        }

        [Test]
        public void Level2_GetAssembliesに多くのアセンブリが含まれるか確認()
        {
            // Act
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(DynamicCodeSecurityLevel.FullAccess);
            
            // Assert
            Assert.IsNotNull(assemblies);
            Assert.Greater(assemblies.Count, 10); // 少なくとも10個以上のアセンブリ
            Assert.IsTrue(assemblies.Any(a => a.StartsWith("System")));
            Assert.IsTrue(assemblies.Any(a => a.StartsWith("UnityEngine")));
        }

        [Test]
        public void Level2_AssemblyCSharpが含まれるか確認()
        {
            // Arrange & Act
            IReadOnlyList<string> assemblies = AssemblyReferencePolicy.GetAssemblies(DynamicCodeSecurityLevel.FullAccess);
            
            // Assert  
            Assert.IsNotNull(assemblies);
            // Assembly-CSharpはLevel 2 (FullAccess)では利用可能
            Assert.IsTrue(assemblies.Any(a => a == "Assembly-CSharp"), "Level 2 (FullAccess) should include Assembly-CSharp");
        }

        [Test]
        public void IsAssemblyAllowed_Level0で全て拒否されるか確認()
        {
            // Act & Assert
            Assert.IsFalse(AssemblyReferencePolicy.IsAssemblyAllowed("UnityEngine", DynamicCodeSecurityLevel.Disabled));
            Assert.IsFalse(AssemblyReferencePolicy.IsAssemblyAllowed("System", DynamicCodeSecurityLevel.Disabled));
            Assert.IsFalse(AssemblyReferencePolicy.IsAssemblyAllowed("System.IO", DynamicCodeSecurityLevel.Disabled));
        }

        [Test]
        public void IsAssemblyAllowed_Level1でUnityEngineが許可されるか確認()
        {
            // Act & Assert
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("UnityEngine", DynamicCodeSecurityLevel.Restricted));
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("UnityEngine.CoreModule", DynamicCodeSecurityLevel.Restricted));
        }

                [Test]
        public void IsAssemblyAllowed_Level1でSystemIOも許可されるか確認()
        {
            // 新仕様: Restrictedモードでも全アセンブリを許可（コンパイル後に危険なAPIをブロック）
            // Act & Assert
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("System.IO", DynamicCodeSecurityLevel.Restricted));
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("System.IO.FileSystem", DynamicCodeSecurityLevel.Restricted));
        }

        [Test]
        public void IsAssemblyAllowed_Level1でSystemNetも許可されるか確認()
        {
            // 新仕様: Restrictedモードでも全アセンブリを許可（コンパイル後に危険なAPIをブロック）
            // Act & Assert
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("System.Net", DynamicCodeSecurityLevel.Restricted));
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("System.Net.Http", DynamicCodeSecurityLevel.Restricted));
        }

        [Test]
        public void IsAssemblyAllowed_Level1でAssemblyCSharpが許可されるか確認()
        {
            // 新仕様: RestrictedモードでもAssembly-CSharpを許可（ユーザー定義クラス実行機能）
            // Act & Assert
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("Assembly-CSharp", DynamicCodeSecurityLevel.Restricted),
                "Level 1 (Restricted) should allow Assembly-CSharp (new feature)");
        }

        [Test]
        public void IsAssemblyAllowed_Level2で基本的なアセンブリが許可されるか確認()
        {
            // Act & Assert
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("UnityEngine", DynamicCodeSecurityLevel.FullAccess));
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("System", DynamicCodeSecurityLevel.FullAccess));
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("mscorlib", DynamicCodeSecurityLevel.FullAccess));
        }

        [Test]
        public void IsAssemblyAllowed_Level2でAssemblyCSharpが許可されるか確認()
        {
            // Act & Assert
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("Assembly-CSharp", DynamicCodeSecurityLevel.FullAccess),
                "Level 2 (FullAccess) should allow Assembly-CSharp");
        }

        [Test]
        public void IsAssemblyAllowed_Level2でSystemReflectionEmitも許可されるか確認()
        {
            // 新仕様: FullAccessモードでは全アセンブリを許可
            // Act & Assert
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("System.Reflection.Emit", DynamicCodeSecurityLevel.FullAccess),
                "System.Reflection.Emit should be allowed in Level 2");
            Assert.IsTrue(AssemblyReferencePolicy.IsAssemblyAllowed("System.CodeDom", DynamicCodeSecurityLevel.FullAccess),
                "System.CodeDom should be allowed in Level 2");
        }

        [Test]
        public void IsAssemblyAllowed_空文字列やnullで常にfalseを返すか確認()
        {
            // Act & Assert
            Assert.IsFalse(AssemblyReferencePolicy.IsAssemblyAllowed("", DynamicCodeSecurityLevel.FullAccess));
            Assert.IsFalse(AssemblyReferencePolicy.IsAssemblyAllowed(null, DynamicCodeSecurityLevel.FullAccess));
            Assert.IsFalse(AssemblyReferencePolicy.IsAssemblyAllowed("  ", DynamicCodeSecurityLevel.FullAccess));
        }
    }
}