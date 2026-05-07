using UnityEditor;
using UnityEngine;

using io.github.hatayama.UnityCliLoop.Application;
using io.github.hatayama.UnityCliLoop.CompositionRoot;
using io.github.hatayama.UnityCliLoop.Domain;
using io.github.hatayama.UnityCliLoop.FirstPartyTools;
using io.github.hatayama.UnityCliLoop.Infrastructure;
using io.github.hatayama.UnityCliLoop.Presentation;
using io.github.hatayama.UnityCliLoop.ToolContracts;

namespace io.github.hatayama.UnityCliLoop.Tests.Editor
{
    public sealed class TestSessionManager : ScriptableSingleton<TestSessionManager>
    {
        [SerializeField] private bool testBoolValue;
        [SerializeField] private int testIntValue;
        [SerializeField] private string testStringValue = "";
        [SerializeField] private float testFloatValue;

        public bool TestBoolValue
        {
            get => testBoolValue;
            set => testBoolValue = value;
        }

        public int TestIntValue
        {
            get => testIntValue;
            set => testIntValue = value;
        }

        public string TestStringValue
        {
            get => testStringValue;
            set => testStringValue = value ?? "";
        }

        public float TestFloatValue
        {
            get => testFloatValue;
            set => testFloatValue = value;
        }

        public void ResetAllValues()
        {
            testBoolValue = false;
            testIntValue = 0;
            testStringValue = "";
            testFloatValue = 0.0f;
        }

        public string GetAllValuesAsString()
        {
            return $"Bool: {testBoolValue}, Int: {testIntValue}, String: '{testStringValue}', Float: {testFloatValue}";
        }
    }
}