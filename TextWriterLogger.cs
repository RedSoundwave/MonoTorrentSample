namespace MonoTorrentSample
{
    public class TextWriterLogger : IRootLogger
    {
        TextWriter Writer { get; }

        public TextWriterLogger(TextWriter writer)
            => Writer = writer;

        public void Debug(string prefix, string message)
        {
            Writer?.WriteLine($"DEBUG:{prefix}:{message}");
        }

        public void Error(string prefix, string message)
        {
            Writer?.WriteLine($"ERROR:{prefix}:{message}");
        }

        public void Info(string prefix, string message)
        {
            Writer?.WriteLine($"INFO: {prefix}:{message}");
        }
    }
}
