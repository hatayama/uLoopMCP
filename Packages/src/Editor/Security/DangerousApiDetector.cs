#if ULOOPMCP_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// An engine for managing and detecting dangerous API patterns
    /// Related Classes: SecuritySyntaxWalker, SecurityValidator
    /// </summary>
    public class DangerousApiDetector
    {
        private static readonly HashSet<string> dangerousTypes = new()
        {
            // System.IO related (FileInfo and DirectoryInfo types are entirely dangerous)
            "System.IO.FileInfo",
            "System.IO.DirectoryInfo",
            "System.IO.Path",
            
            // Network-related
            "System.Net.Http.HttpClient",
            "System.Net.WebClient",
            "System.Net.WebRequest",
            "System.Net.Sockets.Socket",
            "System.Net.Sockets.TcpClient",
            
            // Process-related
            "System.Diagnostics.ProcessStartInfo",
            
            // Task-related
            "System.Threading.Tasks.Task",
            
            // Web-related (Not typically available in Unity, but checked for safety)
            "System.Web.HttpContext",
            "System.Web.HttpRequest",
            "System.Web.HttpResponse",
            
            // UnityEngine.Networking related
            "UnityEngine.Networking.UnityWebRequest",
            "UnityEngine.Networking.NetworkTransport",
            
            // System.Data related
            "System.Data.SqlClient.SqlConnection",
            "System.Data.SqlClient.SqlCommand",
            "System.Data.DataSet",
            
            // System.Runtime.Remoting related
            "System.Runtime.Remoting.RemotingConfiguration",
            "System.Runtime.Remoting.RemotingServices",
            
            // System.Security.Cryptography related (Certificate manipulation)
            "System.Security.Cryptography.X509Certificates.X509Certificate",
            "System.Security.Cryptography.X509Certificates.X509Store"
        };
        
        // Statically declare dangerous members (per type)
        private static readonly Dictionary<string, List<string>> dangerousMembers = new()
        {
            // System.IO.File - Deletion and write operations are dangerous
            ["System.IO.File"] = new() 
            { 
                "Delete", "WriteAllText", "WriteAllBytes", "Replace"
                // Create, Copy, Move, ReadAllText, ReadAllBytes, Exists, Open-related methods are relatively safe
            },
            
            // System.IO.Directory - Deletion is dangerous
            ["System.IO.Directory"] = new() 
            { 
                "Delete"
                // Create, GetFiles, GetDirectories, Move, Exists are relatively safe
            },
            
            // System.Diagnostics.Process - Starting and forcibly terminating are dangerous
            ["System.Diagnostics.Process"] = new() 
            { 
                "Start", "Kill"
                // GetProcesses, GetCurrentProcess and similar are relatively safe as they only retrieve information
            },
            
            // System.Reflection.Assembly - Dynamic loading is dangerous
            ["System.Reflection.Assembly"] = new() 
            { 
                "Load", "LoadFrom", "LoadFile", "LoadWithPartialName"
                // GetExecutingAssembly and similar are relatively safe as they only retrieve information
            },
            
            // System.Type - Dynamic method invocation is dangerous
            ["System.Type"] = new() 
            { 
                "InvokeMember"
                // GetType, GetMethod and similar are relatively safe as they only retrieve information
            },
            
            // System.Activator - Creating COM objects is dangerous
            ["System.Activator"] = new() 
            { 
                "CreateComInstanceFrom"
                // CreateInstance is permitted depending on its use case
            },
            
            // UnityEditor.AssetDatabase - Permanent data deletion is dangerous
            ["UnityEditor.AssetDatabase"] = new() 
            { 
                "DeleteAsset"
                // MoveAsset, CopyAsset, CreateAsset are relatively safe and therefore excluded
            },
            
            // UnityEditor.FileUtil - File deletion is dangerous
            ["UnityEditor.FileUtil"] = new() 
            { 
                "DeleteFileOrDirectory"
                // CopyFileOrDirectory, MoveFileOrDirectory are relatively safe
            },
            
            // System.Environment - Application termination is dangerous
            ["System.Environment"] = new() 
            {
                "Exit", "FailFast"
                // SetEnvironmentVariable, ExpandEnvironmentVariables are relatively safe
            },
            
            // System.Threading.Thread - Forced thread manipulation is dangerous
            ["System.Threading.Thread"] = new() 
            {
                "Abort", "Suspend", "Resume"
                // Start, Join are relatively safe
            },
            
            // Additional restrictions to prevent security settings modifications
            ["io.github.hatayama.uLoopMCP.DynamicCodeSecurityManager"] = new() 
            {
                "InitializeFromSettings"  // Changing security levels is dangerous (CurrentLevel is read-only, so permitted)
            },
            
            ["io.github.hatayama.uLoopMCP.McpEditorSettings"] = new() 
            {
                "SetDynamicCodeSecurityLevel"  // Security level manipulation is dangerous
            }
        };
        
        public bool IsDangerousType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return false;
            
            string fullTypeName = typeSymbol.ToDisplayString();
            return dangerousTypes.Contains(fullTypeName);
        }
        
        public bool IsDangerousApi(string fullApiName)
        {
            if (string.IsNullOrWhiteSpace(fullApiName)) return false;
            
            // First, check if it's a dangerous type itself (type name only)
            if (dangerousTypes.Contains(fullApiName))
            {
                return true;
            }
            
            // Analyze API full name
            string[] parts = fullApiName.Split('.');
            if (parts.Length < 2) return false;
            
            // Get member name and type name
            string memberName = parts[parts.Length - 1];
            string typeName = string.Join(".", parts.Take(parts.Length - 1));
            
            // Check if it's a dangerous member
            if (dangerousMembers.ContainsKey(typeName))
            {
                return dangerousMembers[typeName].Contains(memberName);
            }
            
            // Constructors of dangerous types and similar
            if (dangerousTypes.Contains(typeName))
            {
                return true;
            }
            
            return false;
        }
    }
}
#endif