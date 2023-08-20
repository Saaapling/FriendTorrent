using BencodeNET.Torrents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{

    public enum Events : UInt16
    {
        none = 0,
        completed = 1,
        started = 2,
        stopped = 3,
        paused = 4
    }

    [Serializable()]
    public class TFData
    {
        public string torrent_name { get; }

        public byte[] info_hash { get; }
        public byte[][] piece_hash { get; }
        public long torrent_size { get; }

        public int[] piece_status; // 0 - Not Downloaded, 1 - Downloaded, 2 - In Progress
        public uint bytes_uploaded;
        public uint bytes_downloaded;

        public int peer_list_indx;
        public List<(string, int)> peer_list { get; set; }
        public List<(string, int)> connected_peers { get; set; }

        public Events _event;
        public byte compact; // 0 - False, 1 - True

        public TFData(Torrent torrent_data, string filename)
        {
            torrent_name = filename;
            info_hash = torrent_data.GetInfoHashBytes();
            torrent_size = torrent_data.TotalSize;
            piece_hash = new byte[torrent_data.Pieces.Length / 20][];
            for (int i = 0, idx = 0; i < torrent_data.Pieces.Length; i+=20, idx++)
            {
                piece_hash[idx] = new byte[20];
                Array.Copy(torrent_data.Pieces, i, piece_hash[idx], 0, 20);
            }
            peer_list = new List<(string, int)>();
            connected_peers = new List<(string, int)>();
            peer_list_indx = 0;
            piece_status = new int[piece_hash.Length];
            bytes_uploaded = 0;
            bytes_downloaded = 0;
            _event = Events.started;
            compact = 1;
        }

        public void ResetStatus()
        {
            peer_list_indx = 0;
            peer_list.Clear();
            connected_peers.Clear();
            for (int i = 0; i < piece_status.Length; i++)
            {
                if (piece_status[i] == 2)
                {
                    piece_status[i] = 0;
                }
            }
        }

        public bool CheckDownloadStatus()
        {
            for (int i = 0; i < piece_status.Length; i++)
            {
                if (piece_status[i] != 1)
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsActive()
        {
            for (int i = 0; i < piece_status.Length; i++)
            {
                if (piece_status[i] == 1)
                {
                    return true;
                }
            }
            return false;
        }

        // Todo: Serialize piece, can have a function in utils that does the serialization. Look at mainproc
        public bool SetPieceStatus(int piece, int status)
        {
            bool result = false;
            if (piece_status[piece] != 1)
            {
                piece_status[piece] = status;
                result = true;
            }
            return result;
        }

        public bool VerifyPiece(int index, byte[] data)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                byte[] hash = sha1.ComputeHash(data);
                return hash.SequenceEqual(piece_hash[index]);
            }
        }
    }

}
