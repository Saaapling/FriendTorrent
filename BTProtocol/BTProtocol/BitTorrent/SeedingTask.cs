using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    public class SeedingTask
    {
        public SeedingTask()
        {
        }

        private void StartServer()
        {
            /*
            IPAddress ipAddress = IPAddress.Parse(ip);
            IPEndPoint local = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(local);

            listener.Listen(100);
            Socket handler = listener.Accept();
            */

            throw new NotImplementedException();
        }

        public Action StartTask()
        {
            throw new NotImplementedException();
        }
    }
}
