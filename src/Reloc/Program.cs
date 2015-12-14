// Shane.Macaulay @IOActive.com Copyright (C) 2013-2015

//Copyright(C) 2015 Shane Macaulay

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

// Shane.Macaulay@IOActive.com (c) copyright 2014,2015 all rights reserved. GNU GPL License


//
//  TODO:   Local mode.  
//              * Pick up and store relocations into a local DB
//
//          Add hash query.
//              * Current query is for test/slow.  Primary queries in the fat windows client is able to
//                perform many 10's of thousands of checks per second.
//
//          Intergrate with InVtero.net for perfect dumps. (i.e. your memory dump will match your disk files percisely).
//
//          Plugin to 3rd party tools, support volatility/rekal memory dumpers
//
//          Error log submissions
//
//          Evaluate exposing the byte array for integration with other tools not requiring a disk file



using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using static System.Console;

namespace Reloc
{
    public class Program
    {
        /// <summary>
        /// probably just get rid of this a better version is in Extract
        /// </summary>
        /// <param name="FilePath"></param>
        /// <returns></returns>
        public static uint PETimeDateStamp(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    using (var binReader = new BinaryReader(fs))
                    {
                        fs.Position = 0x3C;
                        var headerOffset = binReader.ReadUInt32();

                        if (headerOffset > fs.Length - 5)
                            return 0;

                        fs.Position = headerOffset;
                        var signature = binReader.ReadUInt32();

                        if (signature != 0x00004550)
                            return 0;

                        fs.Position += 4;

                        var time = binReader.ReadUInt32();
                        WriteLine($"Calculated time {time:X8}");
                        return time;
                    }
                }
            }
            return 0;
        }

        static bool PrintHelp(string[] args)
        {
            WriteLine($"{Environment.NewLine} Commands: Reloc & Extract");
            WriteLine($"\tDeLocate DumpedBinaryPath .reloc-file InMemoryBaseAddrInHex Save.File.Exe Is64");
            WriteLine($"\tDeLocate dumped.msctf.dll msctf.dll-10000000-564D1E7B.reloc 77740000 delocated.msctf.dll False");
            WriteLine($"\tExtract will compile a local directory for reloc's from your own files");
            WriteLine($"\tReloc will download from our hosted server");
            WriteLine($"\tReloc Is64 Region TimeDateStamp");
            WriteLine($"\te.g. running the default Reloc command ===>>>[dnx Reloc True ntdll 51DA4B7D]<<<===");
            WriteLine($"\twill result in the 64bit 7zip compressed reloc data to be downloaded to NTDLL.DLL-78E50000-51DA4B7D.reloc.7z");
            WriteLine($"\tBy using relocation data during a memory dump extraction, an exact match may be calculated from disk-code<->memory-code.{ Environment.NewLine}");
            WriteLine($"\tuser provided {args.Length + 1} arguments (only specify 3), interpreted as;");
            WriteLine($"\tIs64[{(args.Length >= 1 ? args[1] : String.Empty)}] Region[{(args.Length >= 2 ? args[2] : String.Empty)}] TimeDateStamp[{(args.Length >= 3 ? args[3] : String.Empty)}] ...");
            return false;
        }

        static Net InterWeb;

        /// <summary>
        /// Entrypoint, command line handler.
        /// </summary>
        /// <param name="args"></param>
        public void Main(string[] args)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "delocate":
                    var dl = new DeLocate();
                    dl.DeLocateFile(args[1], args[2], ulong.Parse(args[3], NumberStyles.HexNumber), args[4], bool.Parse(args[5]), true, true).Wait();
                    break;
                case "reloc":
                    Reloc(args);
                    break;
                case "extract":
                    Extract.Verbose = 1;
                    Extract.ScanDirectoryAsync(args[1], args[2]).Wait();
                    WriteLine($"Compiled {Extract.NewCnt} new .reloc data fragments");
                    break;
                default:
                    return;

            }

        }

        /// <summary>
        /// Network download from IOActive server for hosted reloc's
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        bool Reloc(string[] args)
        {
            if (args.Length != 4)
            {
                var done = Task.Run(() =>
                {
                    return PrintHelp(args);
                });
                return done.Result;
            }

            var Is64 = false;
            var time = uint.MinValue;
            var Region = string.Empty;
            var dt = DateTime.MinValue;
            var KnownAsName = string.Empty;
            var OrigLoadAddress = ulong.MinValue;

            var TimeStr = args[3];
            Region = args[2];

            if (!bool.TryParse(args[1], out Is64))
            {
                WriteLine($"Error parsing a booliean value (True or False) from [{args[1]}], unable to continue.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Region) || Region.Contains(Path.GetInvalidFileNameChars().ToString()))
            {
                WriteLine($"Must provide a value for the DLL/EXE name to search for (region), provided value [{Region}], unable to continue.");
                return false;
            }

            if (!uint.TryParse(TimeStr, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out time)
                &&
                !uint.TryParse(TimeStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out time)
                &&
                !DateTime.TryParse(TimeStr, out dt)
                )
            {
                WriteLine($"Error parsing a TimeDateStamp value (numeric (hex allowed) or in text form e.g. (8/18/2010 1:30:30 PM - 1/1/2010 8:00:15 AM = 229.05:30:15) from [{TimeStr}], unable to continue.");
                return false;
            }
            // if the argument was not a number or string value for date
            // maybe it's a filename to use as a reference? ;)??
            if (dt == DateTime.MinValue && time == uint.MinValue)
                time = PETimeDateStamp(TimeStr);

            // The FinalFileName is only known after the server responds with additional metadata
            var DestName = $"{Region}-?####?-{time:X}.reloc.7z";

            WriteLine($"Contacting, dest file [{DestName}]: 64bit:{Is64}, Region(dll):{Region}, TimeDateStamp:{time:X}.");

            InterWeb = new Net(@"http://blockwatch.ioactive.com:8888/");
            InterWeb.UserName = "demo@ioactive.com";
            InterWeb.PassWord = "demo";

            //
            // Sending the "Online" packet dosent really matter since the cred's are sent always.
            // It's more of an application ping/test that you're good to go.
            //
            // Aside from the downloaded .reloc file.  You will also get the pre ferred load address
            // which can sometimes be missing or altered by due to loader artifacts ? :(
            //

            var FinalFileName =
                Task.Factory.StartNew(() => InterWeb.Online())
                .ContinueWith((isOn) =>
                {
                    Task<byte[]> data = null;
                    if (isOn.Result)
                        data = Task.Factory.StartNew(() => InterWeb.NetRelocCheck(Region, time, Is64, ref OrigLoadAddress, ref KnownAsName));
                    return data;

                }).Unwrap().ContinueWith((bytez) =>
                {
                    var FinalName = $"{KnownAsName}-{OrigLoadAddress:X}-{time:X}.reloc.7z";
                    File.WriteAllBytes(FinalName, bytez.Result);
                    return FinalName;
                });

            if (OrigLoadAddress == ulong.MaxValue)
                Write("An error reported from server: ");

            if (!File.Exists(FinalFileName.Result))
            {
                WriteLine("No .reloc available, request an import of the reloc data you need, we will expand the table based on feedback.");
                return false;
            }

            WriteLine($"Downloaded to {FinalFileName.Result}, size {new FileInfo(FinalFileName.Result).Length}.");
            return true;


#if FALSE
            var LC = new LoginCredsText() { username = InterWeb.UserName, password = InterWeb.PassWord };
            WriteLine("test1...");
            IChannelFactory<IRequestChannel> factory = new BasicHttpBinding().BuildChannelFactory<IRequestChannel>(new BindingParameterCollection());
            factory.Open();
            IRequestChannel channel = factory.CreateChannel(new EndpointAddress("http://blockwatch.ioactive.com:8888/Buffer/Text/wsHttp"));
            channel.Open();
            Message requestmessage = Message.CreateMessage(MessageVersion.Soap11, "http://tempuri.org/IElmerBuffer/Online", LC, new DataContractSerializer(LC.GetType()));
            //send message
            Message replymessage = channel.Request(requestmessage);
            WriteLine("Reply message received");
            WriteLine("Reply action: {0}", replymessage.Headers.Action);
            string data = replymessage.GetBody<string>();
            WriteLine("Reply content: {0}", data);
            //Step5: don't forget to close the message
            requestmessage.Close();
            replymessage.Close();
            //don't forget to close the channel
            channel.Close();
            //don't forget to close the factory
            factory.Close();
#endif
        }
    }
}
