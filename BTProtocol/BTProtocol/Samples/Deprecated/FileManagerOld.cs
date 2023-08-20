using System;
using System.IO;

using System.Collections.Generic;
using BencodeNET.Torrents;

namespace BTProtocol
{
    // Obituary: This FileManger was designed to write to files 1 block at a time. Design decisions were made in that it makes more sense to write 1 whole piece at a time after the peice has been verified to be correct. 
    public class FileManagerOld
    {
        // TFile represents the relationship between a piece and its corresponding files
        public struct TFile
        {
            public int index;
            public FileStream filestream;
            public long offset;

            public TFile(int index, FileStream fs, long offset)
            {
                this.index = index;
                filestream = fs;
                this.offset = offset;
            }
        }

        private const string path = @"../../TorrentDownloads/";
        private long[] file_sizes = null;
        public List<TFile>[] piece_filemap = null;
        Torrent torrent;
        public long piece_size { get; }

        public FileManagerOld(Torrent torrent_data)
        {
            torrent = torrent_data;
            piece_size = torrent_data.PieceSize;
        }

        public void Initialize()
        {
            if (piece_filemap != null)
                return;

            int pieces = torrent.Pieces.Length / 20;
            piece_filemap = new List<TFile>[pieces];
            if (torrent.File != null)
            {
                TFile file;
                string torrent_path = path + torrent.File.FileName;
                FileStream fs = File.OpenWrite(torrent_path);
                file_sizes = new long[1] { torrent.TotalSize };
                for (int i = 0; i < pieces; i++)
                {
                    file = new TFile(0, fs, i * piece_size);
                    piece_filemap[i] = new List<TFile> { file };
                }
            }
            else
            {
                int file_count = torrent.Files.Count;
                FileStream[] filestreams = new FileStream[file_count];
                long[] total_offsets = new long[file_count + 1];
                file_sizes = new long[file_count];
                total_offsets[0] = 0;
                for (int i = 0; i < file_count; i++)
                {
                    FileStream fs = File.OpenWrite(path + torrent.Files[i].FileName);
                    filestreams[i] = fs;
                    total_offsets[i+1] = total_offsets[i] + torrent.Files[i].FileSize;
                    file_sizes[i] = torrent.Files[i].FileSize;
                }

                int curr = 0;
                for (int i = 0; i < pieces; i++)
                {
                    long offset = i * piece_size;
                    piece_filemap[i] = new List<TFile>();

                    while (curr < file_count && offset + piece_size > total_offsets[curr + 1])
                    {
                        TFile file = new TFile(curr, filestreams[curr], Math.Max(0, offset - total_offsets[curr]));
                        piece_filemap[i].Add(file);
                        curr++;
                    }

                    if (i != pieces - 1)
                    {
                        TFile file = new TFile(curr, filestreams[curr], Math.Max(0, offset - total_offsets[curr]));
                        piece_filemap[i].Add(file);
                    }
                }
            }
        }

        // Figure out the files to write to from the piece_idx
        private List<TFile> GetFiles(int piece_idx, long offset, int length)
        {
            HashSet<int> file_indices = new HashSet<int>();
            long block_start = piece_size * piece_idx + offset;
            long block_end = piece_size * piece_idx + offset + length;
            long curr_offset = 0;
            int curr = 0;
            while (curr_offset + file_sizes[curr] < block_start)
            {
                curr_offset += file_sizes[curr];
                curr += 1;
            }
            do
            {
                curr_offset += file_sizes[curr];
                file_indices.Add(curr);
                curr += 1;
            } while (curr < file_sizes.Length && curr_offset < block_end);
          

            List<TFile> files = new List<TFile>();
            foreach (TFile tfile in piece_filemap[piece_idx])
            {
                if (file_indices.Contains(tfile.index))
                {
                    files.Add(tfile);
                }
            }

            return files;
        }

        public void WriteBlockPiece(int piece_idx, int block_offset, byte[] data)
        {
            List<TFile> curr_files = GetFiles(piece_idx, block_offset, data.Length);
            int data_offset = 0;
            foreach (TFile curr_file in curr_files)
            {
                FileStream fs = curr_file.filestream;
                long file_offset = curr_file.offset + block_offset;
                int write_bytes = (int) Math.Min(file_sizes[curr_file.index] - file_offset, data.Length - data_offset);
                fs.Seek(file_offset, SeekOrigin.Begin);
                fs.Write(data, data_offset, write_bytes);
                data_offset += write_bytes;
                block_offset = 0;
                fs.Flush();
            }
        }

        /*
        private void ReceivePiece(byte[] byte_buffer)
        {
            *//*
             * Packet Structure: <id=7><index><begin><block>
             * Parse out the following variables:
             *      - Index
             *      - Begin
             *      - Block
             * Pass these variables to the FileManager, which will handle the write operation
             * Send a Piece-Request to the peer, requesting for the next block of the peice
             *//*

            int piece_idx = Utils.ParseInt(byte_buffer, 1);
            int offset = Utils.ParseInt(byte_buffer, 5);
            byte[] block_data = new byte[byte_buffer.Length - 5];
            Array.Copy(byte_buffer, 5, block_data, 0, block_data.Length);

            // Call FileManager to write the block out to the file
            file_manager.WriteBlockPiece(piece_idx, offset, block_data);

            // Send out the next piece request
            int block_idx = offset / Utils.BLOCK_SIZE;
            if (block_idx == curr_piece.last_block_index)
            {
                // Todo: Confirm that the full piece was downloaded correctly by doing a Hash comparison
                // If the piece finished downloading successfully, choose a new peice to download
                SendPieceRequest();
            }
            else
            {
                SendBlockRequest(block_idx + 1);
            }
        }
        */
    }
}
