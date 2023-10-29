using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System.Threading;
using System.Threading.Tasks;

using static BTProtocol.BitTorrent.Utils;

namespace BTProtocol.BitTorrent
{
    class FriendTorrent
    {
        public static string peerid { get; set; }
        public const string resource_path = @"../../../Resources/";
        public const string serialized_path = resource_path + "TorrentData/";
        public static Dictionary<string, TFData> torrent_dict = new Dictionary<string, TFData>();
        public static Dictionary<string, TrackerManager> tracker_dict = new Dictionary<string, TrackerManager>();
        public static Dictionary<string, FileManager> file_dict = new Dictionary<string, FileManager>();

        private static void InitPeerid()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ds_bit");
            sb.Append(Environment.MachineName.ToString());
            if (sb.Length < 20)
            {
                int rest = 20 - sb.Length;
                Random random = new Random();
                for (int i = 0; i < rest; i++)
                {
                    sb.Append(random.Next() % 10);
                }
            }
            peerid = sb.ToString().Substring(0, 20);
        }

        private static Dictionary<string, Torrent> ParseTorrentFiles(string resource_path)
        {
            /*
             Iterates through the torrent files in / resources, creating new serialized
             files for each torrent if they don't exist, and cleaning up old serialized
             files whose torrents have been removed
            */

            string[] torrent_files = Directory.GetFiles(resource_path);
            List<string> torrent_data_files = new List<string>(Directory.GetFiles(serialized_path));
            Dictionary<string, Torrent> torrents = new Dictionary<string, Torrent>();
            foreach (string file in torrent_files)
            {
                Torrent torrent_file = parser.Parse<Torrent>(file);
                string torrent_name = torrent_file.DisplayName;
                string torrent_filedata_path = serialized_path + torrent_name;
                TFData file_data;
                if (File.Exists(torrent_filedata_path))
                {
                    file_data = DeserializeTFData(torrent_filedata_path);
                    torrent_data_files.Remove(torrent_filedata_path);
                }
                else
                {
                    file_data = new TFData(torrent_file, torrent_name);
                    SerializeTFData(file_data);
                }
                file_dict.Add(torrent_name, new FileManager(torrent_file));
                torrent_dict.Add(torrent_name, file_data);
                torrents.Add(torrent_name, torrent_file);
            }

            // Delete serialized files for removed torrents
            foreach (string file in torrent_data_files)
            {
                string torrent_name = file.Split('/').Last();
                logger.Info($"Removing serialized torrent data: {torrent_name}");
                File.Delete(file);
            }

            return torrents;
        }

        static void Main(string[] args)
        {
            InitPeerid(); 
            Dictionary<string, Torrent> torrents = ParseTorrentFiles(resource_path);

            // For each torrent file found, create and contact its tracker, and set up
            // Timers to reconnect with the tracker after a specified amount of time has passed
            foreach (KeyValuePair<string, Torrent> torrent in torrents)
            {
                TrackerManager tracker = new TrackerManager(torrent.Value, torrent_dict[torrent.Key]);
                tracker_dict.Add(torrent.Key, tracker);
            }

            foreach (KeyValuePair<string, TFData> torrent in torrent_dict)
            {
                if (torrent.Value.CheckDownloadStatus())
                {
                    file_dict[torrent.Key].Initialize(FileAccess.Read);
                }
                else if (torrent.Value.IsActive())
                {
                    file_dict[torrent.Key].Initialize(FileAccess.ReadWrite);
                }
            }

            // Forced sleep to allow trackers to respond in time before creating downloading tasks
            Thread.Sleep(2000);

            // Create thread-pools for downloading and uploading (25 down, 5 up)
            SeedingThreadManager seeding_task = new SeedingThreadManager(5);
            Task seeding_manager = new Task(() => seeding_task.StartSeeding());
            //seeding_manager.Start();

            DownloadingThreadManager downloading_task = new DownloadingThreadManager(25);
            Task download_manager = new Task(() => downloading_task.StartDownloads());
            download_manager.Start();

            Thread.Sleep(3600000);
        }
    }
}
