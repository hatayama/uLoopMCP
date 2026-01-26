namespace io.github.hatayama.uLoopMCP
{
    public class FindGameObjectsResponse : BaseToolResponse
    {
        public FindGameObjectResult[] results { get; set; }
        public int totalFound { get; set; }
        public string errorMessage { get; set; }

        // For multiple selection file output
        public string resultsFilePath { get; set; }
        public string message { get; set; }
    }
    
    public class FindGameObjectResult
    {
        public string name { get; set; }
        public string path { get; set; }
        public bool isActive { get; set; }
        public string tag { get; set; }
        public int layer { get; set; }
        public ComponentInfo[] components { get; set; }
    }
}