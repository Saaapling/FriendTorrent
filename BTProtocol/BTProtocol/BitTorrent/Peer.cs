using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;


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
        private TcpClient client;

        public bool[] bitfield { get; set; }
        public List<int> high_priority_pieces;
        public List<int> low_priority_pieces;

        public string ip { get; private set; }
        public int port { get; private set; }

        public bool interested { get; set; }

        public Peer(TcpClient client, int bitfield_size, string torrent_name)
        {
            this.client = client;
            ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
            bitfield = new bool[bitfield_size];
            interested = false;
            high_priority_pieces = new List<int>();
            low_priority_pieces = new List<int>();
        }

        public Peer(TcpClient client)
        {
            this.client = client;
            ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
            interested = false;
        }

        public bool CheckInterested()
        {
            return (high_priority_pieces.Count + low_priority_pieces.Count) > 0;
        }

        public bool Connected() 
        {
            return client.Connected;
        }

        public NetworkStream GetStream() 
        {
            return client.GetStream();
        }

        public void Close()
        {
            client.Close();
        }

        public int GetNextPiece(TFData torrent_data)
        {
            // Returns a piece index the peer owns that we are interested in, or -1 otherewise
            Random rnd = new Random();
            int index = -1;
            int high_size = high_priority_pieces.Count;
            while (high_size > 0 && index == -1)
            {
                index = high_priority_pieces[rnd.Next(0, high_size)];

                int piece_status = torrent_data.piece_status[index];
                if (piece_status == 1)
                {
                    high_size--;
                    high_priority_pieces.Remove(index);
                    index = -1;
                }else if (piece_status == 2)
                {
                    high_size--;
                    high_priority_pieces.Remove(index);
                    low_priority_pieces.Add(index);
                    index = -1;
                }
            }

            int low_size = low_priority_pieces.Count;
            while (low_size > 0 && index == -1)
            {
                index = low_priority_pieces[rnd.Next(0, low_size)];
                int piece_status = torrent_data.piece_status[index];

                if (piece_status == 1)
                {
                    low_size--;
                    high_priority_pieces.Remove(index);
                    index = -1;
                }
            }

            return index;
        }
    }
}
