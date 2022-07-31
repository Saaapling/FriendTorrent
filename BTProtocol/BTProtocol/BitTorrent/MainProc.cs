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

namespace BTProtocol.BitTorrent
{

    enum Events : UInt16
    {
        started = 0,
        paused  = 1,
        stopped = 2,
    }

    [Serializable()]
    struct TFData
    {
        public string torrent_name { get; }
        public string resource_path { get; }

        public byte[] piece_hash { get; }
        public long piece_size { get; }

        public int[] peice_status; // 0 - Not Downloaded, 1 - Downloaded, 2 - In Progress
        public uint bytes_uploaded;
        public uint bytes_downloaded;
        public UInt64 bytes_left;

        public HashSet<(string, int)> peer_list { get; set; }

        public Events _event;
        public bool compact;

        public TFData(string torrent_name, string resource_path, byte[] piece_hash, long piece_size)
        {
            this.torrent_name = torrent_name;
            this.resource_path = resource_path;
            this.piece_hash = piece_hash;
            this.piece_size = piece_size;
            this.peer_list = new HashSet<(string, int)>();

            peice_status = new int[piece_hash.Length];
            bytes_uploaded = 0;
            bytes_downloaded = 0;
            bytes_left = Convert.ToUInt64(piece_size * piece_hash.Length);
            _event = Events.started;
            compact = true;
        }

        public void reset_status()
        {
            peer_list = new HashSet<(string, int)>();
            for (int i = 0; i < peice_status.Length; i++)
            {
                if (peice_status[i] == 2)
                {
                    peice_status[i] = 1;
                }
            }
        }
    }
    
    class MainProc
    {
        const string resource_path = @"../../Resources/";
        const string serailized_path = resource_path + "TorrentData/";
        static Dictionary<string, TFData> torrent_file_dict = new Dictionary<string, TFData>();

        static BencodeParser parser = new BencodeParser();

        public static Semaphore thread_pool;

        private static Dictionary<string, Torrent> parse_torrent_files(string resource_path)
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
                Torrent torrent_file = parser.Parse<Torrent>(file);
                TFData file_data;
                if (File.Exists(torrent_filedata_path))
                {
                    Stream openFileStream = File.OpenRead(torrent_filedata_path);
                    BinaryFormatter deserializer = new BinaryFormatter();
                    file_data = (TFData)deserializer.Deserialize(openFileStream);
                    file_data.reset_status();

                    Console.WriteLine("Torrent found: " + torrent_name);
                    torrent_data_files.Remove(torrent_filedata_path);
                }
                else
                {
                    file_data = new TFData(torrent_name, file, torrent_file.Pieces, torrent_file.PieceSize);

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
            Tracker.init_peerid();
            Dictionary<string, Torrent> torrents = parse_torrent_files(resource_path);

            // For each torrent file found, create and contact its tacker, and set up
            // Timers to reconnect with the tracker after a specified amount of time has passed
            foreach (KeyValuePair<string, Torrent> torrent in torrents)
            {
                Tracker tracker = new Tracker(torrent.Value);
                BDictionary dictionary = torrent.Value.ToBDictionary();
                TFData torrent_data = torrent_file_dict[torrent.Key];
                int wait_time = tracker.SendRecvToTracker(torrent_data, dictionary["announce"].ToString());
                var stateTimer = new Timer(tracker.updateTracker, torrent_data, wait_time * 1000, wait_time * 1000);
            }

            // Create thread-pool for downloading and uploading (29 down, 1 up)
            thread_pool = new Semaphore(30, 30);

            // For each torrent, spin up threads for each peer (blocks when thread_pool is exhausted)
            // Potential Issues: For torrents with large # of peers, could be inefficient once download fiinishes and process cycles through remaining peers
            foreach (TFData data in torrent_file_dict.Values)
            {
                foreach ((String, int) ip_addr in data.peer_list)
                {
                    thread_pool.WaitOne();
                    Console.WriteLine(ip_addr.Item1 + ":" + ip_addr.Item2);
                    DownloadingPeer new_peer = new DownloadingPeer(ip_addr.Item1, ip_addr.Item2, data);
                    thread_pool.Release();
                    Thread t = new Thread(new ThreadStart(new_peer.start_peer_thread));
                }
            }


            //There exists the question of how to handle the main thread: currently it exits after completing the above task, but in the
            //final form, the main thread presumable is connected to a window/display and would not close until the user exits the window
            //In addition, the seeding peer would also never close. 
            //Thus, in the current state, this long sleep represents the main thraed having a window open

            //Sleeps for an hour
            Thread.Sleep(3600000);
        }

    }
}
