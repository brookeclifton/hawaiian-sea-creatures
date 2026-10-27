using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Reef.EditorTools
{
    /// <summary>
    /// Development helper: copies Console errors/warnings and any "[Reef]" messages, plus the
    /// compile status of the Reef shaders, into Logs/ReefConsole.log in the project folder.
    /// Repeated messages are collapsed (at most one line per message every 30 s) so a script
    /// that errors every frame doesn't flood the file. Editor-only; safe to delete.
    /// </summary>
    [InitializeOnLoad]
    static class ReefConsoleLog
    {
        static readonly string LogPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ReefConsole.log");
        static readonly object Gate = new object();
        static readonly Dictionary<string, Seen> seen = new Dictionary<string, Seen>();
        const double RepeatSeconds = 30.0;

        class Seen
        {
            public DateTime lastWrite;
            public int suppressed;
        }

        static ReefConsoleLog()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.delayCall += WriteShaderStatus;
        }

        static void Write(string line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 2_000_000) info.Delete();
                File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss") + " " + line + Environment.NewLine);
            }
            catch { /* never let logging break the editor */ }
        }

        static void OnLog(string message, string stackTrace, LogType type)
        {
            bool reef = message != null && message.Contains("[Reef]");
            if (type == LogType.Log && !reef) return;
            if (message == null) message = string.Empty;

            lock (Gate)
            {
                string key = type + ":" + (message.Length > 300 ? message.Substring(0, 300) : message);
                DateTime now = DateTime.UtcNow;
                string repeatNote = string.Empty;
                bool first = true;
                if (seen.TryGetValue(key, out Seen s))
                {
                    first = false;
                    if ((now - s.lastWrite).TotalSeconds < RepeatSeconds)
                    {
                        s.suppressed++;
                        return;
                    }
                    if (s.suppressed > 0) repeatNote = " (repeated " + s.suppressed + " more times)";
                    s.lastWrite = now;
                    s.suppressed = 0;
                }
                else
                {
                    if (seen.Count > 500) seen.Clear();
                    seen[key] = new Seen { lastWrite = now };
                }

                bool withTrace = first && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && !string.IsNullOrEmpty(stackTrace);
                string trace = withTrace ? Environment.NewLine + "    " + stackTrace.Trim().Replace("\n", "\n    ") : string.Empty;
                Write("[" + type + "] " + message + repeatNote + trace);
            }
        }

        static void WriteShaderStatus()
        {
            string[] names =
            {
                "Reef/FishSwim", "Reef/HighlightRim", "Reef/SoftParticle", "Reef/LightShaft",
                "Reef/UnderwaterSky", "Reef/WaterSurface", "Reef/UnlitVertexColor"
            };
            lock (Gate)
            {
                foreach (var n in names)
                {
                    var sh = Shader.Find(n);
                    if (sh == null) { Write("[Shader] " + n + ": NOT FOUND"); continue; }
                    var msgs = ShaderUtil.GetShaderMessages(sh);
                    bool hasError = ShaderUtil.ShaderHasError(sh);
                    Write("[Shader] " + n + ": " + (hasError ? "ERROR" : "ok") + " (" + msgs.Length + " messages)");
                    foreach (var m in msgs)
                        Write("    " + m.severity + " " + m.message + " " + m.file + ":" + m.line + " (" + m.platform + ")");
                }
            }
        }
    }
}
