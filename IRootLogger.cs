namespace MonoTorrentSample
{
    public interface IRootLogger
    {
        void Info(string name, string message);
        void Debug(string name, string message);
        void Error(string name, string message);
    }
}
