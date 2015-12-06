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

using System.ServiceModel;

namespace SO.Elmer.Client
{
    [MessageContract]
    public abstract class Paste
    {
        public virtual bool Succeeded { get; set; }
    }

    [MessageContract]
    public partial class LoginCredsText : Paste
    {
        [MessageBodyMember]
        public string username { get; set; }
        [MessageBodyMember]
        public string password { get; set; }

        public LoginCredsText()
        {
            username = string.Empty;
            password = string.Empty;
        }
        public LoginCredsText(LoginCredsText lc)
        {
            this.username = lc.username;
            this.password = lc.password;
        }
    }
    [MessageContract]
    public partial class LoginReplyText
    {
        [MessageBodyMember]
        public bool Succeeded { get; set; }
    }

    [MessageContract]
    public partial class ErrorLogText : LoginCredsText
    {
        public ErrorLogText(LoginCredsText lc) : base(lc) { }
        public ErrorLogText() { }

        [MessageBodyMember]
        public string message { get; set; } // any string data
        [MessageBodyMember]
        public uint id { get; set; } // any numeric data
    }
    
    [MessageContract]
    public partial class QueryReply
    {
        public QueryReply() { }
        public QueryReply(uint ret) { rv = ret; }
        [MessageBodyMember]
        public bool Succeeded { get; set; }
        [MessageBodyMember]
        public uint rv;
    }
    [MessageContract]
    public partial class Query : LoginCredsText
    {
        public Query(LoginCredsText lc) : base(lc) { }
        public Query() { }
        [MessageBodyMember]
        public byte[] hash;
    }

    [MessageContract]
    public partial class NetRelocTextRequest : LoginCredsText
    {
        public NetRelocTextRequest(LoginCredsText lc) : base(lc) { }

        [MessageBodyMember] 
        public string RegionName;
        [MessageBodyMember] 
        public uint TimeDateStamp;
        [MessageBodyMember] 
        public bool Is64bit;
    }
    [MessageContract]
    public partial class NetRelocTextReply
    {
        [MessageBodyMember]
        public bool Succeeded { get; set; }
        [MessageBodyMember]
        public string KnownAs;
        [MessageBodyMember]
        public ulong FileImageBase;
        [MessageBodyMember]
        public byte[] NetRelocContent;
    }
}
