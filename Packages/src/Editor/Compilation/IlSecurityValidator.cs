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

            // Inspect local variable types for dangerous type instantiation
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

            // Inspect parameters for dangerous types
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
        }
    }
}
