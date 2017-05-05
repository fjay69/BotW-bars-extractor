using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BotW_bars_extractor
{
    class Program
    {
        static BinaryReader bread;
        static BinaryWriter bwrite;
        struct endFiles
        {
            public string name;
            public uint offset;
        }
        static void Main(string[] args)
        {
            string output_bars = "";
            if (args.Length == 1)
                output_bars = args[0];
            else {
                System.Console.WriteLine("Usage: bars_extract <output .bars file>");
                return;
            }
            FileInfo fi = new FileInfo(output_bars);
            string input_folder = fi.DirectoryName + "\\" + fi.Name + "_extracted\\";
            uint full_size = (uint)fi.Length;
            bread = new BinaryReader(File.Open(output_bars, FileMode.Open));
            Directory.CreateDirectory(input_folder);
                        
            //first 4 bytes form the word BARS(42 41 52 53)
            string bars = new string(bread.ReadChars(4));
            if (bars != "BARS") {
                System.Console.WriteLine("Not a valid .bars file");
                return;
            }
            //starting byte 0x4 is the lenght of the file, 4 bytes long. maximum file size would be 16777215 bytes.
            uint file_size = readInt32be(bread);
            //next 4 bytes seem to be always the same(FE FF 01 01).
            bread.ReadChars(4);

            List<uint> entries = collect_entries();
            //after that starts the data itself.
            List<endFiles> filesToCreate = new List<endFiles>();

            for (int i = 0; i < entries.Count; i++)
            {                
                string file_name = "";
                if (i < entries.Count/2) {
                    bread.BaseStream.Position = entries[i];
                    //AMTA
                    bread.ReadChars(4);
                    //24 unknown bytes
                    bread.ReadChars(24);
                    //DATA
                    string chunk_name = new string(bread.ReadChars(4));
                    uint chunk_size = readInt32be(bread);
                    bread.BaseStream.Position += chunk_size;
                    //MARK
                    chunk_name = new string(bread.ReadChars(4));
                    chunk_size = readInt32be(bread);
                    bread.BaseStream.Position += chunk_size;
                    //EXT_
                    chunk_name = new string(bread.ReadChars(4));
                    chunk_size = readInt32be(bread);
                    bread.BaseStream.Position += chunk_size;
                    //STRG
                    //byte 0xA7 of the header contains the lenght of the file name.
                    chunk_name = new string(bread.ReadChars(4));
                    chunk_size = readInt32be(bread);
                    byte[] bytes = new byte[chunk_size-1];
                    for (uint j = 0; j < chunk_size-1; j++)
                        bytes[j] = bread.ReadByte();                    
                    file_name = System.Text.Encoding.UTF8.GetString(bytes)+ ".bfwav";

                    endFiles f1 = new endFiles();
                    f1.name = file_name;
                    f1.offset = entries[i+ entries.Count / 2];
                    filesToCreate.Add(f1);
                } else {
                    //if the.bars doesn't have bfwav's inside then that second value is null(FF FF FF FF).
                    if (entries[i] == 0xffffffff) continue;
                    bread.BaseStream.Position = entries[i];
                    bwrite = new BinaryWriter(File.Open(input_folder + filesToCreate[i- entries.Count / 2].name, FileMode.Create));

                    bread.ReadBytes(4);//FWAV
                    bread.ReadBytes(2);//Byte order (feff)
                    bread.ReadBytes(2);//Header size(0040)
                    bread.ReadBytes(4);//Unknown
                    uint fwav_size = readInt32be(bread);
                    bread.BaseStream.Position -= 16;
                    for (uint ik = 0; ik < fwav_size; ik++)
                        bwrite.Write(bread.ReadByte());
                }
            }
        }

        static uint readInt32be(BinaryReader bread)
        {
            byte[] a32 = new byte[4];
            a32 = bread.ReadBytes(4);
            Array.Reverse(a32);
            return BitConverter.ToUInt32(a32, 0);
        }        

        static List<uint> collect_entries()
        {
            List<uint> entries = new List<uint>();
            List<uint> filess = new List<uint>();
            //next 4 bytes is the total of files packed inside.
            uint files = readInt32be(bread);
            //next bytes really can't make heads or tails of what it is, but could be a 4 bytes identifier of the files packed inside.
            //can't be a crc because comparing the japanese and usa NV_Custom_NoActor.bars shows that those values are identical.
            bread.BaseStream.Position += files*4;
            //next bytes are the content index, 2 values 4 bytes long each,                        
            for (uint i = 0; i < files; i++)
            {
                //first value is the offset to (what seems to be) the header and name of the file.
                uint header = readInt32be(bread);
                entries.Add(header);
                //the second value is the offset to the file itself.
                uint offset = readInt32be(bread);
                filess.Add(offset);
            }
            entries = entries.Concat(filess).ToList();
            return entries;
        }
    }
}
