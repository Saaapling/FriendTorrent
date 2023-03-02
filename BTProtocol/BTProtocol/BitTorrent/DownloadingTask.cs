using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace BTProtocol.BitTorrent
{
    public class DownloadingTask
    {

        TFData torrent_data;
        Peer connection;
        TcpClient client;
        NetworkStream netstream;
        bool[] bitfield;

        public DownloadingTask(TFData tfdata)
        {
            torrent_data = tfdata;
        }

        public void ExitThread()
        {
            MainProc.thread_pool.Release();
        }

        public void InitiateConnection((String, int) peer_addr)
        {
            torrent_data.visited_peers.Add(peer_addr);
            String ipaddr = peer_addr.Item1;
            int port = peer_addr.Item2;
            Peer connection = new Peer(ipaddr, port);

            IPEndPoint ip_endpoint = new IPEndPoint(IPAddress.Parse(ipaddr), port);
            client = new TcpClient();
            client.Connect(ip_endpoint);
            netstream = client.GetStream();
            Thread.Sleep(20000);

            // Exchange a handshake with the new peer
            InitiateHandshake();
            ReceivePacket();
        }

        public void StartTask()
        {
            /*
             * Downloading Task initialization steps:
             *      - Check whether there remains to be any peices to be downloaded
             *      - Find an available, unvisted peer to connect to
             *      - Connect to the peer and start downloading, or exit the Task if no peers are avaiable
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
                ExitThread();
            }

            // Find an unvisited peer to connect to
            (String, int) peer_addr = ("", 0);
            foreach ((String, int) ip_addr in torrent_data.peer_list)
            {
                if (!torrent_data.visited_peers.Contains(ip_addr))
                {
                    peer_addr = ip_addr;
                    break;
                }
            }

            if (peer_addr.Item1 != "")
                InitiateConnection(peer_addr);
            else
                Console.WriteLine("No Peer found");
                ExitThread();

        }

        private void InitiateHandshake()
        {
            byte[] message = new byte[68];
            // Message Length
            message[0] = 19;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("BitTorrent protocol"), 0, message, 1, 19);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(torrent_data.info_hash), 0, message, 28, 20);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(MainProc.peerid), 0, message, 48, 20);

            netstream.Write(message, 0, message.Length);
        }

        public void ReceivePacket()
        {
            Console.WriteLine("I got here");

            // Todo: implement getmessagetype, move this into handshake response
            // Get Handshake from peer
            byte[] bytes = new byte[68];
            Thread.Sleep(5000);
            int bytes_read = netstream.Read(bytes, 0, 68);

            Console.WriteLine(bytes_read);
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
