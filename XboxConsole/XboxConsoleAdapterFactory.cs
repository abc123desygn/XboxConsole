﻿//------------------------------------------------------------------------------
// <copyright file="XboxConsoleAdapterFactory.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Internal.GamesTest.Xbox
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides a factory to create adapters compatible with installed XDK.
    /// </summary>
    internal static class XboxConsoleAdapterFactory
    {
        private const int November2014XdkBuild = 11785;
        private const int October2014XdkBuild = 11653;
        private const int August2014XdkBuild = 11396;
        private const int July2014XdkBuild = 11274;
        private const int May2014XdkBuild = 10951;
        private const int April2014XdkBuild = 10812;

        private static FileVersionInfo currentXdkVersion = null;

        // The adapter lookup table puts a relationship between adapters and versions of XDK they support.
        // The adapters must be listed in the latest-to-oldest order. Please add new adapters at the beginning of the list.
        private static Tuple<int, Func<XboxConsoleAdapterBase>>[] adapterLookup = 
        {
            new Tuple<int, Func<XboxConsoleAdapterBase>>(April2014XdkBuild, () => new Adapter.April2014.XboxConsoleAdapter()),
            new Tuple<int, Func<XboxConsoleAdapterBase>>(May2014XdkBuild, () => new Adapter.May2014.XboxConsoleAdapter()),
            new Tuple<int, Func<XboxConsoleAdapterBase>>(July2014XdkBuild, () => new Adapter.July2014.XboxConsoleAdapter()),
            new Tuple<int, Func<XboxConsoleAdapterBase>>(August2014XdkBuild, () => new Adapter.August2014.XboxConsoleAdapter()),
            new Tuple<int, Func<XboxConsoleAdapterBase>>(October2014XdkBuild, () => new Adapter.October2014.XboxConsoleAdapter()),
            new Tuple<int, Func<XboxConsoleAdapterBase>>(November2014XdkBuild, () => new Adapter.November2014.XboxConsoleAdapter()),
        };

        /// <summary>
        /// The lookup for a branch to Xdk version.
        /// </summary>
        private static Dictionary<string, int> adapterBranchBases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) 
            {
                { "xb_rel_1404", April2014XdkBuild },
                { "xb_rel_1405", May2014XdkBuild },
                { "xb_rel_1407", July2014XdkBuild },
                { "xb_rel_1408", August2014XdkBuild },
                { "xb_rel_1410", October2014XdkBuild },
                { "xb_rel_1411", November2014XdkBuild }
            };

        /// <summary>
        /// Gets the current XDK version from file, returning a previously cached version if called more than once.
        /// </summary>
        internal static FileVersionInfo XdkVersion
        {
            get
            {
                if (currentXdkVersion == null)
                {
                    string xdkdir = Environment.GetEnvironmentVariable("DurangoXDK");

                    if (xdkdir == null)
                    {
                        throw new XdkNotFoundException("The environment variable for XDK location is not found. Please make sure the XDK is installed.");
                    }

                    string filePath = Directory.EnumerateFiles(Path.Combine(xdkdir, @"bin"), "xb*", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if (filePath == null)
                    {
                        throw new XboxConsoleException("Unable to find 'xb*' file to verify version of XDK installed");
                    }

                    currentXdkVersion = FileVersionInfo.GetVersionInfo(filePath);                    
                }

                return currentXdkVersion;
            }
        }

        /// <summary>
        /// Creates and returns an adapter compatible with installed XDK.
        /// </summary>
        /// <returns>The best compatible adapter.</returns>
        internal static XboxConsoleAdapterBase CreateAdapterForInstalledXdk()
        {
            FileVersionInfo fileVersionInfo = XdkVersion;
            var buildKey = fileVersionInfo.FileBuildPart;

            // format: <version> (<branchName>.<timestamp>)
            // version: (standard) w.x.y.z
            // branchName example: xb_rel_1304
            // timestamp: yymmdd-hhmm
            Regex regex = new Regex(@"\d+\.\d+\.\d+\.\d+ \((?<branch>.+)\.\d{6}-\d{4}\)", RegexOptions.IgnoreCase);
            Match match = regex.Match(fileVersionInfo.FileVersion);
            if (match.Success)
            {
                // Branch name pattern examples:
                //  July 2013 XDK: xb_rel_1306
                //  July 2013 XDK QFE 1: xb_rel_1306
                //  August 2013 XDK: xb_rel_1308
                //  August 2013 XDK QFE 2: xb_rel_1308qfe2
                // The predicate below will catch both of these patterns, and return the correct base branch
                // for each full branch name, which will allow us to instantiate the correct adapter without
                // a release needed for each QFE.
                string fullBranchName = match.Groups["branch"].Value;
                string branch = adapterBranchBases.Keys.FirstOrDefault(branchBase => fullBranchName.StartsWith(branchBase, StringComparison.OrdinalIgnoreCase));

                if (branch == null)
                {
                    // Default to the newest adapter.
                    branch = adapterBranchBases.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
                }

                buildKey = adapterBranchBases[branch];
            }

            var bestCompatibleAdapterTuple = adapterLookup.FirstOrDefault(adapterTuple => adapterTuple.Item1 == buildKey);
            if (bestCompatibleAdapterTuple == null)
            {
                XdkAdapterNotFound();
            }

            return bestCompatibleAdapterTuple.Item2();
        }

        private static void XdkAdapterNotFound()
        {
            throw new XboxConsoleException(string.Format(CultureInfo.CurrentCulture, "Could not find a compatible adapter for installed XDK"));
        }
    }
}
