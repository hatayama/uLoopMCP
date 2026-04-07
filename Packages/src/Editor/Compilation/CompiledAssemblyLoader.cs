using System.Collections.Generic;
using System.Diagnostics;
using Assembly = System.Reflection.Assembly;
using Debug = UnityEngine.Debug;

namespace io.github.hatayama.uLoopMCP
{
    internal static class CompiledAssemblyLoader
    {
        public static CompiledAssemblyLoadResult Load(
            DynamicCodeSecurityLevel securityLevel,
            byte[] assemblyBytes)
        {
            Debug.Assert(assemblyBytes != null, "assemblyBytes must not be null");

            Stopwatch stopwatch = Stopwatch.StartNew();

            if (securityLevel == DynamicCodeSecurityLevel.Restricted)
            {
                SecurityValidationResult metadataValidationResult = ValidateBeforeAssemblyLoad(assemblyBytes);
                if (!metadataValidationResult.IsValid)
                {
                    stopwatch.Stop();
                    return new CompiledAssemblyLoadResult(
                        false,
                        null,
                        metadataValidationResult.Violations,
                        stopwatch.Elapsed.TotalMilliseconds);
                }
            }

            Assembly compiledAssembly = Assembly.Load(assemblyBytes);

            if (securityLevel == DynamicCodeSecurityLevel.Restricted)
            {
                IlSecurityValidator validator = new IlSecurityValidator();
                SecurityValidationResult ilValidationResult = validator.Validate(compiledAssembly);
                if (!ilValidationResult.IsValid)
                {
                    stopwatch.Stop();
                    return new CompiledAssemblyLoadResult(
                        false,
                        null,
                        ilValidationResult.Violations,
                        stopwatch.Elapsed.TotalMilliseconds);
                }
            }

            stopwatch.Stop();
            return new CompiledAssemblyLoadResult(
                true,
                compiledAssembly,
                new List<SecurityViolation>(),
                stopwatch.Elapsed.TotalMilliseconds);
        }

        private static SecurityValidationResult ValidateBeforeAssemblyLoad(byte[] assemblyBytes)
        {
            IPreloadAssemblySecurityValidator registeredValidator;
            if (PreloadAssemblySecurityValidatorRegistry.TryGetValidator(out registeredValidator))
            {
                return registeredValidator.Validate(assemblyBytes);
            }

            SystemReflectionMetadataPreloadValidator fallbackValidator = new SystemReflectionMetadataPreloadValidator();
            return fallbackValidator.Validate(assemblyBytes);
        }
    }
}
