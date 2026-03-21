#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
#if ULOOPMCP_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace io.github.hatayama.uLoopMCP
{
    [McpTool(Description = "Record keyboard and mouse input during PlayMode. Captures key presses, mouse movement, clicks, and scroll events frame-by-frame into a JSON file for later replay.")]
    public class RecordInputTool : AbstractUnityTool<RecordInputSchema, RecordInputResponse>
    {
        public override string ToolName => "record-input";

        protected override
#if !ULOOPMCP_HAS_INPUT_SYSTEM
#pragma warning disable CS1998
#endif
            async Task<RecordInputResponse> ExecuteAsync(
            RecordInputSchema parameters,
            CancellationToken ct)
#if !ULOOPMCP_HAS_INPUT_SYSTEM
#pragma warning restore CS1998
#endif
        {
            ct.ThrowIfCancellationRequested();

#if !ULOOPMCP_HAS_INPUT_SYSTEM
            return new RecordInputResponse
            {
                Success = false,
                Message = "record-input requires the Input System package (com.unity.inputsystem). Install it via Package Manager.",
                Action = parameters.Action.ToString()
            };
#else
            string correlationId = McpConstants.GenerateCorrelationId();

            VibeLogger.LogInfo(
                "record_input_start",
                "Record input started",
                new { Action = parameters.Action.ToString() },
                correlationId: correlationId
            );

            RecordInputResponse response;

            switch (parameters.Action)
            {
                case RecordInputAction.Start:
                    response = ExecuteStart(parameters);
                    break;

                case RecordInputAction.Stop:
                    response = ExecuteStop(parameters);
                    break;

                default:
                    throw new ArgumentException($"Unknown record-input action: {parameters.Action}");
            }

            VibeLogger.LogInfo(
                "record_input_complete",
                $"Record input completed: {response.Message}",
                new { Action = parameters.Action.ToString(), Success = response.Success },
                correlationId: correlationId
            );

            await Task.CompletedTask;
            return response;
#endif
        }

#if ULOOPMCP_HAS_INPUT_SYSTEM
        private static RecordInputResponse ExecuteStart(RecordInputSchema parameters)
        {
            if (!EditorApplication.isPlaying)
            {
                return new RecordInputResponse
                {
                    Success = false,
                    Message = "PlayMode is not active. Use control-play-mode tool to start PlayMode first.",
                    Action = RecordInputAction.Start.ToString()
                };
            }

            if (EditorApplication.isPaused)
            {
                return new RecordInputResponse
                {
                    Success = false,
                    Message = "PlayMode is paused. Resume PlayMode before recording input.",
                    Action = RecordInputAction.Start.ToString()
                };
            }

            if (InputRecorder.IsRecording)
            {
                return new RecordInputResponse
                {
                    Success = false,
                    Message = "Already recording. Stop the current recording first.",
                    Action = RecordInputAction.Start.ToString()
                };
            }

            if (InputReplayer.IsReplaying)
            {
                return new RecordInputResponse
                {
                    Success = false,
                    Message = "Cannot record while replaying. Stop the replay first.",
                    Action = RecordInputAction.Start.ToString()
                };
            }

            HashSet<Key>? keyFilter = ParseKeyFilter(parameters.Keys);
            InputRecorder.StartRecording(keyFilter);

            string filterMessage = keyFilter != null ? $" (filtering: {parameters.Keys})" : "";
            return new RecordInputResponse
            {
                Success = true,
                Message = $"Recording started{filterMessage}. Use Stop to save.",
                Action = RecordInputAction.Start.ToString()
            };
        }

        private static RecordInputResponse ExecuteStop(RecordInputSchema parameters)
        {
            if (!InputRecorder.IsRecording)
            {
                return new RecordInputResponse
                {
                    Success = false,
                    Message = "Not currently recording. Use Start first.",
                    Action = RecordInputAction.Stop.ToString()
                };
            }

            InputRecordingData data = InputRecorder.StopRecording();

            string outputPath = ResolveOutputPath(parameters.OutputPath);
            string directoryPath = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(directoryPath);

            JsonSerializerSettings jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented
            };
            string json = JsonConvert.SerializeObject(data, jsonSettings);
            File.WriteAllText(outputPath, json);

            int eventCount = data.GetTotalEventCount();

            return new RecordInputResponse
            {
                Success = true,
                Message = $"Recording saved: {eventCount} events across {data.Metadata.TotalFrames} frames ({data.Metadata.DurationSeconds:F1}s)",
                Action = RecordInputAction.Stop.ToString(),
                OutputPath = outputPath,
                TotalFrames = data.Metadata.TotalFrames,
                DurationSeconds = data.Metadata.DurationSeconds
            };
        }

        private static string ResolveOutputPath(string outputPath)
        {
            if (!string.IsNullOrEmpty(outputPath))
            {
                return outputPath;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            return $"{RecordInputConstants.DEFAULT_OUTPUT_DIR}/{RecordInputConstants.RECORDING_FILE_PREFIX}{timestamp}.json";
        }

        private static HashSet<Key>? ParseKeyFilter(string keys)
        {
            if (string.IsNullOrEmpty(keys))
            {
                return null;
            }

            HashSet<Key> filter = new HashSet<Key>();
            string[] parts = keys.Split(',');

            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                if (Enum.TryParse<Key>(trimmed, ignoreCase: true, out Key key) && key != Key.None)
                {
                    filter.Add(key);
                }
                else
                {
                    Debug.LogWarning($"[RecordInputTool] Unknown key name in filter: '{trimmed}'");
                }
            }

            return filter.Count > 0 ? filter : null;
        }
#endif
    }
}
