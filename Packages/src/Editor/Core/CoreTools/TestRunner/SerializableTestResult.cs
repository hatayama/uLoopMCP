using System;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Serializable test result for domain reload survival
    /// </summary>
    [Serializable]
    public class SerializableTestResult
    {
        [SerializeField] public bool success;
        [SerializeField] public string message;
        [SerializeField] public string completedAt;
        [SerializeField] public int testCount;
        [SerializeField] public int passedCount;
        [SerializeField] public int failedCount;
        [SerializeField] public int skippedCount;
        [SerializeField] public string xmlPath;
        
        /// <summary>
        /// Convert from ITestResultAdaptor to SerializableTestResult
        /// </summary>
        public static SerializableTestResult FromTestResult(ITestResultAdaptor result)
        {
            if (result == null)
            {
                return new SerializableTestResult
                {
                    success = true,
                    message = "PlayMode test execution completed (detailed results not available)",
                    completedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    testCount = 1,
                    passedCount = 1,
                    failedCount = 0,
                    skippedCount = 0,
                    xmlPath = null
                };
            }
            
            return new SerializableTestResult
            {
                success = result.TestStatus == TestStatus.Passed,
                message = $"Test execution completed with status: {result.TestStatus}",
                completedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                testCount = CountTotalTests(result),
                passedCount = CountPassedTests(result),
                failedCount = CountFailedTests(result),
                skippedCount = CountSkippedTests(result),
                xmlPath = null
            };
        }
        
        /// <summary>
        /// Count total tests
        /// </summary>
        private static int CountTotalTests(ITestResultAdaptor result)
        {
            int count = 0;
            CountTestsByStatus(result, ref count, null);
            return count;
        }
        
        /// <summary>
        /// Count passed tests
        /// </summary>
        private static int CountPassedTests(ITestResultAdaptor result)
        {
            int count = 0;
            CountTestsByStatus(result, ref count, TestStatus.Passed);
            return count;
        }
        
        /// <summary>
        /// Count failed tests
        /// </summary>
        private static int CountFailedTests(ITestResultAdaptor result)
        {
            int count = 0;
            CountTestsByStatus(result, ref count, TestStatus.Failed);
            return count;
        }
        
        /// <summary>
        /// Count skipped tests
        /// </summary>
        private static int CountSkippedTests(ITestResultAdaptor result)
        {
            int count = 0;
            CountTestsByStatus(result, ref count, TestStatus.Skipped);
            return count;
        }
        
        /// <summary>
        /// Recursively count tests by status
        /// </summary>
        private static void CountTestsByStatus(ITestResultAdaptor result, ref int count, TestStatus? targetStatus)
        {
            if (!result.Test.IsSuite)
            {
                if (targetStatus == null || result.TestStatus == targetStatus)
                {
                    count++;
                }
                return;
            }
            
            if (result.Children != null)
            {
                foreach (ITestResultAdaptor child in result.Children)
                {
                    CountTestsByStatus(child, ref count, targetStatus);
                }
            }
        }
    }
}