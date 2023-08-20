using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    internal class DownloadingThreadManager
    {
        public static Semaphore main_semaphore;
        public static SemaphoreSlim thread_pool;
        private Queue<string> download_queue;

        public DownloadingThreadManager(int threadcount)
        {
            main_semaphore = new Semaphore(1, 1);
            thread_pool = new SemaphoreSlim(threadcount, threadcount);
            main_semaphore.WaitOne();
            thread_pool.Wait();
        }

        private TFData CheckTorrentStatus()
        {
            // Fetch the next torrent to be downloaded
            TFData curr_tfdata = MainProc.torrent_file_dict[download_queue.Peek()];
            while (curr_tfdata.CheckDownloadStatus())
            {
                download_queue.Dequeue();
                if (download_queue.Count <= 0)
                {
                    Console.WriteLine("Leaving Downloading Manager - No torrents remaining");
                    return null;
                }
                curr_tfdata = MainProc.torrent_file_dict[download_queue.Peek()];
            }
            return curr_tfdata;
        }

        private TFData GetNextTorrent()
        {
            TFData tf_data = CheckTorrentStatus();

            // Check Peer availability
            /* When there is only exhausted torrent remaining (and it has exhausted the available peers), this loops prevents the downloading 
               manager from creating downloading tasks that instantly terminate due to lack of available peers */
            while (tf_data != null && tf_data.peer_list_indx >= tf_data.peer_list.Count)
            {
                download_queue.Dequeue();
                

                if (MainProc.tracker_dict[tf_data.torrent_name].ContactTracker())
                {
                    tf_data.peer_list_indx = 0;
                }
                download_queue.Enqueue(tf_data.torrent_name);
                tf_data = CheckTorrentStatus();
            }
            return tf_data;
        }

        public void StartDownloads()
        {
            // For each torrent, spin up threads to download pieces (blocks when thread_pool is exhausted)
            // Only downloads from one torrent at a time, once a torrent is finished downloading, start on the next torrent
            // Todo: Implement logic to switch from one torrent to the next (and to mark finished torrents as complete)

            download_queue = new Queue<string>(MainProc.torrent_file_dict.Keys);
            int tc = 0;
            while (download_queue.Count > 0)
            {
                TFData curr_tfdata = GetNextTorrent();
                if (curr_tfdata == null)
                {
                    return;
                }

                // Start a new downloading task
                string torrent_name = download_queue.Peek();
                DownloadingTask task = new DownloadingTask(curr_tfdata, MainProc.file_dict[torrent_name]);
                thread_pool.Release();

                tc++;
                Console.WriteLine("Starting new Downloading Task: " + tc);
                Task t = new Task(() => task.StartTask());
                t.Start();


                main_semaphore.WaitOne();
                thread_pool.Wait();
            }

            Console.WriteLine("Downloading Manager Exited");
        }
    }
}
