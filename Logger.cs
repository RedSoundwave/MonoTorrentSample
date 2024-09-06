namespace MonoTorrentSample
{
    class Logger : ILogger
    {
        public string Name { get; }

        public Logger(string name)
            => Name = name;

        public void Debug(string message)
        {
            LoggerFactory.RootLogger.Debug(Name, message);
        }

        public void Error(string message)
        {
            LoggerFactory.RootLogger.Debug(Name, message);
        }

        public void Info(string message)
        {
            LoggerFactory.RootLogger.Debug(Name, message);
        }
    }
}
