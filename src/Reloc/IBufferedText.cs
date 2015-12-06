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

using SO.Elmer.Client;
using System.ServiceModel;

namespace SO.Elmer.Server
{
    [ServiceContract]
    public interface IBufferedText
    {
        [OperationContract]
        LoginReplyText Online(LoginCredsText input);
        [OperationContract]
        void Offline(LoginCredsText input);
        [OperationContract]
        void ErrorSubmit(ErrorLogText log);
        [OperationContract]
        QueryReply Query(Query input);

        [OperationContract]
        NetRelocTextReply NetRelocCheck(NetRelocTextRequest sm);
    }
}

