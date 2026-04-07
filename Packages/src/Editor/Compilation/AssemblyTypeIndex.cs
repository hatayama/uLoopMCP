using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Builds a type-name → namespace lookup from loaded assemblies.
    /// Used by AutoUsingResolver to find missing using directives without Roslyn.
    /// </summary>
    internal sealed class AssemblyTypeIndex
    {
        private static AssemblyTypeIndex _instance;
        private readonly Dictionary<string, HashSet<string>> _typeToNamespaces = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _typeToAssemblyLocations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _namespaceToAssemblyLocations = new(StringComparer.Ordinal);

        public static AssemblyTypeIndex Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AssemblyTypeIndex();
                    _instance.Build();
                }
                return _instance;
            }
        }

        [InitializeOnLoadMethod]
        private static void InvalidateOnDomainReload()
        {
            _instance = null;
        }

        private void Build()
        {
            _typeToNamespaces.Clear();
            _typeToAssemblyLocations.Clear();
            _namespaceToAssemblyLocations.Clear();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;

                string assemblyLocation = GetAssemblyLocation(assembly);
                Type[] types;
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
                    if (type == null || string.IsNullOrEmpty(type.Namespace)) continue;
                    if (!type.IsPublic) continue;

                    string typeName = type.Name;

                    // Strip generic arity suffix (e.g. "List`1" → "List")
                    int backtick = typeName.IndexOf('`');
                    if (backtick > 0)
                    {
                        typeName = typeName.Substring(0, backtick);
                    }

                    if (!_typeToNamespaces.TryGetValue(typeName, out HashSet<string> namespaces))
                    {
                        namespaces = new HashSet<string>(StringComparer.Ordinal);
                        _typeToNamespaces[typeName] = namespaces;
                    }
                    namespaces.Add(type.Namespace);

                    if (!string.IsNullOrEmpty(assemblyLocation))
                    {
                        RegisterTypeAssemblyLocation(typeName, assemblyLocation);
                        RegisterNamespaceAssemblyLocation(type.Namespace, assemblyLocation);
                    }
                }
            }
        }

        public List<string> FindNamespacesForType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return new List<string>();

            if (_typeToNamespaces.TryGetValue(typeName, out HashSet<string> namespaces))
            {
                return new List<string>(namespaces);
            }
            return new List<string>();
        }

        public List<string> FindAssemblyLocationsForType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return new List<string>();
            }

            if (_typeToAssemblyLocations.TryGetValue(typeName, out HashSet<string> assemblyLocations))
            {
                return new List<string>(assemblyLocations);
            }

            return new List<string>();
        }

        public List<string> FindAssemblyLocationsForIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return new List<string>();
            }

            if (_typeToAssemblyLocations.TryGetValue(identifier, out HashSet<string> typeAssemblyLocations))
            {
                return new List<string>(typeAssemblyLocations);
            }

            if (_namespaceToAssemblyLocations.TryGetValue(identifier, out HashSet<string> namespaceAssemblyLocations))
            {
                return new List<string>(namespaceAssemblyLocations);
            }

            return new List<string>();
        }

        private static string GetAssemblyLocation(Assembly assembly)
        {
            try
            {
                return assembly.Location;
            }
            catch (NotSupportedException)
            {
                return string.Empty;
            }
        }

        private void RegisterTypeAssemblyLocation(string typeName, string assemblyLocation)
        {
            if (!_typeToAssemblyLocations.TryGetValue(typeName, out HashSet<string> assemblyLocations))
            {
                assemblyLocations = new HashSet<string>(StringComparer.Ordinal);
                _typeToAssemblyLocations[typeName] = assemblyLocations;
            }

            assemblyLocations.Add(assemblyLocation);
        }

        private void RegisterNamespaceAssemblyLocation(string namespaceName, string assemblyLocation)
        {
            string[] namespaceParts = namespaceName.Split('.');
            string currentNamespace = string.Empty;

            foreach (string namespacePart in namespaceParts)
            {
                currentNamespace = currentNamespace.Length == 0
                    ? namespacePart
                    : $"{currentNamespace}.{namespacePart}";

                if (!_namespaceToAssemblyLocations.TryGetValue(currentNamespace, out HashSet<string> assemblyLocations))
                {
                    assemblyLocations = new HashSet<string>(StringComparer.Ordinal);
                    _namespaceToAssemblyLocations[currentNamespace] = assemblyLocations;
                }

                assemblyLocations.Add(assemblyLocation);
            }
        }
    }
}
