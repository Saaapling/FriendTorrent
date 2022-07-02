using System;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net.Http;
using System.Net;
using System.IO;

using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;

namespace BTProtocol.BitTorrent
{
    class Tracker
    {
        public static void PrintTorrentDict(Torrent tf)
        {
            BDictionary dictionary = tf.ToBDictionary();
            string info_hash = tf.GetInfoHash();

            Console.WriteLine(info_hash);
            Console.WriteLine(string.Join("\n", dictionary.Select(m => $"{m.Key}={m.Value}")));
            Console.WriteLine(((BDictionary)dictionary["info"])["name"]);
            //Console.WriteLine(string.Join("\n", ((BDictionary) dictionary["info"]).Select(m => $"{m.Key}={m.Value}")));

            Console.ReadLine();
        }

        public Torrent torrent_file { get; }
        public string peerid { get; }
        private static BencodeParser parser = new BencodeParser();

        private string UrlSafeStringInfohash(byte[] Infohash)
        {
            return Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(Infohash, 0, 20));
        }

        private string EventToString(Events _event)
        {
            string ret = "";
            
            switch (_event)
            {
                case Events.started:
                    ret = "started";
                    break;
                case Events.paused:
                    ret = "paused";
                    break;
                case Events.stopped:
                    ret = "stopped";
                    break;
                default:
                    ret = "paused";
                    break;
            }

            return ret;
        }

        public Tracker(Torrent torrentfile)
        {
            this.torrent_file = torrentfile;
            StringBuilder sb = new StringBuilder();
            sb.Append("ds_bit");
            sb.Append(Environment.MachineName.ToString());
            if (sb.Length >= 20)
            {
                peerid = sb.ToString().Substring(0, 20);
            }
            else
            {
                int rest = 20 - sb.Length;
                Random random = new Random();
                for (int i = 0; i < rest; i++)
                {
                    sb.Append(random.Next() % 10);
                }
                peerid = sb.ToString();
            }
        }

        public void SendRecvToTracker(TFData tfdata)
        {
            BDictionary dictionary = torrent_file.ToBDictionary();

            StringBuilder sb = new StringBuilder();
            sb.Append(dictionary["announce"].ToString());
            sb.Append("?info_hash=").Append(UrlSafeStringInfohash(torrent_file.GetInfoHashBytes()));
            sb.Append("&peer_id=").Append(peerid);
            sb.Append("&port=").Append(6881);
            sb.Append("&uploaded=").Append(tfdata.bytes_uploaded);
            sb.Append("&downloaded=").Append(tfdata.bytes_downloaded);
            sb.Append("&left=").Append(tfdata.bytes_left);
            sb.Append("&event=").Append(EventToString(tfdata._event));
            sb.Append("&compact=").Append(tfdata.compact);

            Console.WriteLine(sb.ToString());

            HttpWebRequest HttpWReq = (HttpWebRequest)WebRequest.Create(sb.ToString());
            HttpWebResponse HttpWResp = (HttpWebResponse)HttpWReq.GetResponse();

            byte[] data;
            using (Stream stream = HttpWResp.GetResponseStream())
            {
                data = new byte[HttpWResp.ContentLength];
                stream.Read(data, 0, Convert.ToInt32(HttpWResp.ContentLength));
            }

            BDictionary tracker_dict = parser.Parse<BDictionary>(data);
            Console.WriteLine(string.Join("\n", tracker_dict.Select(m => $"{m.Key}={m.Value}")));

            Console.ReadLine();
        }

    }
}
