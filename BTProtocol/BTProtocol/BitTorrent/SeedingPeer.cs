using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    internal class SeedingPeer : Peer
    {
        public SeedingPeer(string ipaddr, int port) : base(ipaddr, port)
        {
        }

        private void StartServer()
        {
            IPAddress ipAddress = IPAddress.Parse(ip);
            IPEndPoint local = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(local);

            listener.Listen(100);
            Socket handler = listener.Accept();
        }

        public override void start_peer_thread()
        {
            throw new NotImplementedException();
        }
    }
}
