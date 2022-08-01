using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    internal class DownloadingPeer : Peer
    {

        TFData torrent_data;
        TcpClient client;
        NetworkStream netStream;
        bool[] bitfield;

        public DownloadingPeer(string ipaddr, int port, TFData tfdata) : base(ipaddr, port)
        {
            torrent_data = tfdata;
            IPEndPoint ip_endpoint = new IPEndPoint(IPAddress.Parse(ipaddr), port);
            client = new TcpClient();
            client.Connect(ip_endpoint);
            netStream = client.GetStream();
        }

        public void exit_thread()
        {
            MainProc.thread_pool.Release();
        }

        public override void start_peer_thread()
        {
            /*
             * Downloading Peer Thread initialization steps:
             *      - Check whether there remains to be any peices to be downloaded (Done first because it is possible this thread was not
             *          immediately created (created after another thread closed due to large number of available peers)
             *      - Exchange handshake with its peeer
             *      - Check whether the peer has any peices we want
             *          - If so, initiate download requests (Send interested flag)
             *          - If not, close the thread
             */
            MainProc.thread_pool.WaitOne();

            // Check the TFData to see what peices remain to be downloaded. 
            bool complete = true;
            foreach (int peice_status in torrent_data.peice_status)
            {
                if (peice_status == 0)
                {
                    complete = false;
                    break;
                }
            }
            if (complete)
            {
                exit_thread();
            }

            // Exchange a handshake with the associated peer
            initiate_handshake();

            recieve_packet();
        }

        private void initiate_handshake()
        {
            byte[] message = new byte[68];
            // Message Length
            message[0] = 19;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("BitTorrent protocol"), 0, message, 1, 19);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(torrent_data.info_hash), 0, message, 28, 20);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(MainProc.peerid), 0, message, 48, 20);

            netStream.Write(message, 0, message.Length);
        }

        public void recieve_packet()
        {
            Console.WriteLine("I got here");

            // Todo: implement getmessagetype, move this into handshake response
            // Get Handshake from peer
            byte[] bytes = new byte[68];
            int byteRead = netStream.Read(bytes, 0, 68);

            Utils.WriteToFile("../handshake", bytes);

            if (bytes[0] != 19)
            {
                Console.WriteLine("Invalid handshake, first byte must equal 19");
            }

            if (Encoding.UTF8.GetString(bytes.Skip(1).Take(19).ToArray()) != "BitTorrent protocol")
            {
                Console.WriteLine("Invalid handshake, protocol must equal \"BitTorrent protocol\"");
            }

            byte[] peer_hash = bytes.Skip(28).Take(20).ToArray();
            if (!peer_hash.SequenceEqual(Encoding.UTF8.GetBytes(torrent_data.info_hash)))
            {
                Console.WriteLine("Invalid handshake, peer returned a different info_hash");
            }
        }
    }
}
