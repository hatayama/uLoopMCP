namespace io.github.hatayama.uLoopMCP
{
    public static class DeviceAgentConstants
    {
        public const int DEFAULT_PORT = 8800;
        public const int MAX_QUEUE_SIZE = 32;
        public const int REQUEST_TIMEOUT_SECONDS = 30;
        public const int MAX_REQUEST_BYTES = 1024 * 1024; // 1MB
        public const string PROTOCOL_VERSION = "1.0";
        public const string AGENT_VERSION = "0.1.0";
        public const string MIN_CLI_VERSION = "0.68.0";

        public static class ErrorCodes
        {
            public const int PARSE_ERROR = -32700;
            public const int INVALID_REQUEST = -32600;
            public const int METHOD_NOT_FOUND = -32601;
            public const int INVALID_PARAMS = -32602;
            public const int INTERNAL_ERROR = -32603;
            public const int UNAUTHORIZED = -32001;
            public const int BUSY = -32002;
            public const int TIMEOUT = -32003;
            public const int PAYLOAD_TOO_LARGE = -32004;
            public const int INCOMPATIBLE_VERSION = -32005;
        }
    }
}
