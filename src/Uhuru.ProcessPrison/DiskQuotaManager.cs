﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DiskQuotaTypeLibrary;
using Alphaleonis.Win32.Filesystem;

namespace Uhuru.Isolation
{
    public class DiskQuotaManager
    {
        /// <summary>
        /// DiskQuotaControlls mapped to the unique volume name.
        /// </summary>
        private static Dictionary<string, DIDiskQuotaControl> quotaControls  = new Dictionary<string,DIDiskQuotaControl>();

        private static object locker = new object();

        /// <summary>
        /// Initialize the quota for everu volume on the system.
        /// </summary>
        public static void StartQuotaInitialization()
        {
            string[] systemVolumes = Volume.GetVolumes();

            lock (locker)
            {
                foreach (string volume in systemVolumes)
                {
                    try
                    {
                        var volumeInfo = Volume.GetVolumeInformation(volume);
                        if (volumeInfo.SupportsDiskQuotas)
                        {
                            // Volume.GetVolumePathNamesForVolume(volume);
                            StartQuotaInitialization(volume);
                        }
                    }
                    catch (DeviceNotReadyException)
                    {
                    }
                }
            }
        }

        public static void StartQuotaInitialization(string rootPath)
        {
            lock (locker)
            {
                var uniqueVolumeName = Volume.GetUniqueVolumeNameForVolumeMountPoint(rootPath);

                if (!quotaControls.ContainsKey(uniqueVolumeName))
                {
                    var qcontrol = quotaControls[uniqueVolumeName] = new DiskQuotaControlClass();

                    qcontrol.Initialize(uniqueVolumeName, true);
                    qcontrol.QuotaState = QuotaStateConstants.dqStateEnforce;

                    // Set to ResolveNone to prevent blocking when using account names.
                    qcontrol.UserNameResolution = UserNameResolutionConstants.dqResolveNone;
                    qcontrol.LogQuotaThreshold = true;
                    qcontrol.LogQuotaLimit = true;

                    // Disable default quota limit and threshold
                    qcontrol.DefaultQuotaThreshold = -1;
                    qcontrol.DefaultQuotaLimit = -1;
                }
            }
        }

        public static bool IsQuotaInitialized()
        {
            lock (locker)
            {
                foreach (var volumePath in quotaControls.Keys)
                {
                    if (!IsQuotaInitialized(volumePath))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool IsQuotaInitialized(string rootPath)
        {
            lock (locker)
            {
                var uniqueVolumeName = Volume.GetUniqueVolumeNameForVolumeMountPoint(rootPath);

                DIDiskQuotaControl qcontrol = null;

                if (quotaControls.TryGetValue(uniqueVolumeName, out qcontrol))
                {
                    return !qcontrol.QuotaFileIncomplete && !qcontrol.QuotaFileRebuilding;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets a object that manages the quota for a specific user on a specific volume.
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="WindowsUsername"></param>
        public static DIDiskQuotaUser GetDiskQuotaUser(string rootPath, string WindowsUsername)
        {
            lock (locker)
            {
                var uniqueVolumeName = Volume.GetUniqueVolumeNameForVolumeMountPoint(rootPath);

                DIDiskQuotaControl qcontrol = null;

                if (quotaControls.TryGetValue(uniqueVolumeName, out qcontrol))
                {
                    return qcontrol.AddUser(WindowsUsername);
                }

                throw new ArgumentException("Volume root path not found or not initialized. ", "rootPath");
            }
        }

        /// <summary>
        /// Get the volume root mount of the path. 
        /// </summary>
        /// <param name="path">The path for which the volume should be returned.</param>
        /// <returns>The root volume path.</returns>
        public static string GetVolumeRootFromPath(string path)
        {
            string currentPath = path + @"\";
            bool isVolume = Alphaleonis.Win32.Filesystem.Volume.IsVolume(currentPath);

            if (isVolume)
            {
                return currentPath;
            }
            else
            {
                string parentPath = new System.IO.DirectoryInfo(path).Parent.FullName;
                return GetVolumeRootFromPath(parentPath);
            }
        }

        /// <summary>
        /// Get the unique volume name for a path. 
        /// </summary>
        /// <param name="path">The path for which the volume should be returned.</param>
        public static string GetUniqueVolumeNameFromPath(string path)
        {
            return Volume.GetUniqueVolumeNameForVolumeMountPoint(GetVolumeRootFromPath(path));
        }
    }
}
