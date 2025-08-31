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
            "System.IO.FileSystemWatcher",
            
            // Network-related
            "System.Net.Http.HttpClient",
            "System.Net.WebClient",
            "System.Net.WebRequest",
            "System.Net.Sockets.Socket",
            "System.Net.Sockets.TcpClient",
            // Additional network-related (full ban, including localhost)
            "System.Net.Sockets.TcpListener",
            "System.Net.Sockets.UdpClient",
            "System.Net.Sockets.NetworkStream",
            "System.Net.Dns",
            "System.Net.IPAddress",
            "System.Net.HttpWebRequest",
            "System.Net.Security.SslStream",
            "System.Net.WebSockets.ClientWebSocket",
            "System.Net.WebSockets.WebSocket",
            "System.Net.Mail.SmtpClient",
            "System.Net.NetworkInformation.Ping",
            "System.Net.Http.HttpClientHandler",
            "System.Net.Http.HttpMessageHandler",
            "System.Net.Http.SocketsHttpHandler",
            
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
                "Delete", "WriteAllText", "WriteAllBytes", "Replace",
                // Stage A additions
                "OpenWrite", "AppendAllText", "AppendText",
                // Explicit Set* family (wildcards not supported)
                "SetAttributes", "SetCreationTime", "SetCreationTimeUtc",
                "SetLastAccessTime", "SetLastAccessTimeUtc",
                "SetLastWriteTime", "SetLastWriteTimeUtc"
                // Create, Copy, Move, ReadAllText, ReadAllBytes, Exists, Open-related methods are relatively safe
            },
            
            // System.IO.Directory - Deletion is dangerous
            ["System.IO.Directory"] = new() 
            { 
                "Delete",
                // Stage A addition
                "SetCurrentDirectory"
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
            // System.Reflection.MethodInfo / ConstructorInfo - Invocation is dangerous
            ["System.Reflection.MethodInfo"] = new()
            {
                "Invoke"
            },
            ["System.Reflection.ConstructorInfo"] = new()
            {
                "Invoke"
            },
            
            // System.Activator - Creating COM objects is dangerous
            ["System.Activator"] = new() 
            { 
                "CreateComInstanceFrom",
                // Stage A: Block CreateInstance, including generic
                "CreateInstance"
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
                "Exit", "FailFast",
                // Stage A: Block environment modification
                "SetEnvironmentVariable"
                // ExpandEnvironmentVariables remains allowed (read-only)
            },
            
            // System.Threading.Thread - Forced thread manipulation is dangerous
            ["System.Threading.Thread"] = new() 
            {
                "Abort", "Suspend", "Resume"
                // Start, Join are relatively safe
            },

            // Note: GCSettings.LatencyMode will be blocked specifically on assignment
            // via SecuritySyntaxWalker (do not blanket-block property access here)
            
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
            
            string fullTypeName = NormalizeFullName(typeSymbol.ToDisplayString());
            return dangerousTypes.Contains(fullTypeName);
        }
        
        public bool IsDangerousApi(string fullApiName)
        {
            if (string.IsNullOrWhiteSpace(fullApiName)) return false;
            fullApiName = NormalizeFullName(fullApiName);
            
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

        private static string NormalizeFullName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            const string globalPrefix = "global::";
            if (name.StartsWith(globalPrefix))
            {
                return name.Substring(globalPrefix.Length);
            }
            return name;
        }
    }
}
#endif