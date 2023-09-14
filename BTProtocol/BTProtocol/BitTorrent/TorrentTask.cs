using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;

using static BTProtocol.BitTorrent.Utils;
using static BTProtocol.BitTorrent.MessageType;

namespace BTProtocol.BitTorrent
{
    public abstract class TorrentTask
    {
        protected TFData torrent_data;
        protected FileManager file_manager;
        protected Peer peer;
        protected DateTime? last_interested = null;
        protected int countdown;

        protected abstract private void ExitThread();

        // msg_ids: choke-0, unchoke-1, interested-2, not interested-3
        public void SendBitMessageType(MessageType msg_id)
        {
            MemoryStream byteStream = new MemoryStream();
            byteStream.Write(Int32ToByteArray(1), 0, 4);
            byteStream.WriteByte((byte) msg_id);
            peer.GetStream().Write(byteStream.ToArray(), 0, (int) byteStream.Length);
        }

        public void SendBitField()
        {
            MemoryStream byteStream = new MemoryStream();
            int size = (int) Math.Ceiling(torrent_data.piece_status.Length / 8d) + 1;
            byteStream.Write(Int32ToByteArray(size), 0, 4);
            byteStream.WriteByte((byte) Bitfield);
            for (int i = 0; i < size - 1; i++)
            {
                byteStream.WriteByte(GetBitfieldByte(torrent_data.piece_status, i));
            }
            peer.GetStream().Write(byteStream.ToArray(), 0, size+4);
        }

        public void SetPeerBitField(byte[] byte_buffer)
        {
            Array.Reverse(byte_buffer);
            BitArray bits = new BitArray(byte_buffer);
            int bitfield_length = peer.bitfield.Length;
            if (bitfield_length % 8 != 0)
            {
                bitfield_length += 8 - (bitfield_length % 8);
            }

            for (int i = 0; i < bitfield_length; i++)
            {
                if (bits.Get(bitfield_length - i - 1))
                {
                    peer.bitfield[i] = true;
                }
            }
        }

        public void SendHandshake()
        {
            byte[] message = new byte[68];

            message[0] = 19;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("BitTorrent protocol"), 0, message, 1, 19);
            Buffer.BlockCopy(torrent_data.info_hash, 0, message, 28, 20);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(MainProc.peerid), 0, message, 48, 20);

            peer.GetStream().Write(message, 0, message.Length);
        }

        public void ReceiveHandshakeDownloading()
        {
            byte[] bytes = new byte[68];
            peer.GetStream().Read(bytes, 0, 68);

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

        public string ReceiveHandshakeSeeding()
        {
            byte[] bytes = new byte[68];
            peer.GetStream().Read(bytes, 0, 68);

            if (bytes[0] != 19)
            {
                throw new Exception("Invalid handshake, first byte must equal 19");
            }

            if (Encoding.UTF8.GetString(bytes.Skip(1).Take(19).ToArray()) != "BitTorrent protocol")
            {
                throw new Exception("Invalid handshake, protocol must equal \"BitTorrent protocol\"");
            }

            byte[] peer_hash = bytes.Skip(28).Take(20).ToArray();

            // Check that the peer_hash matches the info_hash of one of our torrents
            foreach (TFData tfdata in MainProc.torrent_file_dict.Values)
            {
                if (peer_hash.SequenceEqual(tfdata.info_hash))
                {
                    return tfdata.torrent_name;
                }
            }

            throw new Exception("Invalid handshake, peer returned a different info_hash");
        }

        public void ReceiveRequest(byte[] byte_buffer)
        {
            // Format of byte_buffer is: MessageType(6 - Request) + Piece Index(4 bytes) + Piece Offset (4 bytes) + Length (4 bytes) 
            int piece_index = ParseInt(byte_buffer, 1);
            int piece_offset = ParseInt(byte_buffer, 5);
            int block_length = ParseInt(byte_buffer, 9);

            // Check that we have this peice fully downloaded
            if (torrent_data.piece_status[piece_index] != 1)
            {
                return;
            }

            file_manager.tf_lock.WaitOne();
            byte[] data = file_manager.ReadPieceBlock(piece_index, piece_offset, block_length);
            file_manager.tf_lock.Release();

            //Packet Structure: Size(4 bytes) + MessageType(7 - Piece) + Piece Index(4 bytes) + Piece Offset (4 bytes) + Data
            MemoryStream byteStream = new MemoryStream();
            byteStream.Write(Int32ToByteArray(9 + data.Length), 0, 4);
            byteStream.WriteByte(7);
            byteStream.Write(Int32ToByteArray(piece_index), 0, 4);
            byteStream.Write(Int32ToByteArray(piece_offset), 0, 4);
            byteStream.Write(data, 0, data.Length);

            byte[] message = byteStream.ToArray();
            peer.GetStream().Write(message, 0, message.Length);
        }
    }
}
