using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{

    public enum Events : UInt16
    {
        started = 0,
        paused = 1,
        stopped = 2,
    }

    [Serializable()]
    public struct TFData
    {
        public string torrent_name { get; }
        public string resource_path { get; }

        public string info_hash { get; }
        public byte[] piece_hash { get; }
        public long piece_size { get; }

        public int[] peice_status; // 0 - Not Downloaded, 1 - Downloaded, 2 - In Progress
        public uint bytes_uploaded;
        public uint bytes_downloaded;
        public UInt64 bytes_left;

        public HashSet<(string, int)> peer_list { get; set; }
        public HashSet<(string, int)> visited_peers { get; set; }

        public Events _event;
        public byte compact; // 0 - False, 1 - True

        public TFData(string torrent_name, string resource_path, string info_hash, byte[] piece_hash, long piece_size)
        {
            this.torrent_name = torrent_name;
            this.resource_path = resource_path;
            this.info_hash = info_hash;
            this.piece_hash = piece_hash;
            this.piece_size = piece_size;
            this.peer_list = new HashSet<(string, int)>();
            this.visited_peers = new HashSet<(string, int)>();

            peice_status = new int[piece_hash.Length / 20];
            bytes_uploaded = 0;
            bytes_downloaded = 0;
            bytes_left = Convert.ToUInt64(piece_size * piece_hash.Length / 20);
            _event = Events.started;
            compact = 1;
        }

        public void ResetStatus()
        {
            peer_list.Clear();
            visited_peers.Clear();
            for (int i = 0; i < peice_status.Length; i++)
            {
                if (peice_status[i] == 2)
                {
                    peice_status[i] = 1;
                }
            }
        }
    }

}
