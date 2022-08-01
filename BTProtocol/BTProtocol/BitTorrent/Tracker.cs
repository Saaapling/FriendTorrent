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
        public Torrent torrent_file { get; }

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
        }

        public int SendRecvToTracker(TFData tfdata, string announce_url)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append(announce_url);
            sb.Append("?info_hash=").Append(Utils.UrlSafeStringInfohash(torrent_file.GetInfoHashBytes()));
            sb.Append("&peer_id=").Append(MainProc.peerid);
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

            BDictionary tracker_dict = Utils.parser.Parse<BDictionary>(data);
            
            BString peers = tracker_dict.Get<BString>("peers");
            byte[] buffer = peers.Value.ToArray();

            for (int i = 0; i < buffer.Length; i += 6)
            {
                String ip = ((int)buffer[i] + "." + (int)buffer[i + 1] + "." + (int)buffer[i + 2] + "." + (int)buffer[i + 3]);
                int port = (int)buffer[i + 4] << 8;
                port += (int)buffer[i + 5];
                //Console.WriteLine(ip + ":" + port);
                tfdata.peer_list.Add((ip, port));
            }

            return Int32.Parse(tracker_dict["interval"].ToString());
        }

        public void updateTracker(Object input)
        {
            BDictionary dictionary = torrent_file.ToBDictionary();

            TFData tfdata = (TFData)input;
            SendRecvToTracker(tfdata, dictionary["announce"].ToString());
        }
    }
}
