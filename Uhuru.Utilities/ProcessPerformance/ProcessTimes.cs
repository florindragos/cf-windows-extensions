﻿// -----------------------------------------------------------------------
// <copyright file="ProcessData.cs" company="Uhuru Software">
// </copyright>
// -----------------------------------------------------------------------
namespace Uhuru.Utilities.ProcessPerformance
{
    using System;
    using ComType = System.Runtime.InteropServices.ComTypes;
    
    // holds the process time data.
    struct ProcessTimes
    {
        public DateTime CreationTime, ExitTime, KernelTime, UserTime;
        public ComType.FILETIME RawCreationTime, RawExitTime, RawKernelTime, RawUserTime;

        public void ConvertTime()
        {
            CreationTime = FiletimeToDateTime(RawCreationTime);
            ExitTime = FiletimeToDateTime(RawExitTime);
            KernelTime = FiletimeToDateTime(RawKernelTime);
            UserTime = FiletimeToDateTime(RawUserTime);
        }

        private static DateTime FiletimeToDateTime(ComType.FILETIME FileTime)
        {
            try
            {
                if (FileTime.dwLowDateTime < 0) FileTime.dwLowDateTime = 0;
                if (FileTime.dwHighDateTime < 0) FileTime.dwHighDateTime = 0;
                long RawFileTime = (((long)FileTime.dwHighDateTime) << 32) + FileTime.dwLowDateTime;
                return DateTime.FromFileTimeUtc(RawFileTime);
            }
            catch (ArgumentOutOfRangeException) 
            { 
                return new DateTime(); 
            }
        }
    };
}
