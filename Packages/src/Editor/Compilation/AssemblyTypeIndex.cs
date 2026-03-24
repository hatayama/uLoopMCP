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

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;

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
    }
}
