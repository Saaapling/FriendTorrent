using System;
using System.Collections.Generic;
using System.Linq;
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
            client = new TcpClient(ip, port);
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
             *          - If so, initiate download requests
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
        }

        void initiate_handshake()
        {

        }
    }
}
