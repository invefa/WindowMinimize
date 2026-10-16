using System.Text;

namespace WindowMinimize;

/// <summary>
/// Minimal logger. Writes to the console only, and also to hotkey.log when --debug is set.
/// </summary>
internal static class Log {
    private static readonly object Gate = new();
    private static string _path = string.Empty;
    private static bool _enabled;
    private static bool _toFile;

    internal static void Init(string path, bool enabled) {
        _path = path;
        _enabled = enabled;
        _toFile = enabled && path.Length > 0;

        if (_toFile) {
            try {
                string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string header = $"==== {stamp} start ===={Environment.NewLine}";
                File.WriteAllText(path, header, Encoding.UTF8);
            } catch (Exception ex) {
                _toFile = false;
                Write("log file is not writable, console only: " + ex.Message);
            }
        }
    }

    internal static void Info(string message) => Write(message);

    internal static void Error(string message) => Write("error: " + message);

    internal static void Debug(string message) {
        if (_enabled) Write("debug: " + message);
    }

    private static void Write(string message) {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (Gate) {
            try {
                Console.WriteLine(line);
            } catch {
                // ignore, there is no console to write to
            }

            if (_toFile) {
                try {
                    File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
                } catch {
                    _toFile = false;
                }
            }
        }
    }
}
