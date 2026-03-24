using System.Collections.Generic;
using System.Text;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Validates compiled assemblies before Assembly.Load by scanning raw metadata strings.
    /// This keeps the first enforcement step ahead of load-time side effects even when deeper IL validation still runs later.
    /// </summary>
    internal sealed class PreloadMetadataSecurityValidator
    {
        private const string ModuleInitializerAttributeName = "System.Runtime.CompilerServices.ModuleInitializerAttribute";

        public SecurityValidationResult Validate(byte[] assemblyBytes)
        {
            SecurityValidationResult result = new SecurityValidationResult
            {
                IsValid = true,
                Violations = new List<SecurityViolation>()
            };

            string metadataText = Encoding.UTF8.GetString(assemblyBytes);
            HashSet<string> seenViolations = new HashSet<string>(System.StringComparer.Ordinal);

            this.ValidateDangerousTypes(metadataText, result, seenViolations);
            this.ValidateDangerousMembers(metadataText, result, seenViolations);
            this.ValidateModuleInitializer(metadataText, result, seenViolations);

            result.IsValid = result.Violations.Count == 0;
            return result;
        }

        private void ValidateDangerousTypes(
            string metadataText,
            SecurityValidationResult result,
            HashSet<string> seenViolations)
        {
            foreach (string dangerousTypeName in DangerousApiCatalog.EnumerateDangerousTypes())
            {
                if (!metadataText.Contains(dangerousTypeName))
                {
                    continue;
                }

                this.AddViolation(
                    result,
                    seenViolations,
                    dangerousTypeName,
                    $"Dangerous metadata reference detected before assembly load: {dangerousTypeName}",
                    $"Raw assembly metadata contains type reference '{dangerousTypeName}'");
            }
        }

        private void ValidateDangerousMembers(
            string metadataText,
            SecurityValidationResult result,
            HashSet<string> seenViolations)
        {
            foreach (KeyValuePair<string, HashSet<string>> dangerousMemberEntry in DangerousApiCatalog.EnumerateDangerousMembers())
            {
                if (!metadataText.Contains(dangerousMemberEntry.Key))
                {
                    continue;
                }

                foreach (string dangerousMemberName in dangerousMemberEntry.Value)
                {
                    if (!metadataText.Contains(dangerousMemberName))
                    {
                        continue;
                    }

                    string apiName = $"{dangerousMemberEntry.Key}.{dangerousMemberName}";
                    this.AddViolation(
                        result,
                        seenViolations,
                        apiName,
                        $"Dangerous metadata member detected before assembly load: {apiName}",
                        $"Raw assembly metadata contains member reference '{apiName}'");
                }
            }
        }

        private void ValidateModuleInitializer(
            string metadataText,
            SecurityValidationResult result,
            HashSet<string> seenViolations)
        {
            if (!metadataText.Contains(ModuleInitializerAttributeName))
            {
                return;
            }

            this.AddViolation(
                result,
                seenViolations,
                ModuleInitializerAttributeName,
                $"Dangerous metadata attribute detected before assembly load: {ModuleInitializerAttributeName}",
                $"Raw assembly metadata contains attribute '{ModuleInitializerAttributeName}'");
        }

        private void AddViolation(
            SecurityValidationResult result,
            HashSet<string> seenViolations,
            string apiName,
            string message,
            string description)
        {
            if (!seenViolations.Add($"{apiName}|{message}"))
            {
                return;
            }

            result.Violations.Add(new SecurityViolation
            {
                Type = SecurityViolationType.DangerousApiCall,
                ApiName = apiName,
                Message = message,
                Description = description,
                Location = "metadata"
            });
        }
    }
}
