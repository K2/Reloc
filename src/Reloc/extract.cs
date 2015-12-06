// Shane.Macaulay @IOActive.com Copyright (C) 2013-2015

//Copyright(C) 2015 Shane Macaulay

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; either version 2
//of the License, or(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

// Shane.Macaulay@IOActive.com (c) copyright 2014,2015 all rights reserved. GNU GPL License

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Diagnostics;
using static System.Console;

namespace Reloc
{
    // Extract compiles a local reloc set that can be used when dumping memory to recover identical files 
    public class Extract
    {
        public static int NewCnt;
        public static int Verbose;
        public static bool OverWrite;

        public string FileName;
        public uint RelocPos;
        public int RelocSize;
        public ulong ImageBase;
        public uint TimeStamp;
        int secOff;
        int secCount;
        bool Is64;

        // Helper that delegates execution
        private static async Task CompileEachFileAsync(string path, string searchPattern, string SaveFolder, SearchOption searchOption, Func<string, string, Task> doAsync)
        {
            // Avoid blocking the caller for the initial enumerate call.
            await Task.Yield();
            
            var sw = Stopwatch.StartNew();

            // really need a simple exception swallowing filesystem walker, enumerations suck with exeptions !
            foreach (string file in Directory.EnumerateFiles(path, searchPattern, searchOption))
            {
                await doAsync?.Invoke(file, SaveFolder);
            }
            
            if(Verbose > 0)
                WriteLine($"processing time: {sw.Elapsed}");
        }
        
        /// Directory Async enumeration
        public static Task ScanDirectoryAsync(string Source, string Dest, string glob = "*", bool Recursive = false)
        {
            if (!Directory.Exists(Source)) {
                WriteLine($"Can not find scan folder {Source} to import PE files from");
                return null;
            } else {
            
            WriteLine($"Scanning folder {Source} and saving relocs into {Dest}.");

            return CompileEachFileAsync(Source, glob, Dest, Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly,
                    (f, g) => new Extract().ScanFile(f, g));
            // this exception handling not working well
                    //.ContinueWith(t => {
                    //    if (Verbose > 0)
                    //        WriteLine($"{t.Exception.Message}. InnerException: {t.Exception.InnerExceptions}", );
            }
        }
        
        /// Perform scan/extract on a single file
        public async Task ScanFile(string name, string saveToFolder)
        {
            if (File.Exists(name))
            {
                FileName = name;
                if (GetDetails())
                {
                    if (RelocPos != 0 && RelocSize != 0)
                    {
                        if (Verbose > 1)
                            WriteLine($"processing [{name}]");

                        var sb = new StringBuilder(Path.GetFileName(name));
                        sb.Append("-");
                        sb.Append(ImageBase.ToString("X"));
                        sb.Append("-");
                        sb.Append(TimeStamp.ToString("X"));
                        sb.Append(".reloc");

                        var outFile = Path.Combine(saveToFolder, sb.ToString());
                        if (File.Exists(outFile) && !OverWrite)
                        {
                            if (Verbose > 0)
                            {
                                WriteLine($"{outFile} exists, skipping due to no over write setting.");
                                return; 
                            }
                        }
                        //var readBuffer = GetBuffAsync().Result;
                        using (FileStream stream = new FileStream(outFile,
                            FileMode.CreateNew, FileAccess.Write, FileShare.None, RelocSize, true))
                                await stream.WriteAsync(GetBuffAsync().Result, 0, RelocSize);

                        NewCnt++;
                        if (Verbose > 0)
                            WriteLine($"extracted {name} relocation data into {outFile} size {RelocSize}");
                        return;
                    }
                }
                else
                    Debug.WriteLine($"Unable to find file: {FileName}");
            }
            return;
        }

        // slim PE 32/64 handling and collect required detials we need for delocation
        // ImageBase, TimeDateStamp, bitness (64/32) and location/size of .reloc section
        public bool GetDetails()
        {
            bool rv = false;
            try {
                using (var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    using (var binReader = new BinaryReader(fs))
                    {
                        if (fs.Length < 0x40)
                            return false;

                        fs.Position = 0x3C;
                        var headerOffset = binReader.ReadUInt32();

                        if (headerOffset > fs.Length - 5)
                            return false;

                        fs.Position = headerOffset;
                        var signature = binReader.ReadUInt32();

                        if (signature != 0x00004550)
                            return false;

                        fs.Position += 2;
                        secCount = binReader.ReadInt16();
                        TimeStamp = binReader.ReadUInt32();
                        fs.Position += 8;
                        secOff = binReader.ReadUInt16();
                        fs.Position += 2;

                        var magic = binReader.ReadInt16();
                        Is64 = magic == 0x20b;
                        
                        fs.Position += 20;
                        // shift down to ABI bitwidth
                        ImageBase = binReader.ReadUInt64() >> 16; 
                        // get to sections
                        fs.Position = headerOffset + (Is64 ? 0x108 : 0xF8);
                        for (int i = 0; i < secCount; i++)
                        {
                            var secName = binReader.ReadBytes(8);
                            var secStr = Encoding.ASCII.GetString(secName);

                            if (Verbose > 2)
                                Write($" section [{secStr}] ");

                            if (secStr.StartsWith(@".reloc", StringComparison.Ordinal))
                            {
                                fs.Position += 8;
                                RelocSize = binReader.ReadInt32();
                                RelocPos = binReader.ReadUInt32();
                            }
                            fs.Position += 0x20;
                        }
                    }
                }
                rv = true;
            }
            catch (Exception ex)
            {
                if (Verbose > 0)
                    WriteLine($"Skipping file [{FileName}] due to error {ex.Message} : {ex.ToString()}.");

            }
            return rv;
        }

        public async Task<byte[]> GetBuffAsync()
        {
            byte[] readBuffer = null;
            var bytesRead = 0;
            try
            {
                using (var fileStream = File.OpenRead(FileName))
                {
                    readBuffer = new Byte[RelocSize];
                    fileStream.Position = RelocPos;
                    bytesRead = await fileStream.ReadAsync(readBuffer, 0, RelocSize).ConfigureAwait(false);
                    return readBuffer;
                }
            }
            catch (Exception)
            {
                return readBuffer;
            }
        }
    }
}