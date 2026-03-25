using System;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Service for discovering Unity MenuItems using reflection.
    /// Used by ExecuteMenuItemUseCase for reflection-based fallback execution.
    /// </summary>
    public static class MenuItemDiscoveryService
    {
        public static MenuItemInfo FindMenuItemByPath(string menuItemPath)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                MenuItemInfo menuItem = FindMenuItemInAssembly(assembly, menuItemPath);
                if (menuItem != null)
                {
                    return menuItem;
                }
            }

            return null;
        }

        private static MenuItemInfo FindMenuItemInAssembly(Assembly assembly, string menuItemPath)
        {
            Type[] types;
            // ReflectionTypeLoadException can occur when some types in the assembly fail to load
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = Array.FindAll(ex.Types, static t => t != null);
            }

            foreach (Type type in types)
            {
                MenuItemInfo menuItem = FindMenuItemInType(type, menuItemPath);
                if (menuItem != null)
                {
                    return menuItem;
                }
            }

            return null;
        }

        private static MenuItemInfo FindMenuItemInType(Type type, string menuItemPath)
        {
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            // Unity requires validate functions to be in the same class as their executable counterpart
            MenuItemInfo validateCandidate = null;
            foreach (MethodInfo method in methods)
            {
                MenuItem[] menuItemAttributes = method.GetCustomAttributes<MenuItem>(false).ToArray();
                if (menuItemAttributes.Length == 0)
                {
                    continue;
                }

                foreach (MenuItem attr in menuItemAttributes)
                {
                    if (!string.Equals(attr.menuItem, menuItemPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    MenuItemInfo info = CreateMenuItemInfo(method, attr, menuItemAttributes);
                    if (!info.IsValidateFunction)
                    {
                        return info;
                    }

                    validateCandidate ??= info;
                }
            }

            return validateCandidate;
        }

        private static MenuItemInfo CreateMenuItemInfo(MethodInfo method, MenuItem matchedAttribute, MenuItem[] allAttributes)
        {
            MenuItemInfo menuItemInfo = new MenuItemInfo(
                matchedAttribute.menuItem,
                method,
                matchedAttribute.validate
            );

            // Multiple [MenuItem] attributes on a single method is unusual; record a warning
            if (allAttributes.Length > 1)
            {
                string methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
                menuItemInfo.WarningMessage = $"Method '{methodName}' has {allAttributes.Length} [MenuItem] attributes. Matched '{matchedAttribute.menuItem}'.";
            }

            return menuItemInfo;
        }
    }
}
