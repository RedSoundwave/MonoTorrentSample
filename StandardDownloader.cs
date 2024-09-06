using System.Text;

using MonoTorrent;
using MonoTorrent.Client;

namespace MonoTorrentSample
{
    public class StandardDownloader
    {
        private readonly ClientEngine _engine;
        private readonly TopListener _listener;

        public StandardDownloader(ClientEngine engine)
        {
            _engine = engine;
            _listener = new TopListener(10);
        }

        public async Task DownloadAsync(string source, string directory, CancellationToken cancellationToken)
        {

#if DEBUG
            LoggerFactory.Register(new TextWriterLogger(Console.Out));
#endif

            // If the torrentsPath does not exist, we want to create it
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            TorrentManager? torrentManager = null;

            if (source.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // EngineSettings.AutoSaveLoadFastResume is enabled, so any cached fast resume
                    // data will be implicitly loaded. If fast resume data is found, the 'hash check'
                    // phase of starting a torrent can be skipped.
                    // 
                    // TorrentSettingsBuilder can be used to modify the settings for this
                    // torrent.
                    var settingsBuilder = new TorrentSettingsBuilder
                    {
                        MaximumConnections = 60,
                    };
                    torrentManager = await _engine.AddAsync(source, directory, settingsBuilder.ToSettings());
                    Console.WriteLine(torrentManager.InfoHashes.V1OrV2.ToHex());
                }
                catch (Exception e)
                {
                    Console.Write("Couldn't decode {0}: ", source);
                    Console.WriteLine(e.Message);
                }
            }

            if (torrentManager != null)
            {
                torrentManager.PeersFound += (o, e) =>
                {
                    lock (_listener)
                    {
                        _listener.WriteLine(
                            string.Format($"{e.GetType().Name}: {e.NewPeers} peers for {e.TorrentManager.Name}"));
                    }
                };
                torrentManager.PeerConnected += (o, e) =>
                {
                    lock (_listener)
                        _listener.WriteLine($"Connection succeeded: {e.Peer.Uri}");
                };
                torrentManager.ConnectionAttemptFailed += (o, e) =>
                {
                    lock (_listener)
                        _listener.WriteLine(
                            $"Connection failed: {e.Peer.ConnectionUri} - {e.Reason}");
                };
                // Every time a piece is hashed, this is fired.
                torrentManager.PieceHashed += delegate (object o, PieceHashedEventArgs e)
                {
                    lock (_listener)
                        _listener.WriteLine($"Piece Hashed: {e.PieceIndex} - {(e.HashPassed ? "Pass" : "Fail")}");
                };

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                torrentManager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e)
                {
                    lock (_listener)
                        _listener.WriteLine($"OldState: {e.OldState} NewState: {e.NewState}");
                };

                // Every time the tracker's state changes, this is fired
                torrentManager.TrackerManager.AnnounceComplete += (sender, e) =>
                {
                    _listener.WriteLine($"{e.Successful}: {e.Tracker}");
                };

                await torrentManager.StartAsync();
            }

            StringBuilder sb = new StringBuilder(1024);

            while (_engine.IsRunning)
            {
                sb.Clear();

                // Update and display system/engine information once per iteration
                AppendFormat(sb,
                    $"Transfer Rate:      {_engine.TotalDownloadRate / 1024.0:0.00}kB/sec ↓ / {_engine.TotalUploadRate / 1024.0:0.00}kB/sec ↑");
                AppendFormat(sb,
                    $"Memory Cache:       {_engine.DiskManager.CacheBytesUsed / 1024.0:0.00}/{_engine.Settings.DiskCacheBytes / 1024.0:0.00} kB");
                AppendFormat(sb,
                    $"Disk IO Rate:       {_engine.DiskManager.ReadRate / 1024.0:0.00} kB/s read / {_engine.DiskManager.WriteRate / 1024.0:0.00} kB/s write");
                AppendFormat(sb,
                    $"Disk IO Total:      {_engine.DiskManager.TotalBytesRead / 1024.0:0.00} kB read / {_engine.DiskManager.TotalBytesWritten / 1024.0:0.00} kB written");
                AppendFormat(sb,
                    $"Open Files:         {_engine.DiskManager.OpenFiles} / {_engine.DiskManager.MaximumOpenFiles}");
                AppendFormat(sb, $"Open Connections:   {_engine.ConnectionManager.OpenConnections}");
                AppendFormat(sb, $"DHT State:          {_engine.Dht.State}");

                // Display port mappings
                foreach (var mapping in _engine.PortMappings.Created)
                    AppendFormat(sb,
                        $"Successful Mapping    {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                foreach (var mapping in _engine.PortMappings.Failed)
                    AppendFormat(sb,
                        $"Failed mapping:       {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
                foreach (var mapping in _engine.PortMappings.Pending)
                    AppendFormat(sb,
                        $"Pending mapping:      {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");

                // Loop through all torrent managers and print their information
                foreach (TorrentManager manager in _engine.Torrents)
                {
                    AppendSeparator(sb);
                    AppendFormat(sb, $"State:              {manager.State}");
                    AppendFormat(sb,
                        $"Name:               {(manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name)}");
                    AppendFormat(sb, $"Progress:           {manager.Progress:0.00}");
                    AppendFormat(sb,
                        $"Transferred:        {manager.Monitor.DataBytesReceived / 1024.0 / 1024.0:0.00} MB ↓ / {manager.Monitor.DataBytesSent / 1024.0 / 1024.0:0.00} MB ↑");

                    AppendFormat(sb, $"Tracker Status");
                    foreach (var tier in manager.TrackerManager.Tiers)
                        AppendFormat(sb,
                            $"\t{tier.ActiveTracker} : Announce Succeeded: {tier.LastAnnounceSucceeded}. Scrape Succeeded: {tier.LastScrapeSucceeded}.");

                    AppendFormat(sb, "Current Requests:   {0}", await manager.PieceManager.CurrentRequestCountAsync());

                    var peers = await manager.GetPeersAsync();
                    AppendFormat(sb, "Outgoing:");
                    foreach (PeerId p in peers.Where(t => t.ConnectionDirection == Direction.Outgoing))
                    {
                        AppendFormat(sb, "\t{2} - {1:0.00}/{3:0.00}kB/sec - {0} - {4} ({5})", p.Uri,
                            p.Monitor.DownloadRate / 1024.0,
                            p.AmRequestingPiecesCount,
                            p.Monitor.UploadRate / 1024.0,
                            p.EncryptionType);
                    }

                    AppendFormat(sb, "");
                    AppendFormat(sb, "Incoming:");
                    foreach (PeerId p in peers.Where(t => t.ConnectionDirection == Direction.Incoming))
                    {
                        AppendFormat(sb, "\t{2} - {1:0.00}/{3:0.00}kB/sec - {0} - {4} ({5})", p.Uri,
                            p.Monitor.DownloadRate / 1024.0,
                            p.AmRequestingPiecesCount,
                            p.Monitor.UploadRate / 1024.0,
                            p.EncryptionType);
                    }

                    if (manager.Torrent != null)
                        foreach (var file in manager.Files)
                            AppendFormat(sb, "{1:0.00}% - {0}", file.Path, file.BitField.PercentComplete);
                }

                // Clear and update the console
                Console.Clear();
                Console.WriteLine(sb.ToString());

                // Export to listener if needed
                lock (_listener)
                {
                    _listener.ExportTo(Console.Out);
                }

                // Delay to avoid constant refresh
                await Task.Delay(1000, cancellationToken);
            }

            void Manager_PeersFound(object sender, PeersAddedEventArgs e)
            {
                lock (_listener)
                    _listener.WriteLine($"Found {e.NewPeers} new peers and {e.ExistingPeers} existing peers");
            }

            void AppendSeparator(StringBuilder sb)
            {
                AppendFormat(sb, "");
                AppendFormat(sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");
                AppendFormat(sb, "");
            }

            void AppendFormat(StringBuilder sb, string str, params object[]? formatting)
            {
                if (formatting is { Length: > 0 })
                    sb.AppendFormat(str, formatting);
                else
                    sb.Append(str);

                sb.AppendLine();
            }
        }
    }
}
