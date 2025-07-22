namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Response schema for SetClientName tool
    /// Confirms client name registration and provides Push Notification Server endpoint
    /// </summary>
    public class SetClientNameResponse : BaseToolResponse
    {
        /// <summary>
        /// Success status message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Registered client name
        /// </summary>
        public string ClientName { get; set; }

        /// <summary>
        /// Push Notification Server endpoint for TypeScript client to connect to
        /// Format: "localhost:port"
        /// </summary>
        public string PushNotificationEndpoint { get; set; }

        /// <summary>
        /// Create a new SetClientNameResponse
        /// </summary>
        /// <param name="message">Success message</param>
        /// <param name="clientName">Registered client name</param>
        /// <param name="pushNotificationEndpoint">Push notification server endpoint</param>
        public SetClientNameResponse(string message, string clientName, string pushNotificationEndpoint = null)
        {
            Message = message;
            ClientName = clientName;
            PushNotificationEndpoint = pushNotificationEndpoint ?? string.Empty;
        }

        /// <summary>
        /// Parameterless constructor for JSON deserialization
        /// </summary>
        public SetClientNameResponse()
        {
            Message = string.Empty;
            ClientName = string.Empty;
            PushNotificationEndpoint = string.Empty;
        }
    }
}