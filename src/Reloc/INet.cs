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

using System.Collections.Generic;

namespace SO.Elmer.Client
{
    public interface INet
    {
        ICollection<string> logs { get; set; }
        string UserName { get; set; }
        string PassWord { get; set; }
        bool Online();
        void Offline();
        void SendErrorLogs(string file);
        uint Query(byte[] Hash);
        byte[] NetRelocCheck(string RegionName, uint TimeDateStamp, bool Is64, ref ulong PerferredLoadAddr, ref string KnownAsFileName);

    }
}
 