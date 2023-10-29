using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using static BTProtocol.BitTorrent.Logger;


using System.Runtime.Serialization;
using System.Xml;
using System.Net.Http;

namespace BTProtocol.BitTorrent
{
    internal static class Utils
    {
        public static Logger logger = new Logger(LoggingLevel.Debug, (int) DebugFlags.All);

        public static BencodeParser parser = new BencodeParser();
        public const int BLOCK_SIZE = 16384; //2^14
        static BinaryFormatter serializer = new BinaryFormatter();
        public static string LOCAL_IP_ADDRESS = GetLocalIPAddress();
        public static string PUBLIC_IP_ADDRESS = GetPublicIPAddress();
        public static int SEEDING_PORT = 6889;

        public static string UrlSafeStringInfohash(byte[] Infohash)
        {
            return Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(Infohash, 0, 20));
        }

        public static void WriteToFile(string filepath, byte[] data)
        {
            using var writer = new BinaryWriter(File.OpenWrite(filepath));
            writer.Write(data);
        }

        public static byte[] ReadHexAsByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static int ParseInt(byte[] byte_buffer, int index = 0)
        {
            byte[] int_bytes = new byte[4];
            Array.Copy(byte_buffer, index, int_bytes, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(int_bytes);
            return BitConverter.ToInt32(int_bytes, 0);
        }

        public static Int64 ParseInt64(byte[] byte_buffer, int index = 0)
        {
            byte[] int_bytes = new byte[8];
            Array.Copy(byte_buffer, index, int_bytes, 0, 8);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(int_bytes);
            return BitConverter.ToInt64(int_bytes, 0);
        }

        public static byte[] Int16ToByteArray(Int16 x)
        {
            byte[] size_bytes = BitConverter.GetBytes(x);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(size_bytes);

            return size_bytes;

        }

        public static byte[] Int32ToByteArray(int x)
        {
            byte[] size_bytes = BitConverter.GetBytes(x);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(size_bytes);

            return size_bytes;
        }


        public static byte[] Int64ToByteArray(Int64 x)
        {
            byte[] size_bytes = BitConverter.GetBytes(x);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(size_bytes);

            return size_bytes;
        }

        public static byte GetBitfieldByte(int[] piece_status, int pointer)
        {
            int value = 0;
            for (int i = 0; i < 8 && pointer + i < piece_status.Length; i++)
            {
                if (piece_status[pointer + i] == 1)
                {
                    value += 1 << (7 - i);
                }
            }

            return (byte)value;
        }

        public static void PrintTorrentDict(Torrent tf)
        {
            BDictionary dictionary = tf.ToBDictionary();
            string info_hash = tf.GetInfoHash();

            logger.Debug(info_hash);
            logger.Debug(string.Join("\n", dictionary.Select(m => $"{m.Key}={m.Value}")));
            logger.Debug(((BDictionary)dictionary["info"])["name"].ToString());
            //logger.Debug(string.Join("\n", ((BDictionary) dictionary["info"]).Select(m => $"{m.Key}={m.Value}")));

            Console.ReadLine();
        }

        public static void SerializeTFData(TFData file_data)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(TFData));
            FileStream writer = new FileStream(FriendTorrent.serialized_path + file_data.torrent_name, FileMode.Create);
            serializer.WriteObject(writer, file_data);
            writer.Close();
        }

        public static TFData DeserializeTFData(string file_path)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(TFData));
            FileStream openFileStream = new FileStream(file_path, FileMode.Open);
            XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(openFileStream, new XmlDictionaryReaderQuotas());
            TFData file_data = (TFData)serializer.ReadObject(reader, true);
            file_data.ResetStatus();
            openFileStream.Close();
            return file_data;
        }

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    logger.Info($"Local IP Address: {ip.ToString()}");
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private static string GetPublicIPAddress()
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.GetAsync("http://checkip.dyndns.org/").Result;
            string address = response.Content.ReadAsStringAsync().Result;

            int first = address.IndexOf("Address: ") + 9;
            int last = address.LastIndexOf("</body>");
            address = address.Substring(first, last - first);
            logger.Info($"Public IP Address: {address}");
            //address = "173.67.0.40"

            return address;
        }
    }
}