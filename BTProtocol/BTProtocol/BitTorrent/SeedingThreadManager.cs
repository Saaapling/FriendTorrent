using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    internal class SeedingThreadManager
    {
        public static Semaphore main_semaphore;
        public static SemaphoreSlim thread_pool;
        private TcpListener listener;
        private const int port = 6889;

        public SeedingThreadManager(int threadcount)
        {
            main_semaphore = new Semaphore(1, 1);
            thread_pool = new SemaphoreSlim(threadcount, threadcount);
            main_semaphore.WaitOne();
            thread_pool.Wait();
        }


        public void StartSeeding()
        {
            IPAddress ipAddress = IPAddress.Parse(Utils.LOCAL_IP_ADDRESS);
            IPEndPoint local = new IPEndPoint(ipAddress, port);

            listener = new TcpListener(local);
            listener.Start();

            int tc = 0;
            while (true)
            {
                // Accept an Incoming connection
                Peer peer = AcceptConnections();
                if (peer == null)
                {
                    continue;
                }

                SeedingTask task = new SeedingTask(peer);
                thread_pool.Release();

                tc++;
                Console.WriteLine("Starting new Seeding Task: " + tc);
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
                Console.WriteLine("Client connected: {0}", new_connection.Client.RemoteEndPoint.ToString());

                Peer new_peer = new Peer(new_connection);
                new_peer.GetStream().ReadTimeout = 50000;
                return new_peer;
            }
            return null;
        }
    }
}
