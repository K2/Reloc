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


using SO.Elmer.Client;
using SO.Elmer.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Xml.Linq;

namespace Reloc
{
    /// <summary>
    /// Hand built service proxy
    /// </summary>
    class NetBufferProxy : ClientBase<IBufferedText>, IBufferedText
    {
        public NetBufferProxy(Binding binding, string address) : base(binding, new EndpointAddress(address)) { }
        public LoginReplyText Online(LoginCredsText lc) { return Channel.Online(lc); }
        public void Offline(LoginCredsText lc) { Channel.Offline(lc); }
        public void ErrorSubmit(ErrorLogText log) { Channel.ErrorSubmit(log); }
        public QueryReply Query(Query input) { return Channel.Query(input); }
        public NetRelocTextReply NetRelocCheck(NetRelocTextRequest sm) { return Channel.NetRelocCheck(sm); }
    }
    /// <summary>
    /// Net is meant to serve as a glue class that abstracts away the ServiceModel includes/references.
    /// This should afford you late binding and faster load times.
    /// </summary>
    public class Net : INet
    {
        public ICollection<string> logs { get; set; }
        const string BufferedService = @"Buffer/Text/wsHttp";
        const string StreamedService = @"Stream/Text/wsHttp";

        public ClientMessageInspectorData Data { get; set; }
        public string UserName { get; set; }
        public string PassWord { get; set; }

        public delegate void OnRecieveData(object sender, EventArgs e);
        public delegate void OnSentData(object sender, EventArgs e);

        LoginCredsText LC;
        NetBufferProxy netBufferProxy;

        public Net(string server, string user, string pass)
            : this(server)
        {
            PassWord = pass;
            UserName = user;

            LC = GetLC();
        }

        public Net(string server)
        {
            if (server.StartsWith("http://"))
            {
                netBufferProxy = new NetBufferProxy(
                    new BasicHttpBinding()
                    {
                        TextEncoding = Encoding.UTF8,
                        MaxBufferSize = 0x1000000,
                        MaxReceivedMessageSize = 0x1000000,
                        ReceiveTimeout = TimeSpan.MaxValue,
                        CloseTimeout = TimeSpan.MaxValue,
                        OpenTimeout = TimeSpan.MaxValue,
                        SendTimeout = TimeSpan.MaxValue,
                        TransferMode = TransferMode.Buffered
                    },
                    server + BufferedService
                );
            }
            else if (server.StartsWith("https://"))
            {
                netBufferProxy = new NetBufferProxy(
                    new BasicHttpBinding(BasicHttpSecurityMode.Transport)
                    {
                        TextEncoding = Encoding.UTF8,
                        MaxBufferSize = 0x1000000,
                        MaxReceivedMessageSize = 0x1000000,
                        ReceiveTimeout = TimeSpan.MaxValue,
                        CloseTimeout = TimeSpan.MaxValue,
                        OpenTimeout = TimeSpan.MaxValue,
                        SendTimeout = TimeSpan.MaxValue,
                        TransferMode = TransferMode.Buffered
                    },
                    server + BufferedService
                );
            }

#if DNXCORE50
            // maintained as a reference / handy way to tweak serialization problems
            //netBufferProxy.Endpoint.EndpointBehaviors.Add(new ClientMessagePropertyBehavior());
            //Data = new ClientMessageInspectorData();
            //netBufferProxy.Endpoint.EndpointBehaviors.Add(new ClientMessageInspectorBehavior(Data));
#endif
            netBufferProxy.ChannelFactory.Open();
            
        }

        LoginCredsText GetLC()
        {
            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(PassWord))
                throw new ArgumentException("UserName or PassWord not set.");

            if (LC == null) LC = new LoginCredsText() { username = UserName, password = PassWord };
            return LC;
        }

        ////////////////////////////////////////////////////// Buffered calls //////////////////////////////////////////////////////
        public bool Online()
        {
            return netBufferProxy.Online(GetLC()).Succeeded;
        }
        public void Offline()
        {
            netBufferProxy.Offline(GetLC());
        }
        public void SendErrorLogs(string file)
        {
            if (!File.Exists(file)) return;

            uint idv = uint.Parse(file.Substring(file.LastIndexOf('-') + 1, file.LastIndexOf('.') - file.LastIndexOf('-') - 1));
            if (idv != 0)
                netBufferProxy.ErrorSubmit(new ErrorLogText(GetLC()) { message = File.ReadAllText(file), id = idv });
        }
        public uint Query(byte[] Hash)
        {
            var reply = netBufferProxy.Query(new Query(GetLC()) { hash = Hash });
            return reply.rv;
        }


        public byte[] NetRelocCheck(string regionName, uint timeDateStamp, bool is64, ref ulong PrefAddr, ref string KnownAsName)
        {
            var reply = netBufferProxy.NetRelocCheck(new NetRelocTextRequest(GetLC()) { RegionName = regionName, TimeDateStamp = timeDateStamp, Is64bit = is64 });

            //Console.WriteLine($"recieved data reply Preferred Address: 0x{reply.FileImageBase:X8}, size recieved: 0x{reply.NetRelocContent.Length:X8}");

            if (reply.Succeeded)
            {
                KnownAsName = reply.KnownAs;
                PrefAddr = reply.FileImageBase;
            }
            else
                PrefAddr = ulong.MaxValue;

            return reply.NetRelocContent;
        }
        ////////////////////////////////////////////////////// Buffered calls //////////////////////////////////////////////////////

        /// <summary>
        /// The behavior's below are not currently used but I found handy to tweak any serialization for compat.
        /// </summary>

        ////////////////////////////////////////////////////// UNUSED UNUSED  //////////////////////////////////////////////////////
        ////////////////////////////////////////////////////// UNUSED UNUSED  //////////////////////////////////////////////////////
        ////////////////////////////////////////////////////// UNUSED UNUSED  //////////////////////////////////////////////////////

        public class ClientMessageInspector : IClientMessageInspector
        {
            private ClientMessageInspectorData _data;
            public ClientMessageInspector(ClientMessageInspectorData data)
            {
                _data = data;
            }

            public void AfterReceiveReply(ref Message reply, object correlationState)
            {
                _data.AfterReceiveReplyCalled = true;
                _data.Reply = reply;
            }

            public object BeforeSendRequest(ref Message request, IClientChannel channel)
            {
                //
                // coreclr WCF was giving me problems interfacing with myserver due to the serialization not honoring
                // [MessageHeader].  This causes a lot of problems if your using streaming since you can only have
                // a single body member (the stream).  
                //
                // I wound up just hosting an alternative interface on the server "*Text" since MTOM MessageEncoding 
                // was not working at the time.
                // 
                // I am leaving this in (not hooked up) as an example for reference ;) how to hijack into the XML Header
                // without manually emitting the entire XML blob with a custom binder/*
                //

                // capture the Message
                var buffer = request.CreateBufferedCopy(int.MaxValue);

                // state variable
                _data.BeforeSendRequestCalled = true;
                // setup return value (request is destroyed when we captured above)
                request = _data.Request = buffer.CreateMessage();
                _data.Channel = channel;

                // buffer.CreateMessage(); is our Message Factory now
                var m3 = buffer.CreateMessage();

                // Use a buffered XML interface to manipulate
                XElement xe = XElement.Parse(m3.ToString());

                // WCF uses prefixed namespaces like crazy
                XNamespace tmp = "http://tempuri.org/";

                // constants/names
                var hdrns = XName.Get("Header", "http://schemas.xmlsoap.org/soap/envelope/");
                var hdrns_user = XName.Get("username", tmp.ToString());
                var hdrns_pass = XName.Get("password");

                // extract Header attribute
                var hE = xe.Element(hdrns);

                // assign expected values
                hE.Add(new XElement(tmp + "username", new XAttribute(XNamespace.Xmlns + "h", tmp), "demo@ioactive.com"));
                hE.Add(new XElement(tmp + "password", new XAttribute(XNamespace.Xmlns + "h", tmp), "demo"));
                //Console.WriteLine($"xe = {xe}");

                // Dump into an outgoing message request
                request = Message.CreateMessage(xe.CreateReader(), 1024 * 1024, MessageVersion.Soap11);

                return null;
            }
        }
        public class ClientMessageInspectorData
        {
            public bool BeforeSendRequestCalled { get; set; }
            public bool AfterReceiveReplyCalled { get; set; }
            public Message Request { get; set; }
            public Message Reply { get; set; }
            public IClientChannel Channel { get; set; }
        }
        class ClientMessagePropertyBehavior : IEndpointBehavior
        {
            ClientMessagePropertyInspector _inspector;

            public ClientMessagePropertyBehavior()
            {
                _inspector = new ClientMessagePropertyInspector();
            }

            public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
            {
            }

            public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
            {
                clientRuntime.ClientMessageInspectors.Add(_inspector);
            }

            public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
            {
            }

            public void Validate(ServiceEndpoint endpoint)
            {
            }
        }

        private class ClientMessagePropertyInspector : IClientMessageInspector
        {
            public void AfterReceiveReply(ref Message reply, object correlationState)
            {
            }

            public object BeforeSendRequest(ref Message request, IClientChannel channel)
            {
                //Console.WriteLine("in interceptor");
                
                // similar to the above code on accessing Message bufferes

                //var buffer = request.CreateBufferedCopy(Int32.MaxValue);
                //request = buffer.CreateMessage();
                //var m = buffer.CreateMessage();


                //var xd = new XDocument();
                //var xw = xd.CreateWriter();
                //m.WriteMessage(xw);
                //Console.WriteLine($"message = {xd.ToString()}");
                
                return null;
            }
        }

        public class ClientMessageInspectorBehavior : IEndpointBehavior
        {
            private ClientMessageInspector _inspector;

            public ClientMessageInspectorBehavior(ClientMessageInspectorData data)
            {
                _inspector = new ClientMessageInspector(data);
            }

            public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
            {
            }

            public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
            {
                clientRuntime.ClientMessageInspectors.Add(_inspector);
            }

            public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
            {
            }

            public void Validate(ServiceEndpoint endpoint)
            {
            }
        }

        ////////////////////////////////////////////////////// UNUSED UNUSED  //////////////////////////////////////////////////////
        ////////////////////////////////////////////////////// UNUSED UNUSED  //////////////////////////////////////////////////////
        ////////////////////////////////////////////////////// UNUSED UNUSED  //////////////////////////////////////////////////////
    }
}

