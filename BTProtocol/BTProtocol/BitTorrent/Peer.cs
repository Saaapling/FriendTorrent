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
    internal abstract class Peer
    {
        public enum MessageType : int
        {
            Unknown = -3,
            Handshake = -2,
            KeepAlive = -1,
            Choke = 0,
            Unchoke = 1,
            Interested = 2,
            NotInterested = 3,
            Have = 4,
            Bitfield = 5,
            Request = 6,
            Piece = 7,
            Cancel = 8,
            Port = 9,
        }

        public string ip { get; set; }
        public int port { get; set; }

        public Peer(String ipaddr, int port)
        {
            this.port = port;
            this.ip = ipaddr;
        }

        public abstract void StartPeerThread();
    }
}
