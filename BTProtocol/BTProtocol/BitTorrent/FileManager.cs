﻿using System;
using System.IO;

using System.Collections.Generic;
using BencodeNET.Torrents;
using System.Threading;

namespace BTProtocol
{
    public class FileManager
    {
        // TFile represents the relationship between a piece and its corresponding files
        public struct TFile
        {
            // Index of the files included the in the torrent, 0-indexed (used for file_sizes)
            public int index;
            // Start offset to this TFile
            public long offset;
            public FileStream filestream;
            public TFile(int index, FileStream fs, long offset)
            {
                this.index = index;
                filestream = fs;
                this.offset = offset;
            }
        }

        private const string path = @"../../../TorrentDownloads/";
        private long[] file_sizes = null;
        private readonly Torrent torrent;
        private List<TFile>[] piece_filemap = null;
        private long piece_size { get; }
        public Semaphore tf_lock { get; }

        public FileManager(Torrent torrent_data)
        {
            torrent = torrent_data;
            piece_size = torrent_data.PieceSize;
            tf_lock = new Semaphore(1, 1);
        }

        public void Initialize(FileAccess fileAccess)
        {
            if (piece_filemap != null)
                return;

            int pieces = torrent.Pieces.Length / 20;
            piece_filemap = new List<TFile>[pieces];
            string torrent_folder = path + torrent.DisplayName + "/";
            Directory.CreateDirectory(torrent_folder);
            if (torrent.File != null)
            {
                // Single File torrent
                string torrent_path = torrent_folder + torrent.File.FileName;
                FileStream fs = File.Open(torrent_path, FileMode.OpenOrCreate, fileAccess, FileShare.Read);
                file_sizes = new long[] { torrent.TotalSize };
                for (int i = 0; i < pieces; i++)
                {
                    TFile file = new TFile(0, fs, i * piece_size);
                    piece_filemap[i] = new List<TFile> { file };
                }
            }
            else
            {
                // Multi-file torrent
                int file_count = torrent.Files.Count, i;
                file_sizes = new long[file_count];
                long[] total_offsets = new long[file_count + 1];
                total_offsets[0] = 0;

                FileStream[] filestreams = new FileStream[file_count];
                for (i = 0; i < file_count; i++)
                {
                    filestreams[i] = File.Open(torrent_folder + torrent.Files[i].FileName, FileMode.OpenOrCreate, fileAccess, FileShare.Read);
                    total_offsets[i + 1] = total_offsets[i] + torrent.Files[i].FileSize;
                    file_sizes[i] = torrent.Files[i].FileSize;
                }

                int curr = 0;
                long offset;
                for (i = 0; i < pieces - 1; i++)
                {
                    offset = i * piece_size;
                    piece_filemap[i] = new List<TFile>();
                    TFile file = new TFile(curr, filestreams[curr], Math.Max(0, offset - total_offsets[curr]));
                    while (offset + piece_size >= total_offsets[curr + 1])
                    {
                        piece_filemap[i].Add(file);
                        file = new TFile(curr, filestreams[curr], Math.Max(0, offset - total_offsets[curr]));
                        curr++;
                    }

                    // Add current file to current piece
                    piece_filemap[i].Add(file);
                }

                // Add remaining files as TFiles for last piece
                offset = i * piece_size;
                piece_filemap[i] = new List<TFile>();
                while (curr < file_count)
                {
                    TFile file = new TFile(curr, filestreams[curr], Math.Max(0, offset - total_offsets[curr]));
                    piece_filemap[i].Add(file);
                    curr++;
                }
            }
        }
        
        public void WritePiece(int piece_idx, byte[] data)
        {
            List<TFile> files = piece_filemap[piece_idx];
            int data_offset = 0;
            foreach (TFile curr_file in files)
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

        public byte[] ReadPieceBlock(int piece_idx, int offset, int length)
        {
            // All files associated with this piece
            List<TFile> files = piece_filemap[piece_idx];

            byte[] data = new byte[length];
            int data_offset = 0;
            foreach (TFile curr_file in files)
            {
                long file_size = file_sizes[curr_file.index];
                long file_offset = curr_file.offset;
                if (offset > file_size - curr_file.offset)
                {
                    offset -= (int)(file_size - curr_file.offset);
                    continue;
                }


                int write_bytes = (int)Math.Min(file_size - file_offset - offset, length);
                length -= write_bytes;
                offset = 0;

                FileStream fs = curr_file.filestream;
                fs.Seek(file_offset, SeekOrigin.Begin);
                fs.Read(data, data_offset, write_bytes);
                data_offset += write_bytes;

                if (length <= 0)
                {
                    break;
                }
            }

            return data;
        }

        public long GetPieceSize(int next_piece)
        {
            if ((next_piece == piece_filemap.Length - 1) && (torrent.TotalSize % piece_size != 0))
            {
                return torrent.TotalSize % piece_size;
            }
            return piece_size;
        }

        public void SetReadOnly()
        {
            HashSet<int> read_only_files = new HashSet<int>();
            Dictionary<int, FileStream> filestreams = new Dictionary<int, FileStream>();
            for (int index = 0; index < piece_filemap.Length; index++)
            {
                for (int j = 0; j < piece_filemap[index].Count; j++)
                {
                    TFile file = piece_filemap[index][j];
                    if (!(read_only_files.Contains(file.index)))
                    {
                        file.filestream.Close();
                        read_only_files.Add(file.index);
                        filestreams[file.index] = File.Open(file.filestream.Name, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    file.filestream = filestreams[file.index];
                    piece_filemap[index][j] = file;
                }
            }
        }
    }
}
