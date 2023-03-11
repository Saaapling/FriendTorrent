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
            client = new TcpClient();
        }

        public void ExitThread()
        {
            MainProc.thread_pool.Release();
        }

        public void StartTask()
        {
            /*
             * Downloading Task initialization steps:
             *      - Check whether there remains to be any pieces to be downloaded
             *      - Find an available, unvisted peer to connect to
             *      - Connect to the peer and start downloading, or exit the Task if no peers are avaiable
             */
            Console.WriteLine("A");
            // Call WaitOne to decrement the count of available threads.
            MainProc.thread_pool.WaitOne();
            // Release the lock on main so it can continue execution.
            // MainProc.main_mutex.ReleaseMutex();
            MainProc.main_semaphore.Release();
            Console.WriteLine("B");

            // Check the TFData to see if there are pieces that stll need to be downloaded. 
            if (torrent_data.CheckDownloadStatus())
            {
                ExitThread();
            }

            (String, int) peer_addr;
            while (!client.Connected) 
            {
                peer_addr = FindAvailablePeer();
                if (peer_addr.Item1 != "")
                    InitiateConnection(peer_addr);
                else
                    ExitThread();
            }
            InitiateHandshake();
        }

        public (String, int) FindAvailablePeer()
        {
            // Find an unvisited peer to connect to
            (String, int) peer_addr = ("", 0);
            foreach ((String, int) ip_addr in torrent_data.peer_list)
            {
                if (!torrent_data.visited_peers.Contains(ip_addr))
                {
                    return ip_addr;
                }
            }
            return ("", -1);
        }

        public void InitiateConnection((String, int) peer_addr)
        {
            torrent_data.visited_peers.Add(peer_addr);
            String ipaddr = peer_addr.Item1;
            int port = peer_addr.Item2;
            Peer connection = new Peer(ipaddr, port);

            IPEndPoint ip_endpoint = new IPEndPoint(IPAddress.Parse(ipaddr), port);

            if (!client.ConnectAsync("173.67.0.40", 80).Wait(10000))
            {
                Console.WriteLine("Peer refused connection");
            }
            else
            {
                netstream = client.GetStream();
                netstream.ReadTimeout = 30000;
            }
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

        public void ReceivePacket(IAsyncResult ar)
        {
            Console.WriteLine("I got here");
            /*
             * Continuously read in new packets from the peer.
             * The proess of reading in packets is as follows:
             *      - Read in 4 bytes to determine the size of the packets. If the size is 0, it is a keep-alive packet
             *      - Read in the full packet to a byte array with size as specified 
             *      - Get the message type of the packet (First 4 bytes)
             *      - Use a case statement to proess the full packet
             *      - Continue readng packets from the peer until the peer closes the connection, or there are no more pieces to download from the peer
            */
            while(true)//while (torrent_data.CheckDownloadStatus())
            {
                byte[] byte_buffer = new byte[12];
                netstream.Read(byte_buffer, 0, 11);
                Console.WriteLine(byte_buffer);
                /*byte[] byte_buffer = new byte[4];
                int packet_size = 0;
                while (packet_size == 0)
                {
                    netstream.Read(byte_buffer, 0, 4);
                    packet_size = BitConverter.ToInt32(byte_buffer, 0);
                }
                Console.WriteLine("Packet Size: ");
                Console.WriteLine(packet_size);*/
                Thread.Sleep(50000);
            }
        }

        public void ReceiveHandshake()
        {
            byte[] bytes = new byte[68];
            int bytes_read = netstream.Read(bytes, 0, 68);

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
