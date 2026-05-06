using System;
using System.Reflection;
using NUnit.Framework;

namespace io.github.hatayama.UnityCliLoop
{
    public class UnityCliLoopServerStartupProtectionTests
    {
        [Test]
        public void ClearStartupProtection_ResetsProtectionWindow()
        {
            Type controllerType = typeof(UnityCliLoopServerController);
            FieldInfo field = controllerType.GetField("startupProtectionUntilTicks", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo method = controllerType.GetMethod("ClearStartupProtection", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field, "startupProtectionUntilTicks field should exist");
            Assert.IsNotNull(method, "ClearStartupProtection method should exist");

            try
            {
                long futureTicks = DateTime.UtcNow.AddMinutes(1).Ticks;
                field.SetValue(null, futureTicks);

                Assert.IsTrue(UnityCliLoopServerController.IsStartupProtectionActive(), "Startup protection should be active after setting future ticks");

                method.Invoke(null, null);

                Assert.IsFalse(UnityCliLoopServerController.IsStartupProtectionActive(), "Startup protection should be cleared by recovery path");
            }
            finally
            {
                field.SetValue(null, 0L);
            }
        }

        [Test]
        public void OnBeforeAssemblyReload_ShouldClearStartupProtectionBeforeRecovery()
        {
            Type controllerType = typeof(UnityCliLoopServerController);
            FieldInfo field = controllerType.GetField("startupProtectionUntilTicks", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo serverField = controllerType.GetField("bridgeServer", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo method = controllerType.GetMethod("OnBeforeAssemblyReload", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field, "startupProtectionUntilTicks field should exist");
            Assert.IsNotNull(serverField, "bridgeServer field should exist");
            Assert.IsNotNull(method, "OnBeforeAssemblyReload method should exist");

            object originalServer = serverField.GetValue(null);
            UnityCliLoopEditorSettingsData originalSettings = CloneSettings(UnityCliLoopEditorSettings.GetSettings());

            try
            {
                serverField.SetValue(null, new UnityCliLoopBridgeServer());
                long futureTicks = DateTime.UtcNow.AddMinutes(1).Ticks;
                field.SetValue(null, futureTicks);

                Assert.IsTrue(UnityCliLoopServerController.IsStartupProtectionActive(), "Startup protection should be active before reload");

                method.Invoke(null, null);

                Assert.IsFalse(
                    UnityCliLoopServerController.IsStartupProtectionActive(),
                    "Assembly reload recovery should clear startup protection so the server can restart"
                );
            }
            finally
            {
                serverField.SetValue(null, originalServer);
                UnityCliLoopEditorSettings.SaveSettings(originalSettings);
                DomainReloadDetectionService.DeleteLockFile();
                field.SetValue(null, 0L);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task StopServerWithUseCaseAsync_ShouldClearStartupProtectionBeforeShutdown()
        {
            Type controllerType = typeof(UnityCliLoopServerController);
            FieldInfo field = controllerType.GetField("startupProtectionUntilTicks", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo serverField = controllerType.GetField("bridgeServer", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo method = controllerType.GetMethod("StopServerWithUseCaseAsync", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field, "startupProtectionUntilTicks field should exist");
            Assert.IsNotNull(serverField, "bridgeServer field should exist");
            Assert.IsNotNull(method, "StopServerWithUseCaseAsync method should exist");

            object originalServer = serverField.GetValue(null);
            UnityCliLoopEditorSettingsData originalSettings = CloneSettings(UnityCliLoopEditorSettings.GetSettings());

            try
            {
                serverField.SetValue(null, new UnityCliLoopBridgeServer());
                long futureTicks = DateTime.UtcNow.AddMinutes(1).Ticks;
                field.SetValue(null, futureTicks);

                Assert.IsTrue(UnityCliLoopServerController.IsStartupProtectionActive(), "Startup protection should be active before shutdown");

                System.Threading.Tasks.Task task = (System.Threading.Tasks.Task)method.Invoke(null, null);
                await task;

                Assert.IsFalse(
                    UnityCliLoopServerController.IsStartupProtectionActive(),
                    "Shutdown path should clear startup protection so recovery can restart the server"
                );
            }
            finally
            {
                serverField.SetValue(null, originalServer);
                UnityCliLoopEditorSettings.SaveSettings(originalSettings);
                field.SetValue(null, 0L);
            }
        }

        private static UnityCliLoopEditorSettingsData CloneSettings(UnityCliLoopEditorSettingsData settings)
        {
            string json = UnityEngine.JsonUtility.ToJson(settings);
            return UnityEngine.JsonUtility.FromJson<UnityCliLoopEditorSettingsData>(json);
        }
    }
}
