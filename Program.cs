using System.Net;

using MonoTorrent.Client;

namespace MonoTorrentSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();

            Task? task = MainAsync(args, cancellation.Token);

            Console.CancelKeyPress += delegate { cancellation.Cancel(); task.Wait(cancellation.Token); };
            AppDomain.CurrentDomain.ProcessExit += delegate { cancellation.Cancel(); task.Wait(cancellation.Token); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(cancellation.Token); };
            Thread.GetDomain().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(cancellation.Token); };

            task.Wait(cancellation.Token);
        }

        private static async Task MainAsync(string[] args, CancellationToken cancellationToken)
        {
            string? torrentFile = @"C:\Users\Soundwave\Downloads\Microsoft-Office-2021.torrent";

            FileInfo fileInfo = new FileInfo(torrentFile);
            string? saveDirectory =
                Path.Combine(fileInfo.Directory?.FullName, Path.GetFileNameWithoutExtension(torrentFile));

            const int httpListeningPort = 55125;

            EngineSettingsBuilder settingBuilder = new EngineSettingsBuilder
            {
                AllowPortForwarding = true,

                AutoSaveLoadDhtCache = true,

                AutoSaveLoadFastResume = true,

                AutoSaveLoadMagnetLinkMetadata = true,

                ListenEndPoints = new Dictionary<string, IPEndPoint> {
                    { "ipv4", new IPEndPoint (IPAddress.Any, 0) },
                    { "ipv6", new IPEndPoint (IPAddress.IPv6Any, 0) }
                },

                DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),

                HttpStreamingPrefix = $"http://127.0.0.1:{httpListeningPort}/"
            };
            using var engine = new ClientEngine(settingBuilder.ToSettings());

            Task task = new StandardDownloader(engine).DownloadAsync(torrentFile, saveDirectory, cancellationToken);

            if (engine.Settings.AllowPortForwarding)
                Console.WriteLine("uPnP or NAT-PMP port mappings will be created for any ports needed by MonoTorrent");

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {

            }

            foreach (var manager in engine.Torrents)
            {
                var stoppingTask = manager.StopAsync();
                while (manager.State != TorrentState.Stopped)
                {
                    Console.WriteLine("{0} is {1}", manager.Torrent.Name, manager.State);
                    await Task.WhenAll(stoppingTask, Task.Delay(250));
                }
                await stoppingTask;
                if (engine.Settings.AutoSaveLoadFastResume)
                    Console.WriteLine($"FastResume data for {manager.Torrent?.Name ?? manager.InfoHashes.V1?.ToHex() ?? manager.InfoHashes.V2?.ToHex()} has been written to disk.");
            }

            if (engine.Settings.AutoSaveLoadDhtCache)
                Console.WriteLine($"DHT cache has been written to disk.");

            if (engine.Settings.AllowPortForwarding)
                Console.WriteLine("uPnP and NAT-PMP port mappings have been removed");
        }
    }
}