using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using static BTProtocol.BitTorrent.MessageType;
using System.IO;

namespace BTProtocol.BitTorrent
{

    sealed internal class DownloadingTask : TorrentTask
    {
        TcpClient client;
        DateTime? last_interested = null;
        int countdown;
        struct PieceData
        {
            public int index;
            public int last_block_index;
            public int final_block_size;
            public byte[] piece_data;
            public int bytes_downloaded;
        }
        PieceData curr_piece;

        protected override private void ExitThread()
        {
            DownloadingThreadManager.thread_pool.Release();
            Console.WriteLine("Exiting Task");
            if (client.Connected)
            {
                torrent_data.connected_peers.Remove((peer.ip, peer.port));
            }
        }

        public DownloadingTask(TFData tfdata, FileManager file_manager)
        {
            torrent_data = tfdata;
            this.file_manager = file_manager;
            this.file_manager.Initialize(FileAccess.ReadWrite);
        }

        public void StartTask()
        {
            /*
             * Downloading Task initialization steps:
             *      - Check whether there remains to be any pieces to be downloaded
             *      - Find an available, unvisted peer to connect to
             *      - Connect to the peer and start downloading, or exit the Task if no peers are avaiable
             */
            // Call Wait to decrement the count of available threads.
            DownloadingThreadManager.thread_pool.Wait();
            // Release the lock on main so it can continue execution.
            DownloadingThreadManager.main_semaphore.Release();

            // Check the TFData to see if there are pieces that stll need to be downloaded. 
            while (!torrent_data.CheckDownloadStatus())
            {
                while (client == null || !client.Connected)
                {
                    Tuple<string, int> peer_addr = FindAvailablePeer();
                    if (peer_addr != null)
                        InitiateConnection(peer_addr);
                    else
                    {
                        ExitThread();
                        return;
                    }
                }

                try
                {
                    SendHandshake();
                    ReceiveHandshakeDownloading();
                    SendBitField();
                    ReceivePackets();
                }
                catch (Exception e)
                {
                    client = null;
                    Console.WriteLine(e.Message);
                }
            }

            ExitThread();
        }

        private Tuple<string, int> FindAvailablePeer()
        {
            // Find an unvisited peer to connect to
            int idx;
            string ip;
            int port;
            do
            {
                idx = Interlocked.Increment(ref torrent_data.peer_list_indx) - 1;
                if (idx >= torrent_data.peer_list.Count)
                {
                    return null;
                }
                ip = torrent_data.peer_list[idx].Item1;
                port = torrent_data.peer_list[idx].Item2;
            } while (torrent_data.connected_peers.Contains((ip, port)));

            return new Tuple<string, int>(ip, port);
        }

        private void InitiateConnection(Tuple<string, int> address)
        {
            string ipaddr = address.Item1;
            int port = address.Item2;
            //Console.WriteLine("Initiating Connection: " + ipaddr + ":" + port);

            client = new TcpClient();
            if (client.ConnectAsync(ipaddr, port).Wait(500))
            {
                Console.WriteLine("Connection Successful: " + ipaddr + ":" + port);
                peer = new Peer(client, torrent_data.piece_status.Length, torrent_data.torrent_name);
                peer.GetStream().ReadTimeout = 30000;
                peer.GetStream().Flush();

                torrent_data.connected_peers.Add((address.Item1, address.Item2));
            }
        }

        private void ReceivePackets()
        {
            //Console.WriteLine("Start Receiving Packets");
            /*
             * Continuously read in new packets from the peer.
             * The proess of reading in packets is as follows:
             *      - Read in 4 bytes to determine the size of the packets. If the size is 0, it is a keep-alive packet
             *      - Read in the full packet to a byte array with size as specified 
             *      - Get the message type of the packet (First 4 bytes)
             *      - Use a case statement to proess the full packet
             *      - Continue readng packets from the peer until the peer closes the connection, or there are no more pieces to download from the peer
            */
            while (true)
            {
                byte[] byte_buffer = new byte[4];
                int packet_size;
                //Console.WriteLine("Trying to read");
                peer.GetStream().Read(byte_buffer, 0, 4);
                packet_size = Utils.ParseInt(byte_buffer);

                if (packet_size > 0)
                {
                    //Console.WriteLine("Packet Size: " + packet_size);
                    byte_buffer = new byte[packet_size];
                    int bytes_read = 0;
                    while (bytes_read < packet_size)
                    {
                        bytes_read += peer.GetStream().Read(byte_buffer, bytes_read, packet_size - bytes_read);
                    }
                    //Console.WriteLine("Bytes Read: " + bytes_read);

                    MessageType packet_type = (MessageType) byte_buffer[0];
                    //Console.WriteLine("Packet Type: " + packet_type.ToString());
                    switch (packet_type)
                    {
                        case Choke:
                            last_interested = DateTime.Now;
                            countdown = 60;
                            break;
                        case Unchoke:
                            SendPieceRequest();
                            break;
                        case Interested:
                            peer.interested = true;
                            SendBitMessageType(Unchoke);
                            break;
                        case NotInterested:
                            peer.interested = false;
                            break;
                        case Have:
                            ReceiveHave(byte_buffer);
                            break;
                        case Bitfield:
                            ReceiveBitField(byte_buffer);
                            break;
                        case Request:
                            ReceiveRequest(byte_buffer);
                            break;
                        case Piece:
                            ReceivePiece(byte_buffer);
                            break;
                        case Cancel:
                            // Not handled by current seeding implementation
                            break;

                        default:
                            throw new Exception("Unknown packet type");
                    }
                }
                if (!(last_interested is null) &&
                     (DateTime.Now.Subtract(last_interested ??= DateTime.Now).Seconds > countdown))
                {
                    client.Close();
                    break;
                }
            }
        }

        private void ReceiveBitField(byte[] byte_buffer)
        {
            SetPeerBitField(byte_buffer);

            // Compare the bitfield
            for (int i = 0; i < torrent_data.piece_status.Length; i++)
            {
                if (peer.bitfield[i])
                {
                    if (torrent_data.piece_status[i] == 0)
                    {
                        peer.high_priority_pieces.Add(i);
                    }
                    else if (torrent_data.piece_status[i] == 2)
                    {
                        peer.low_priority_pieces.Add(i);
                    }
                }
            }

            if (!peer.CheckInterested())
            {
                last_interested = DateTime.Now;
                countdown = 10;
            }
            else
            {
                last_interested = null;
            }

            SendBitMessageType(Interested);
        }

        private void ReceiveHave(byte[] byte_buffer)
        {
            int piece_idx = Utils.ParseInt(byte_buffer, 1);
            peer.bitfield[piece_idx] = true;
            if (torrent_data.piece_status[piece_idx] != 1)
            {
                if (torrent_data.piece_status[piece_idx] == 0)
                {
                    peer.high_priority_pieces.Add(piece_idx);
                }
                else
                {
                    peer.low_priority_pieces.Add(piece_idx);
                }

                // If we were prevously not interested, set ourselves to interested and send out a piece request
                // If we are currently interested, do not send out a peice request, we should be already attempting to download a piece
                if (last_interested != null)
                {
                    last_interested = null;
                    SendPieceRequest();
                }      
            }
        }

        private void SendPieceRequest()
        {
            int next_piece = peer.GetNextPiece(torrent_data);
            file_manager.tf_lock.WaitOne();
            while (next_piece != -1 && !torrent_data.SetPieceStatus(next_piece, 2))
            {
                next_piece = peer.GetNextPiece(torrent_data);
            }
            file_manager.tf_lock.Release();

            if (next_piece != -1)
            {
                curr_piece = new PieceData();
                curr_piece.index = next_piece;
                long piece_size;
                if (next_piece == file_manager.piece_filemap.Length - 1)
                {
                    piece_size = torrent_data.torrent_size % file_manager.piece_size;
                    if (piece_size == 0)
                    {
                        piece_size = file_manager.piece_size;
                    }
                }
                else
                {
                    piece_size = file_manager.piece_size;
                }
                curr_piece.last_block_index = (int)Math.Ceiling(piece_size / (double)Utils.BLOCK_SIZE) - 1;
                curr_piece.final_block_size = (int) piece_size % Utils.BLOCK_SIZE;
                if (curr_piece.final_block_size == 0)
                {
                    curr_piece.final_block_size = Utils.BLOCK_SIZE;
                }
                curr_piece.piece_data = new byte[piece_size];
                curr_piece.bytes_downloaded = 0;
                for (int block = 0; block < curr_piece.last_block_index + 1; block++)
                {
                    SendBlockRequest(block);
                }
            }
            else
            {
                last_interested = DateTime.Now;
                countdown = 10;
            }
        }

        private void SendBlockRequest(int block_index)
        {
            //Console.WriteLine("Requesting (Piece, Block): (" + curr_piece.index + ", " +  block_index + ")");
            MemoryStream byteStream = new MemoryStream();
            byteStream.Write(Utils.Int32ToByteArray(13), 0, 4); // Size
            byteStream.WriteByte(6); // Packet Type
            byteStream.Write(Utils.Int32ToByteArray(curr_piece.index), 0, 4); //Piece Index
            byteStream.Write(Utils.Int32ToByteArray(block_index * Utils.BLOCK_SIZE), 0, 4); // Piece (Block) offset
            int length = Utils.BLOCK_SIZE; // Block Size
            if (block_index == curr_piece.last_block_index)
            {
                length = curr_piece.final_block_size;
            }
            byteStream.Write(Utils.Int32ToByteArray(length), 0, 4);
            peer.GetStream().Write(byteStream.ToArray(), 0, (int)byteStream.Length);
        }

        private void ReceivePiece(byte[] byte_buffer)
        {
            /*
             * Packet Structure: <id=7><index><begin><block>
             * Parse out the following variables:
             *      - Index
             *      - Begin
             *      - Block
             * Pass these variables to the FileManager, which will handle the write operation
             * Send a Piece-Request to the peer, requesting for the next block of the peice
             */

            int piece_idx = Utils.ParseInt(byte_buffer, 1);
            int offset = Utils.ParseInt(byte_buffer, 5);
            Array.Copy(byte_buffer, 9, curr_piece.piece_data, offset, byte_buffer.Length - 9);

            int block_idx = offset / Utils.BLOCK_SIZE;
            if (block_idx == curr_piece.last_block_index)
            {
                curr_piece.bytes_downloaded += curr_piece.final_block_size;
            }
            else
            {
                curr_piece.bytes_downloaded += Utils.BLOCK_SIZE;
            }


            // Once the full piece has been downloaded, verify that the data is correct through a hash comparison
            // If the piece is incorrect, discard all data downloaded and choose a new piece to download
            if (curr_piece.bytes_downloaded == curr_piece.piece_data.Length)
            {
                if (torrent_data.VerifyPiece(piece_idx, curr_piece.piece_data))
                {
                    file_manager.tf_lock.WaitOne();
                    if (torrent_data.SetPieceStatus(piece_idx, 1))
                    {
                        //Console.WriteLine("Piece Downloaded (" + torrent_data.torrent_name + "): " + piece_idx);
                        file_manager.WritePiece(piece_idx, curr_piece.piece_data);
                        torrent_data.bytes_downloaded += (uint)curr_piece.piece_data.Length;
                        Console.WriteLine("Progress: " + ((float) torrent_data.bytes_downloaded / (float) torrent_data.torrent_size));
                        Utils.SerializeTFData(torrent_data);
                    }
                    file_manager.tf_lock.Release();
                }
                else
                {
                    file_manager.tf_lock.WaitOne();
                    torrent_data.SetPieceStatus(piece_idx, 0);
                    file_manager.tf_lock.Release();
                }
                SendPieceRequest();
            }
        }
    }
}
