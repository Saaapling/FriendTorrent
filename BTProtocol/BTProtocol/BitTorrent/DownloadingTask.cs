using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using static BTProtocol.BitTorrent.MessageType;

namespace BTProtocol.BitTorrent
{
    sealed internal class DownloadingTask : TorrentTask
    {

        TFData torrent_data;
        Peer peer;
        TcpClient client;

        public DownloadingTask(TFData tfdata)
        {
            torrent_data = tfdata;
        }

        public void StartTask()
        {
            /*
             * Downloading Task initialization steps:
             *      - Check whether there remains to be any pieces to be downloaded
             *      - Find an available, unvisted peer to connect to
             *      - Connect to the peer and start downloading, or exit the Task if no peers are avaiable
             */
            // Call WaitOne to decrement the count of available threads.
            
            thread_pool.Wait();
            // Release the lock on main so it can continue execution.
            MainProc.main_semaphore.Release();

            // Check the TFData to see if there are pieces that stll need to be downloaded. 
            while (!torrent_data.CheckDownloadStatus())
            {
                (String, int) peer_addr;
                while (client == null || !client.Connected)
                {
                    peer = FindAvailablePeer();
                    if (peer != null)
                        InitiateConnection();
                    else
                    {
                        ExitThread();
                        return;
                    }
                }

                try
                {
                    SendHandshake(torrent_data, peer.netstream);
                    ReceiveHandshake(torrent_data, peer.netstream);
                    SendBitField(torrent_data, peer.netstream);
                    ReceivePacket();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            ExitThread();
        }

        private Peer FindAvailablePeer()
        {
            // Find an unvisited peer to connect to
            int idx = Interlocked.Increment(ref torrent_data.peer_list_indx) - 1;
            if (idx < torrent_data.peer_list.Count)
            {
                return new Peer(torrent_data.peer_list[idx].Item1, torrent_data.peer_list[idx].Item2);
            }

            return null;
        }

        private void InitiateConnection()
        {
            //Console.WriteLine("Initiating Connection: " + peer_addr.Item1 + ":" +  peer_addr.Item2);
            string ipaddr = peer.ip;
            int port = peer.port;
            client = new TcpClient();

            if (client.ConnectAsync(ipaddr, port).Wait(500))
            {
                Console.WriteLine("Connection Successful: " + ipaddr + ":" + port);
                peer.netstream = client.GetStream();
                peer.netstream.ReadTimeout = 30000;
            }
        }

        private void ReceivePacket()
        {
            Console.WriteLine("Start Recieving Packets");
            /*
             * Continuously read in new packets from the peer.
             * The proess of reading in packets is as follows:
             *      - Read in 4 bytes to determine the size of the packets. If the size is 0, it is a keep-alive packet
             *      - Read in the full packet to a byte array with size as specified 
             *      - Get the message type of the packet (First 4 bytes)
             *      - Use a case statement to proess the full packet
             *      - Continue readng packets from the peer until the peer closes the connection, or there are no more pieces to download from the peer
            */
            while (!torrent_data.CheckDownloadStatus())
            {
                byte[] byte_buffer = new byte[4];
                int packet_size = -1;
                while (packet_size <= 0)
                {
                    Console.WriteLine("Trying to read");
                    peer.netstream.Read(byte_buffer, 0, 4);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(byte_buffer);
                    packet_size = BitConverter.ToInt32(byte_buffer, 0);
                }
                Console.WriteLine("Packet Size: " + packet_size);
                byte_buffer = new byte[packet_size];
                peer.netstream.Read(byte_buffer, 0, packet_size);

                MessageType packet_type = (MessageType) byte_buffer[0];
                switch (packet_type)
                {
                    case Choke:
                        break;
                    case Unchoke:
                        break;
                    case Interested:
                        break;
                    case NotInterested:
                        break;
                    case Have:
                        break;
                    case Bitfield:
                        ReceiveBitField(torrent_data, peer.netstream);
                        break;
                    case Request:
                        break;
                    case Piece:
                        break;
                    case Cancel:
                        break;

                    default:
                        throw new Exception("Unknown packet type");
                }
            }
        }
    }
}
