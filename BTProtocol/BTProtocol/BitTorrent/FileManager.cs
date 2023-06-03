using System;
using System.IO;

using System.Collections.Generic;
using BencodeNET.Torrents;

namespace BTProtocol
{
    public class FileManager
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

        public FileManager(Torrent torrent_data)
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
            string torrent_folder = path + torrent.DisplayName + "/";
            Directory.CreateDirectory(torrent_folder);
            if (torrent.File != null)
            {
                TFile file;
                string torrent_path = torrent_folder + torrent.File.FileName;
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
                    FileStream fs = File.OpenWrite(torrent_folder + torrent.Files[i].FileName);
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

        public void WritePiece(int piece_idx, byte[] data)
        {
            List<TFile> curr_files = piece_filemap[piece_idx];
            int data_offset = 0;
            foreach (TFile curr_file in curr_files)
            {
                FileStream fs = curr_file.filestream;
                long file_offset = curr_file.offset;
                int write_bytes = (int)Math.Min(file_sizes[curr_file.index] - file_offset, data.Length - data_offset);
                fs.Seek(file_offset, SeekOrigin.Begin);
                fs.Write(data, data_offset, write_bytes);
                data_offset += write_bytes;
                fs.Flush();
            }
        }
    }
}
