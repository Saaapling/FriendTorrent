using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    public enum MessageType : int
    {
        Choke = 0,
        Unchoke = 1,
        Interested = 2,
        NotInterested = 3,
        Have = 4,
        Bitfield = 5,
        Request = 6,
        Piece = 7,
        Cancel = 8,
    }
    public class Peer
    {
        public bool[] bitfield { get; set; }

        public string ip { get; set; }
        public int port { get; set; }
        public NetworkStream netstream { get; set; }


        public Peer(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }
    }
}
