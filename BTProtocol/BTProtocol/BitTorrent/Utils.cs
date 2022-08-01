using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BTProtocol.BitTorrent
{
    internal class Utils
    {

        public static BencodeParser parser = new BencodeParser();

        public static string UrlSafeStringInfohash(byte[] Infohash)
        {
            return Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(Infohash, 0, 20));
        }

        public static void WriteToFile(String filepath, byte[] data)
        {
            using var writer = new BinaryWriter(File.OpenWrite(filepath));
            writer.Write(data);
        }

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
    }
}
