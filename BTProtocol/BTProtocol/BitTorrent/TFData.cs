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
    public class TFData
    {
        public string torrent_name { get; }
        public string resource_path { get; }

        public byte[] info_hash { get; }
        public byte[] piece_hash { get; }
        public long piece_size { get; }

        public int[] piece_status; // 0 - Not Downloaded, 1 - Downloaded, 2 - In Progress
        public uint bytes_uploaded;
        public uint bytes_downloaded;
        public UInt64 bytes_left;

        public int peer_list_indx;
        public List<(string, int)> peer_list { get; set; }

        public Events _event;
        public byte compact; // 0 - False, 1 - True

        public TFData(string torrent_name, string resource_path, byte[] info_hash, byte[] piece_hash, long piece_size)
        {
            this.torrent_name = torrent_name;
            this.resource_path = resource_path;
            this.info_hash = info_hash;
            this.piece_hash = piece_hash;
            this.piece_size = piece_size;
            peer_list = new List<(string, int)>();
            peer_list_indx = 0;

            piece_status = new int[piece_hash.Length / 20];
            bytes_uploaded = 0;
            bytes_downloaded = 0;
            bytes_left = Convert.ToUInt64(piece_size * piece_hash.Length / 20);
            _event = Events.started;
            compact = 1;
        }

        public void ResetStatus()
        {
            peer_list_indx = 0;
            peer_list.Clear();
            for (int i = 0; i < piece_status.Length; i++)
            {
                if (piece_status[i] == 2)
                {
                    piece_status[i] = 1;
                }
            }
        }

        public bool CheckDownloadStatus()
        {
            for (int i = 0; i < piece_status.Length; i++)
            {
                if (piece_status[i] == 0)
                {
                    return false;
                }
            }
            return true;
        }

        public void SetPieceStatus(int piece, int status)
        {
            piece_status[piece] = status;
        }
    }

}
