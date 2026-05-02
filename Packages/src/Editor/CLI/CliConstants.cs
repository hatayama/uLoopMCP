namespace io.github.hatayama.uLoopMCP
{
    public static class CliConstants
    {
        public const string EXECUTABLE_NAME = "uloop";
        public const string VERSION_FLAG = "--version";
        public const int GLOBAL_INSTALL_TIMEOUT_MS = 30000;
        public const string POSIX_INSTALL_SCRIPT_URL = "https://raw.githubusercontent.com/hatayama/unity-cli-loop/main/scripts/install.sh";
        public const string WINDOWS_INSTALL_SCRIPT_URL = "https://raw.githubusercontent.com/hatayama/unity-cli-loop/main/scripts/install.ps1";
        public const string SKILL_DIR_PREFIX = "uloop-";
        public const string SKILL_DIR_GLOB = "uloop-*";
        public const string GO_CLI_PACKAGE_DIR_NAME = "GoCli~";
        public const string DIST_DIR_NAME = "dist";
        public const string PROJECT_LOCAL_BIN_DIR_NAME = "bin";
        public const string PROJECT_LOCAL_UNIX_COMMAND_NAME = "uloop-core";
        public const string PROJECT_LOCAL_WINDOWS_COMMAND_NAME = "uloop-core.exe";
    }
}
