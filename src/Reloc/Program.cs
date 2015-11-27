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
    /// <summary>
    /// Entrypoint
    /// </summary>
    public class Program
    {
        public static uint PETimeDateStamp(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    var binReader = new BinaryReader(fs);

                    fs.Position = 0x3C;
                    UInt32 headerOffset = binReader.ReadUInt32();

                    if (headerOffset > fs.Length - 5)
                        return 0;

                    fs.Position = headerOffset;
                    UInt32 signature = binReader.ReadUInt32();

                    if (signature != 0x00004550)
                        return 0;

                    fs.Position += 4;

                    var time = binReader.ReadUInt32();
                    WriteLine($"Calculated time {time:X8}");
                    return time;
                }
            }
            return 0;
        }

        static Net InterWeb;
        public async Task Main(string[] args)
        {
            if (args.Length != 3)
            {
                var done = await Task.Run(() =>
                {
                    WriteLine($"{Environment.NewLine} Commands: [Reloc] Is64 Region TimeDateStamp");
                    WriteLine($"\te.g. running the default Reloc command [dnx run True ntdll 51DA4B7D]");
                    WriteLine($"\twill result in the 64bit 7zip compressed reloc data to be downloaded to NTDLL.DLL-78E50000-51DA4B7D.reloc.7z");
                    WriteLine($"\tBy using relocation data during a memory dump extraction, an exact match may be calculated from disk-code<->memory-code.{ Environment.NewLine}");
                    WriteLine($"\tuser provided {args.Length + 1} arguments (only specify 3), interpreted as;");
                    WriteLine($"\tIs64[{(args.Length >= 1 ? args[0] : String.Empty)}] Region[{(args.Length >= 2 ? args[1] : String.Empty)}] TimeDateStamp[{(args.Length >= 3 ? args[2] : String.Empty)}] ...");
                    return false;
                });
                return;
            }

            var Is64 = false;
            var time = uint.MinValue;
            var Region = string.Empty;
            var dt = DateTime.MinValue;
            var KnownAsName = string.Empty;
            var OrigLoadAddress = ulong.MinValue;

            if (!bool.TryParse(args[0], out Is64))
            {
                WriteLine($"Error parsing a booliean value (True or False) from [{args[0]}], unable to continue.");
                return;
            }

            Region = args[1];
            if (string.IsNullOrWhiteSpace(Region) || Region.Contains(Path.GetInvalidFileNameChars().ToString()))
            {
                WriteLine($"Must provide a value for the DLL/EXE name to search for (region), provided value [{args[1]}], unable to continue.");
                return;
            }

            if (!uint.TryParse(args[2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out time)
                &&
                !uint.TryParse(args[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out time)
                &&
                !DateTime.TryParse(args[2], out dt)
                )
            {
                WriteLine($"Error parsing a TimeDateStamp value (numeric (hex allowed) or in text form e.g. (8/18/2010 1:30:30 PM - 1/1/2010 8:00:15 AM = 229.05:30:15) from [{args[2]}], unable to continue.");
                return;
            }
            // if the argument was not a number or string value for date
            // maybe it's a filename to use as a reference? ;)??
            if (dt == DateTime.MinValue && time == uint.MinValue)
                time = PETimeDateStamp(args[2]);

            // The FinalFileName is only known after the server responds with additional metadata
            var DestName = $"{Region}-?####?-{time:X}.reloc.7z";

            WriteLine($"Contacting, dest file [{DestName}]: 64bit:{Is64}, Region(dll):{Region}, TimeDateStamp:{time:X}.");

            InterWeb = new Net("http://blockwatch.ioactive.com:8888/");
            InterWeb.UserName = "demo@ioactive.com";
            InterWeb.PassWord = "demo";

            //
            // Sending the "Online" packet dosent really matter since the cred's are sent always.
            // It's more of an application ping/test that you're good to go.
            //
            // Aside from the downloaded .reloc file.  You will also get the preferred load address
            // which can sometimes be missing or altered by due to loader artifacts ? :(
            //

            var FinalFileName =
                await Task.Factory.StartNew(() => InterWeb.Online())
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

            if (File.Exists(FinalFileName))
                WriteLine($"Downloaded to {FinalFileName}, size {new FileInfo(FinalFileName).Length}.");
            else 
                WriteLine("No .reloc available, request an import of the reloc data you need, we will expand the table based on feedback.");

            return;
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
