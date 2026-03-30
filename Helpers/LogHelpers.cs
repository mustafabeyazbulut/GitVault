using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace GitVault.Helpers
{
    public enum LogCategory
    {
        Service,
        GitHub,
        Sync,
        Git
    }

    public static class LogHelpers
    {
        private static readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private static readonly Timer _flushTimer;
        private static readonly object _lock = new object();
        private static readonly string _logBasePath;

        static LogHelpers()
        {
            _logBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
            Directory.CreateDirectory(Path.Combine(_logBasePath, "AppLog"));

            _flushTimer = new Timer(_ => FlushQueue(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        public static void Debug(string message, LogCategory category, string source)
        {
            Enqueue("DEBUG", message, category, source);
        }

        public static void Info(string message, LogCategory category, string source)
        {
            Enqueue("INFO ", message, category, source);
        }

        public static void Warn(string message, LogCategory category, string source)
        {
            Enqueue("WARN ", message, category, source);
        }

        public static void Error(string message, LogCategory category, string source)
        {
            Enqueue("ERROR", message, category, source);
        }

        public static void Error(string message, Exception ex, LogCategory category, string source)
        {
            Enqueue("ERROR", $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", category, source);
        }

        public static IDisposable MeasureTime(string operationName, LogCategory category, string source)
        {
            return new TimeMeasurer(operationName, category, source);
        }

        private static void Enqueue(string level, string message, LogCategory category, string source)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {level} | T{Thread.CurrentThread.ManagedThreadId:D2} | {category,-8} | [{source}] {message}";
            _logQueue.Enqueue($"{category}|{line}");

#if DEBUG
            Console.WriteLine(line);
#endif
        }

        private static void FlushQueue()
        {
            lock (_lock)
            {
                while (_logQueue.TryDequeue(out var entry))
                {
                    var separatorIndex = entry.IndexOf('|');
                    var categoryStr = entry.Substring(0, separatorIndex);
                    var line = entry.Substring(separatorIndex + 1);
                    var dateStr = DateTime.Now.ToString("yyyy-MM-dd");

                    try
                    {
                        // All log
                        var allFile = Path.Combine(_logBasePath, "AppLog", $"{dateStr}_All.txt");
                        File.AppendAllText(allFile, line + Environment.NewLine);

                        // Category log
                        var categoryFile = Path.Combine(_logBasePath, "AppLog", $"{dateStr}_{categoryStr}.txt");
                        File.AppendAllText(categoryFile, line + Environment.NewLine);

                        // Error log
                        if (line.Contains("| ERROR") || line.Contains("| WARN "))
                        {
                            var errorFile = Path.Combine(_logBasePath, "AppLog", $"{dateStr}_Errors.txt");
                            File.AppendAllText(errorFile, line + Environment.NewLine);
                        }
                    }
                    catch
                    {
                        // Logging hatasi servis durdurmamali
                    }
                }
            }
        }

        public static void Flush()
        {
            FlushQueue();
        }

        private class TimeMeasurer : IDisposable
        {
            private readonly string _operation;
            private readonly LogCategory _category;
            private readonly string _source;
            private readonly DateTime _start;

            public TimeMeasurer(string operation, LogCategory category, string source)
            {
                _operation = operation;
                _category = category;
                _source = source;
                _start = DateTime.Now;
                Info($"{operation} basladi", _category, _source);
            }

            public void Dispose()
            {
                var elapsed = DateTime.Now - _start;
                Info($"{_operation} tamamlandi ({elapsed.TotalMilliseconds:F0}ms)", _category, _source);
            }
        }
    }
}
