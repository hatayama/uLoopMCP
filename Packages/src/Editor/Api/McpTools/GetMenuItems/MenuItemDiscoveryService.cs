using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Service for discovering Unity MenuItems using reflection
    /// Provides common functionality for MenuItem discovery and lookup
    /// </summary>
    public static class MenuItemDiscoveryService
    {
        /// <summary>
        /// Discovers all MenuItem methods in loaded assemblies
        /// </summary>
        public static List<MenuItemInfo> DiscoverAllMenuItems()
        {
            List<MenuItemInfo> menuItems = new List<MenuItemInfo>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (Assembly assembly in assemblies)
            {
                AddMenuItemsFromAssembly(assembly, menuItems);
            }
            
            return menuItems.OrderBy(item => item.Path).ToList();
        }

        /// <summary>
        /// Finds a specific MenuItem by path
        /// </summary>
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

        private static void AddMenuItemsFromAssembly(Assembly assembly, List<MenuItemInfo> menuItems)
        {
            Type[] types = GetTypesFromAssembly(assembly);
            
            foreach (Type type in types)
            {
                AddMenuItemsFromType(type, menuItems);
            }
        }

        private static MenuItemInfo FindMenuItemInAssembly(Assembly assembly, string menuItemPath)
        {
            Type[] types = GetTypesFromAssembly(assembly);
            
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

        private static Type[] GetTypesFromAssembly(Assembly assembly)
        {
            return assembly.GetTypes();
        }

        private static void AddMenuItemsFromType(Type type, List<MenuItemInfo> menuItems)
        {
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Static | 
                BindingFlags.Public | 
                BindingFlags.NonPublic
            );
            
            foreach (MethodInfo method in methods)
            {
                MenuItemInfo menuItem = CreateMenuItemInfo(method);
                if (menuItem != null)
                {
                    menuItems.Add(menuItem);
                }
            }
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
                MenuItemInfo menuItem = CreateMenuItemInfo(method);
                if (menuItem != null && 
                    string.Equals(menuItem.Path, menuItemPath, StringComparison.OrdinalIgnoreCase))
                {
                    return menuItem;
                }
            }
            
            return null;
        }

        private static MenuItemInfo CreateMenuItemInfo(MethodInfo method)
        {
            MenuItem menuItemAttribute = method.GetCustomAttribute<MenuItem>();
            if (menuItemAttribute == null) return null;
            
            return new MenuItemInfo(
                menuItemAttribute.menuItem,
                method,
                menuItemAttribute.priority,
                menuItemAttribute.validate
            );
        }
    }
} 