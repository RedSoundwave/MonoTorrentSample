namespace MonoTorrentSample
{
    public class LoggerFactory
    {
        public static IRootLogger RootLogger { get; private set; } = new NullLogger();

        public static ILogger? Create(string name)
            => new Logger(name);

        public static void Register(IRootLogger rootLogger)
            => RootLogger = rootLogger ?? new NullLogger();
    }
}
