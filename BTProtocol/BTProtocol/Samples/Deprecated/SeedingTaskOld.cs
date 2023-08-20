using BencodeNET.Torrents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static BTProtocol.BitTorrent.MessageType;

namespace BTProtocol.BitTorrent
{
    sealed internal class SeedingTaskOld : TorrentTask
    {
        private TcpListener listener;
        private Dictionary<string, List<Peer>> torrent_peer_dict;
        private Dictionary<string, Peer> ip_peer_dict;

        private Queue<Tuple<MessageType, Peer, byte[]>> backlog;

        private List<Peer> unchoked_peers = new List<Peer>();

        private const int port = 6889;
        /* Variables that we need:
         * Map of Torrent -> Peers
         * Map of IP -> Peers
         *      - Modify Peers to include a variable tracking what torrent its associated with
         * Queue of waiting messages
         * List of interested and choked peers (Potentially uneeded) 
         * 
         * FileManager get pieces from TFData
         *      
         * Methods/Things needed:
         *      - Function to choke/unchoke peers
         *      - 'Broadcasting Task'? (Subtask of seeding that sends peers haves/chokes, triggered by an event call)
         *      -  Request (adds to queue of waiting messages)
         *      
         * Unimplemented Logic:
         *      - Handshake check
         */

        protected override private void ExitThread()
        {
        
        }

        public SeedingTaskOld(Dictionary<string, Torrent> torrents)
        {
            torrent_peer_dict = new Dictionary<string, List<Peer>>();
            foreach (KeyValuePair<string, Torrent> torrent in torrents)
            {
                torrent_peer_dict.Add(torrent.Key, new List<Peer>());
            }
            ip_peer_dict = new Dictionary<string, Peer>();
            backlog = new Queue<Tuple<MessageType, Peer, byte[]>>();
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine(ip.ToString());
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public Action StartTask()
        {
            IPAddress ipAddress = IPAddress.Parse(GetLocalIPAddress());
            IPEndPoint local = new IPEndPoint(ipAddress, port);

            listener = new TcpListener(local);
            listener.Start();
            while (true)
            {
                // Accept any incoming connections
                AcceptConnections();

                // Read any incoming messages
                ReadSockets();

                // Process messages in the stored backlog


            }
        }

        private void AcceptConnections()
        {
            while (true)
            {
                bool available = listener.Server.Poll(0, SelectMode.SelectRead);
                if (available)
                {
                    TcpClient new_connection = listener.AcceptTcpClient();
                    Console.WriteLine("Client connected: {0}", new_connection.Client.RemoteEndPoint.ToString());

                    string ip_addr = new_connection.Client.RemoteEndPoint.ToString();
                    Peer new_peer = new Peer(new_connection);
                    new_peer.GetStream().ReadTimeout = 100;
                    ip_peer_dict.Add(ip_addr, new_peer);
                }
                else
                {
                    break;
                }
            }
        }

        // Todo: Still need to find a way to check whether the peer is still connected, a try-catch could work
        private void ReadSockets()
        {
            foreach (KeyValuePair<string, Peer> kv_pair in ip_peer_dict)
            {
                Peer peer = kv_pair.Value;
                byte[] byte_buffer = new byte[4];
                int packet_size;
                try
                {
                    while (true)
                    {
                        if (!peer.handshake)
                        {
                            string torrent_name = ReceiveHandshakeSeeding();
                            torrent_peer_dict[torrent_name].Add(peer);
                        }

                        peer.GetStream().Read(byte_buffer, 0, 4);
                        packet_size = Utils.ParseInt(byte_buffer);
                        if (packet_size > 0)
                        {
                            byte_buffer = new byte[packet_size];
                            int bytes_read = 0;
                            while (bytes_read < packet_size)
                            {
                                bytes_read += peer.GetStream().Read(byte_buffer, bytes_read, packet_size - bytes_read);
                            }

                            MessageType packet_type = (MessageType)byte_buffer[0];
                            backlog.Enqueue(new Tuple<MessageType, Peer, byte[]>(packet_type, peer, byte_buffer));
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        private void ReceivePacket(string ip)
        {
            while (true)
            {
                Tuple<MessageType, Peer, byte[]> message = backlog.Dequeue();
                switch (message.Item1)
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

        private void ReceiveRequest(Peer peer)
        {
            if (!unchoked_peers.Contains(peer))
            {
                return;
            }
        }

        private void SendPieceBlock()
        {

        }
    }
}
