using System;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ToolKitV.Models
{
    public class LogWriter : IAsyncDisposable
    {
        private readonly string m_logPath;
        private readonly Channel<string> m_channel;
        private readonly Task m_writerTask;

        public LogWriter(string logMessage = "=== Log Initialized ===")
        {
            m_logPath = Path.Combine(Directory.GetCurrentDirectory(), "log.txt");
            
            // Create an unbounded channel so your parallel loops never have to wait
            m_channel = Channel.CreateUnbounded<string>();
            
            // Start the background writer task
            m_writerTask = ProcessLogQueueAsync();
            
            LogWrite(logMessage);
        }

        public void LogWrite(string logMessage)
        {
            // Format the message and push it to the memory queue instantly
            string formattedMsg = $"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()} | {logMessage}";
            m_channel.Writer.TryWrite(formattedMsg);
        }

        private async Task ProcessLogQueueAsync()
        {
            // Keep the file stream open and let the OS handle the buffering
            try
            {
                using StreamWriter writer = new StreamWriter(m_logPath, append: true) { AutoFlush = true };
                
                await foreach (string msg in m_channel.Reader.ReadAllAsync())
                {
                    await writer.WriteLineAsync(msg);
                }
            }
            catch (Exception)
            {
                // Failsafe: If the log file is totally locked by an external program, 
                // the channel will just drain without crashing the main application.
            }
        }

        // Call this when the app is closing to ensure the final logs are written
        public async ValueTask DisposeAsync()
        {
            m_channel.Writer.Complete();
            await m_writerTask;
        }
    }
}
