/*
 * NetworkUtility.cs
 * 
 * Design Document: ARCHITECTURE.md - Network Communication
 * Related Classes: McpBridgeServer, McpServerController, ConnectedClient
 * 
 * Provides network-related utility functions
 * Handles port availability checking, process ID resolution, and network connection management
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using io.github.hatayama.uLoopMCP;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Provides network-related utility functions
    /// Handles port availability checking, process ID resolution, and network connection management
    /// </summary>
    public static class NetworkUtility
    {
        /// <summary>
        /// Common system ports to avoid conflicts
        /// </summary>
        private static readonly int[] CommonSystemPorts = { 80, 443, 21, 22, 23, 25, 53, 110, 143, 993, 995, 3389 };

        #region Port Availability Methods

        /// <summary>
        /// Checks if the specified port is currently in use
        /// Uses TCP connection test to determine port availability
        /// </summary>
        /// <param name="port">Port number to check</param>
        /// <returns>True if the port is in use, false if available</returns>
        public static bool IsPortInUse(int port)
        {
            TcpListener tcpListener = null;
            
            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();
                return false; // The port is available.
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                // Port is already in use - this is expected behavior
                return true;
            }
            catch (SocketException ex)
            {
                // Other socket errors should be logged with specific information
                return true; // Treat as "in use" to be safe
            }
            finally
            {
                tcpListener?.Stop();
            }
        }

        /// <summary>
        /// Finds an available port starting from the specified port number
        /// Avoids system ports and selects a safe port for use
        /// </summary>
        /// <param name="startPort">Starting port number for search</param>
        /// <param name="maxAttempts">Maximum number of attempts (default: 10)</param>
        /// <returns>Available port number</returns>
        /// <exception cref="InvalidOperationException">Thrown when no available port is found</exception>
        public static int FindAvailablePort(int startPort, int maxAttempts = 10)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                int candidatePort = startPort + i;
                
                // Skip if port is out of valid range
                if (candidatePort > 65535)
                {
                    break;
                }
                
                // Skip commonly used system ports
                if (Array.IndexOf(CommonSystemPorts, candidatePort) != -1)
                {
                    continue;
                }
                
                // Check if port is available
                if (!IsPortInUse(candidatePort))
                {
                    return candidatePort;
                }
            }
            
            // If no available port found, throw exception
            throw new InvalidOperationException(
                $"Could not find an available port starting from {startPort}. Tried {maxAttempts} ports.");
        }

        /// <summary>
        /// Validates if the port number is within valid range
        /// </summary>
        /// <param name="port">Port number to validate</param>
        /// <param name="parameterName">Parameter name for error messages</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidPort(int port, string parameterName = "port")
        {
            if (port < 1 || port > 65535)
            {
                return false;
            }
            return true;
        }

        #endregion

    }
} 