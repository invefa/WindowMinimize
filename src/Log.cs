using System.Text;

namespace WindowMinimize;

/// <summary>
/// 极简日志。默认只写控制台，--debug 时同时写 hotkey.log。
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static string _path = string.Empty;
    private static bool _enabled;
    private static bool _toFile;

    internal static void Init(string path, bool enabled)
    {
        _path = path;
        _enabled = enabled;
        _toFile = enabled && path.Length > 0;

        if (_toFile)
        {
            try
            {
                File.WriteAllText(path, $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} 启动 ===={Environment.NewLine}", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _toFile = false;
                Write("日志文件不可写，仅输出到控制台：" + ex.Message);
            }
        }
    }

    internal static void Info(string message) => Write(message);

    internal static void Error(string message) => Write("错误: " + message);

    internal static void Debug(string message)
    {
        if (_enabled)
        {
            Write("调试: " + message);
        }
    }

    private static void Write(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (Gate)
        {
            try
            {
                Console.WriteLine(line);
            }
            catch
            {
                // 没有控制台时忽略
            }

            if (_toFile)
            {
                try
                {
                    File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    _toFile = false;
                }
            }
        }
    }
}
