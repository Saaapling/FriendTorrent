using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System.Threading;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    class MainProc
    {
        public static string peerid { get; set; }
        //public Queue<TFData> download_queue { get; private set; }
        public const string resource_path = @"../../Resources/";
        public const string serialized_path = resource_path + "TorrentData/";
        static Dictionary<string, TFData> torrent_file_dict = new Dictionary<string, TFData>();
        static Dictionary<string, FileManager> file_managers_dict = new Dictionary<string, FileManager>();
        static Dictionary<string, Semaphore> semaphore_dict = new Dictionary<string, Semaphore>();

        public static Semaphore main_semaphore;

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
            List<string> torrent_data_files = new List<string>(Directory.GetFiles(serialized_path));
            Dictionary<string, Torrent> torrents = new Dictionary<string, Torrent>();
            foreach (string file in torrent_files)
            {
                Torrent torrent_file = Utils.parser.Parse<Torrent>(file);
                string torrent_name = torrent_file.DisplayName;
                string torrent_filedata_path = serialized_path + torrent_name;
                TFData file_data;
                if (File.Exists(torrent_filedata_path))
                {
                    file_data = Utils.DeserializeTFData(torrent_filedata_path);
                    torrent_data_files.Remove(torrent_filedata_path);
                }
                else
                {
                    file_data = new TFData(torrent_file, torrent_name);
                    Utils.SerializeTFData(file_data);
                }
                file_managers_dict.Add(torrent_name, new FileManager(torrent_file));
                torrent_file_dict.Add(torrent_name, file_data);
                semaphore_dict.Add(torrent_name, new Semaphore(1, 1));
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
                TrackerManager tracker = new TrackerManager(torrent.Value, torrent_file_dict[torrent.Key]);
                tracker.Initialize();
            }

            Thread.Sleep(2000);
            // Create thread-pool for downloading and uploading (29 down, 1 up)
            int threads = 30;
            TorrentTask.thread_pool = new SemaphoreSlim(threads, threads);
            main_semaphore = new Semaphore(1, 1);
            // For each torrent, spin up threads to download pieces (blocks when thread_pool is exhausted)
            // Only downloads from one torrent at a time, once a torrent is finished downloading, start on the next torrent
            // Todo: Implement logic to switch from one torrent to the next (and to mark finished torrents as complete)
            Queue<string> download_queue = new Queue<string>(torrent_file_dict.Keys);
            int tc = 1;
            while (download_queue.Count > 0)
            {
                main_semaphore.WaitOne();
                TorrentTask.thread_pool.Wait();
                TFData curr_tfdata = torrent_file_dict[download_queue.Peek()];
                while (download_queue.Count > 0 && curr_tfdata.peer_list_indx >= curr_tfdata.peer_list.Count)
                {
                    download_queue.Dequeue();
                    if (download_queue.Count > 0)
                    {
                        curr_tfdata = torrent_file_dict[download_queue.Peek()];
                    }
                }
                if (download_queue.Count <= 0)
                {
                    Console.WriteLine("Leaving Main Proc");
                    break;
                }
                DownloadingTask task = new DownloadingTask(torrent_file_dict[download_queue.Peek()], file_managers_dict[download_queue.Peek()], semaphore_dict[download_queue.Peek()]);
                TorrentTask.thread_pool.Release();
                Task t = new Task(() => task.StartTask());
                Console.WriteLine("Starting new Task: " + tc);
                tc++;
                t.Start(); 
            }
            Thread.Sleep(3600000);
        }
    }
}
