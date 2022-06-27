using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net.Http;

using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System.Net;
using System.IO;

namespace BTProtocol
{
    class Program
    {

        static void PrintTorrentDict()
        {
            var parser = new BencodeParser();
            string path = @"..\..\Resources\ex-arm.torrent";
            Torrent torrent_file = parser.Parse<Torrent>(path);

            BDictionary dictionary = torrent_file.ToBDictionary();
            string info_hash = torrent_file.GetInfoHash();


            Console.WriteLine(info_hash);
            Console.WriteLine(string.Join("\n", dictionary.Select(m => $"{m.Key}={m.Value}")));
            Console.WriteLine(((BDictionary)dictionary["info"])["name"]);
            //Console.WriteLine(string.Join("\n", ((BDictionary) dictionary["info"]).Select(m => $"{m.Key}={m.Value}")));

            Console.ReadLine();
        }

        public static string UrlSafeStringInfohash (byte[] Infohash) 
        { 
            return Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(Infohash, 0, 20));
        }

        static void Main(string[] args)
        {
            var parser = new BencodeParser();
            string path = @"..\..\Resources\ex-arm.torrent";
            Torrent torrent_file = parser.Parse<Torrent>(path);
            BDictionary dictionary = torrent_file.ToBDictionary();


            StringBuilder sb = new StringBuilder();
            sb.Append(dictionary["announce"].ToString());
            sb.Append("?info_hash=").Append(UrlSafeStringInfohash(torrent_file.GetInfoHashBytes()));
            sb.Append("&peer_id=").Append("ds_bit14943629834679");
            sb.Append("&port=").Append(6881);
            sb.Append("&uploaded=").Append(0);
            sb.Append("&downloaded=").Append(0);
            sb.Append("&left=").Append(torrent_file.TotalSize);
            sb.Append("&event=").Append("paused");
            sb.Append("&compact=").Append(1);

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
