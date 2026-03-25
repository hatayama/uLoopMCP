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

            foreach (MethodInfo method in methods)
            {
                MenuItem[] menuItemAttributes = method.GetCustomAttributes<MenuItem>(false).ToArray();
                if (menuItemAttributes.Length == 0)
                {
                    continue;
                }

                // Short-circuit: compare path before creating full MenuItemInfo object
                if (!string.Equals(menuItemAttributes[0].menuItem, menuItemPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return CreateMenuItemInfo(method, menuItemAttributes);
            }

            return null;
        }

        private static MenuItemInfo CreateMenuItemInfo(MethodInfo method, MenuItem[] menuItemAttributes)
        {
            MenuItem menuItemAttribute = menuItemAttributes[0];
            MenuItemInfo menuItemInfo = new MenuItemInfo(
                menuItemAttribute.menuItem,
                method,
                menuItemAttribute.validate
            );

            // Multiple [MenuItem] attributes on a single method is unusual; record a warning
            if (menuItemAttributes.Length > 1)
            {
                string methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
                menuItemInfo.WarningMessage = $"Method '{methodName}' has {menuItemAttributes.Length} duplicate MenuItem attributes for '{menuItemAttribute.menuItem}'. Using the first one.";
            }

            return menuItemInfo;
        }
    }
}
