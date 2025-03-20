namespace DatabaseConfigDemo.Services
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message, Exception ex);
    }

    public class Logger : ILogger
    {
        private readonly string _logPath;
        private TextBox? _logTextBox;

        public Logger()
        {
            _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
        }

        public void SetLogTextBox(TextBox logTextBox)
        {
            _logTextBox = logTextBox;
        }

        public void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO: {message}";
            WriteToFile(logMessage);
            AppendToTextBox(logMessage);
        }

        public void LogError(string message, Exception ex)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR: {message}\n{ex}";
            WriteToFile(logMessage);
            AppendToTextBox(logMessage);
        }

        private void WriteToFile(string message)
        {
            try
            {
                File.AppendAllText(_logPath, message + Environment.NewLine);
            }
            catch { }
        }

        private void AppendToTextBox(string message)
        {
            if (_logTextBox?.IsDisposed == false)
            {
                if (_logTextBox.InvokeRequired)
                {
                    _logTextBox.Invoke(new Action(() => AppendToTextBox(message)));
                }
                else
                {
                    _logTextBox.AppendText(message + Environment.NewLine);
                    _logTextBox.SelectionStart = _logTextBox.TextLength;
                    _logTextBox.ScrollToCaret();
                }
            }
        }
    }
} 