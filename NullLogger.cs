namespace MonoTorrentSample
{
    class NullLogger : IRootLogger
    {
        public void Debug(string name, string message)
        {
        }

        public void Error(string name, string message)
        {
        }

        public void Info(string name, string message)
        {
        }
    }
}
