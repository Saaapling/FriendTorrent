using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    class MainProc
    {
        public static string peerid { get; set; }
        //public Queue<TFData> download_queue { get; private set; }
        const string resource_path = @"../../Resources/";
        const string serailized_path = resource_path + "TorrentData/";
        static Dictionary<string, TFData> torrent_file_dict = new Dictionary<string, TFData>();

        public static Semaphore thread_pool;

        private static void InitPeerid()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("ds_bit");
            sb.Append(Environment.MachineName.ToString());
            if (sb.Length >= 20)
            {
                peerid = sb.ToString().Substring(0, 20);
            }
            else
            {
                int rest = 20 - sb.Length;
                Random random = new Random();
                for (int i = 0; i < rest; i++)
                {
                    sb.Append(random.Next() % 10);
                }
                peerid = sb.ToString();
            }
        }

        private static Dictionary<string, Torrent> ParseTorrentFiles(string resource_path)
        {
            /*
             Iterates through the torrent files in / resources, creating new serialized
             files for each torrent if they don't exist, and cleaning up old serialized
             files whose torrents have been removed
            */

            string[] torrent_files = Directory.GetFiles(resource_path);
            List<string> torrent_data_files = new List<string>(Directory.GetFiles(serailized_path));
            Dictionary<string, Torrent> torrents = new Dictionary<string, Torrent>();
            foreach (string file in torrent_files)
            {
                string torrent_name = file.Split('/').Last();
                torrent_name = torrent_name.Substring(0, torrent_name.Length - 8);
                string torrent_filedata_path = serailized_path + torrent_name;
                Torrent torrent_file = Utils.parser.Parse<Torrent>(file);
                TFData file_data;
                if (File.Exists(torrent_filedata_path))
                {
                    Stream openFileStream = File.OpenRead(torrent_filedata_path);
                    BinaryFormatter deserializer = new BinaryFormatter();
                    file_data = (TFData)deserializer.Deserialize(openFileStream);
                    file_data.ResetStatus();

                    Console.WriteLine("Torrent found: " + torrent_name);
                    torrent_data_files.Remove(torrent_filedata_path);
                }
                else
                {
                    file_data = new TFData(torrent_name, file, torrent_file.GetInfoHash(), torrent_file.Pieces, torrent_file.PieceSize);

                    Console.WriteLine("Creating new TFData serialized object: " + torrent_name);
                    Stream SaveFileStream = File.Create(serailized_path + torrent_name);
                    BinaryFormatter serializer = new BinaryFormatter();
                    serializer.Serialize(SaveFileStream, file_data);
                    SaveFileStream.Close();
                }
                torrent_file_dict.Add(torrent_name, file_data);
                torrents.Add(torrent_name, torrent_file);
            }

            // Delete serialized files for removed torrents
            foreach (string file in torrent_data_files)
            {
                string torrent_name = file.Split('/').Last();
                Console.WriteLine("Removing serialized torrent data: " + torrent_name);
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
                Tracker tracker = new Tracker(torrent.Value);
                BDictionary dictionary = torrent.Value.ToBDictionary();
                TFData torrent_data = torrent_file_dict[torrent.Key];
                int wait_time = tracker.SendRecvToTracker(torrent_data, dictionary["announce"].ToString());

                var state_timer = new Timer(tracker.UpdateTracker, torrent_data, wait_time * 1000, wait_time * 1000);
            }

            // Create thread-pool for downloading and uploading (29 down, 1 up)
            thread_pool = new Semaphore(30, 30);

            // For each torrent, spin up threads to download peices (blocks when thread_pool is exhausted)
            // Only downloads from one torrent at a time, once a torrent is finished downloading, start on the next torrent
            // Todo: Implement logic to switch from one torrent to the next (and to mark finished torrents as complete)
            Queue<TFData> download_queue = new Queue<TFData>(torrent_file_dict.Values);
            while (download_queue.Count > 0)
            {
                Console.WriteLine(download_queue.Peek().visited_peers.Count());
                thread_pool.WaitOne();
                DownloadingTask task = new DownloadingTask(download_queue.Peek());
                thread_pool.Release();
                Task t = new Task(() => task.StartTask());
                t.Start();
                Thread.Sleep(1000);
            }
        }

    }
}
