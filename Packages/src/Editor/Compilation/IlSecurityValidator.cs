using System;
using System.Collections.Generic;
using System.Reflection;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Post-compilation security validator using reflection.
    /// Inspects the compiled assembly's type references and method calls
    /// against DangerousApiCatalog before allowing execution.
    /// </summary>
    internal sealed class IlSecurityValidator
    {
        public SecurityValidationResult Validate(Assembly assembly)
        {
            SecurityValidationResult result = new SecurityValidationResult
            {
                IsValid = true,
                Violations = new List<SecurityViolation>()
            };

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
                ValidateTypeHierarchy(type, result);
                ValidateMethodReferences(type, result);
            }

            result.IsValid = result.Violations.Count == 0;
            return result;
        }

        private static void ValidateTypeHierarchy(Type type, SecurityValidationResult result)
        {
            if (type.BaseType != null && DangerousApiCatalog.IsDangerousType(type.BaseType.FullName))
            {
                result.Violations.Add(new SecurityViolation
                {
                    Type = SecurityViolationType.DangerousApiCall,
                    ApiName = type.BaseType.FullName,
                    Message = $"Inheriting from dangerous type: {type.BaseType.FullName}",
                    Description = $"Type '{type.FullName}' inherits from blocked type '{type.BaseType.FullName}'"
                });
            }

            foreach (Type iface in type.GetInterfaces())
            {
                if (DangerousApiCatalog.IsDangerousType(iface.FullName))
                {
                    result.Violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        ApiName = iface.FullName,
                        Message = $"Implementing dangerous interface: {iface.FullName}",
                        Description = $"Type '{type.FullName}' implements blocked interface '{iface.FullName}'"
                    });
                }
            }
        }

        private static void ValidateMethodReferences(Type type, SecurityValidationResult result)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.Static |
                                 BindingFlags.DeclaredOnly;

            foreach (MethodInfo method in type.GetMethods(flags))
            {
                ValidateMethodBody(method, result);
            }

            foreach (ConstructorInfo ctor in type.GetConstructors(flags))
            {
                ValidateMethodBody(ctor, result);
            }
        }

        private static void ValidateMethodBody(MethodBase method, SecurityValidationResult result)
        {
            MethodBody body;
            try
            {
                body = method.GetMethodBody();
            }
            catch
            {
                // Abstract/extern methods have no body
                return;
            }
            if (body == null) return;

            foreach (LocalVariableInfo local in body.LocalVariables)
            {
                string typeName = local.LocalType?.FullName;
                if (typeName != null && DangerousApiCatalog.IsDangerousType(typeName))
                {
                    result.Violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        ApiName = typeName,
                        Message = $"Using dangerous type as local variable: {typeName}",
                        Description = $"Local variable of type '{typeName}' in {method.DeclaringType?.FullName}.{method.Name}"
                    });
                }
            }

            foreach (System.Reflection.ParameterInfo param in method.GetParameters())
            {
                string typeName = param.ParameterType?.FullName;
                if (typeName != null && DangerousApiCatalog.IsDangerousType(typeName))
                {
                    result.Violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        ApiName = typeName,
                        Message = $"Using dangerous type as parameter: {typeName}",
                        Description = $"Parameter '{param.Name}' of type '{typeName}' in {method.DeclaringType?.FullName}.{method.Name}"
                    });
                }
            }

            // typeof(DangerousType) compiles to ldtoken (0xD0) + metadata token
            ValidateTypeofReferences(method, body, result);
        }

        private const byte OpCode_Ldtoken = 0xD0;

        private static void ValidateTypeofReferences(MethodBase method, MethodBody body, SecurityValidationResult result)
        {
            byte[] il = body.GetILAsByteArray();
            if (il == null) return;

            Module module = method.Module;

            for (int i = 0; i < il.Length; i++)
            {
                if (il[i] != OpCode_Ldtoken || i + 4 >= il.Length) continue;

                int token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);

                Type resolvedType;
                try
                {
                    resolvedType = module.ResolveType(token);
                }
                catch
                {
                    // ldtoken can also reference fields/methods, not just types
                    continue;
                }

                string fullName = resolvedType.FullName;
                if (fullName != null && DangerousApiCatalog.IsDangerousType(fullName))
                {
                    result.Violations.Add(new SecurityViolation
                    {
                        Type = SecurityViolationType.DangerousApiCall,
                        ApiName = fullName,
                        Message = $"typeof() reference to dangerous type: {fullName}",
                        Description = $"typeof({fullName}) in {method.DeclaringType?.FullName}.{method.Name}"
                    });
                }

                i += 4;
            }
        }
    }
}
