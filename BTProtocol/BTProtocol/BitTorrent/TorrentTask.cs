using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static BTProtocol.BitTorrent.MessageType;

namespace BTProtocol.BitTorrent
{
    public class TorrentTask
    {
        public static SemaphoreSlim thread_pool;

        protected private void ExitThread()
        {
            thread_pool.Release();
        }

        public void SendBitField(TFData torrent_data, NetworkStream netstream)
        {
            MemoryStream byteStream = new MemoryStream();
            int size = (int) Math.Ceiling(torrent_data.piece_status.Length / 8d) + 1;
            byte[] size_bytes = BitConverter.GetBytes(size);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(size_bytes);
            byteStream.Write(size_bytes, 0, 4);
            byteStream.WriteByte((byte) Bitfield);
            for (int i = 0; i < size - 1; i++)
            {
                byteStream.WriteByte(Utils.GetBitfieldByte(torrent_data.piece_status, i));
            }
            netstream.Write(byteStream.ToArray(), 0, size+4);
        }

        public void ReceiveBitField(TFData torrent_data, NetworkStream netstream)
        {
            Console.WriteLine("Todo: Receive bitfield");
        }

        public void SendHandshake(TFData torrent_data, NetworkStream netstream)
        {
            byte[] message = new byte[68];

            message[0] = 19;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("BitTorrent protocol"), 0, message, 1, 19);
            Buffer.BlockCopy(torrent_data.info_hash, 0, message, 28, 20);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(MainProc.peerid), 0, message, 48, 20);

            netstream.Write(message, 0, message.Length);
        }

        public void ReceiveHandshake(TFData torrent_data, NetworkStream netstream)
        {
            byte[] bytes = new byte[68];
            netstream.Read(bytes, 0, 68);

            if (bytes[0] != 19)
            {
                throw new Exception("Invalid handshake, first byte must equal 19");
            }

            if (Encoding.UTF8.GetString(bytes.Skip(1).Take(19).ToArray()) != "BitTorrent protocol")
            {
                throw new Exception("Invalid handshake, protocol must equal \"BitTorrent protocol\"");
            }

            byte[] peer_hash = bytes.Skip(28).Take(20).ToArray();
            if (!peer_hash.SequenceEqual(torrent_data.info_hash))
            {
                throw new Exception("Invalid handshake, peer returned a different info_hash");
            }
        }
    }
}
