//  ---------------------------------------------------------------------------------
//  Copyright © eDahab. All rights reserved.
//  ---------------------------------------------------------------------------------

using System;
using System.IO;

namespace easyCheckBalance
{
    public static class Logger
    {
        private static string _errorFile;

        public static void Log(string message)
        {
            var path = Path.Combine(GetLogDir(), "Logs");
            Directory.CreateDirectory(path);

            var logFile = new FileInfo(Path.Combine(path, $"log-{DateTime.Today:MM-dd-yyyy}.txt"));
            if (!logFile.Exists)
            {
                using var stream = logFile.Create();
                stream?.Dispose();
            }

            try
            {
                File.AppendAllLines(logFile.FullName, new[] { "===***===", message, "===***===" });
            }
            catch
            {
            }
        }

        public static void LogError(Exception ex, int retries = 0)
        {
            var path = Path.Combine(GetLogDir(), "Errors");
            Directory.CreateDirectory(path);

            if (retries >= 5)
            {
                return;
            }

            if (string.IsNullOrEmpty(_errorFile) || !_errorFile.StartsWith($"errors-{DateTime.Today:MM-dd-yyyy}"))
            {
                _errorFile = $"errors-{DateTime.Today:MM-dd-yyyy}";
            }

            var logFile = new FileInfo(Path.Combine(path, $"{_errorFile}.txt"));
            if (!logFile.Exists)
            {
                using (var stream = logFile.Create())
                {
                }
            }

            var lines = new[]
            {
                $"====== {DateTime.Now} ======",
                $"Message: {ex.Message} -- {ex.InnerException?.Message}",
                $"Stack Trace: {ex.StackTrace}",
                "======================="
            };

            try
            {
                File.AppendAllLines(logFile.FullName, lines);
            }
            catch
            {
                _errorFile += "-1";

                LogError(ex, retries++);
            }
        }

        private static string GetLogDir() => Directory.GetCurrentDirectory();
    }
}
