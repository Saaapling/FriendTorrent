using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using static BTProtocol.BitTorrent.Utils;
using static BTProtocol.BitTorrent.Logger;
using static BTProtocol.BitTorrent.TorrentTask;


namespace BTProtocol.BitTorrent
{
    internal class SeedingThreadManager
    {
        public static SemaphoreSlim thread_pool;
        private TcpListener listener;
        private const int port = 6889;

        public SeedingThreadManager(int threadcount)
        {
            thread_pool = new SemaphoreSlim(threadcount, threadcount);
        }


        public void StartSeeding()
        {
            IPAddress ipAddress = IPAddress.Parse(LOCAL_IP_ADDRESS);
            IPEndPoint local = new IPEndPoint(ipAddress, port);

            listener = new TcpListener(local);
            listener.Start();

            int tc = 0;
            while (true)
            {
                main_semaphore.WaitOne();
                thread_pool.Wait();

                // Accept an Incoming connection
                Peer peer = AcceptConnections();
                if (peer == null)
                {
                    continue;
                }

                SeedingTask task = new SeedingTask(peer);
                thread_pool.Release();

                tc++;
                logger.Info($"Starting new Seeding Task: {tc}");
                Task t = new Task(() => task.StartTask());
                t.Start();

                main_semaphore.WaitOne();
                thread_pool.Wait();
            }
        }

        private Peer AcceptConnections()
        {
            bool available = listener.Server.Poll(0, SelectMode.SelectRead);
            if (available)
            {
                TcpClient new_connection = listener.AcceptTcpClient();
                logger.Info($"Client connected: {new_connection.Client.RemoteEndPoint}");

                Peer new_peer = new Peer(new_connection);
                new_peer.GetStream().ReadTimeout = 5000;
                return new_peer;
            }
            return null;
        }
    }
}
