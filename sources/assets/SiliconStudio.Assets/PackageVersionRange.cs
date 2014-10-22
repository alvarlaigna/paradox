﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// Original code from http://nuget.codeplex.com  class SemanticVersion
// Copyright 2010-2014 Outercurve Foundation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using SiliconStudio.Core;

namespace SiliconStudio.Assets
{
    /// <summary>
    /// A dependency to a range of version.
    /// </summary>
    /// <remarks>
    ///  The version string is either a simple version or an arithmetic range
    /// <code>
    ///     e.g.
    ///     1.0         --> 1.0 ≤ x
    ///     (,1.0]      --> x ≤ 1.0
    ///     (,1.0)      --> x &lt; 1.0
    ///     [1.0]       --> x == 1.0
    ///     (1.0,)      --> 1.0 &lt; x
    ///     (1.0, 2.0)   --> 1.0 &lt; x &lt; 2.0
    ///     [1.0, 2.0]   --> 1.0 ≤ x ≤ 2.0
    /// </code>
    /// </remarks>
    [DataContract("PackageVersionDependency")]
    public sealed class PackageVersionRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PackageVersionRange"/> class.
        /// </summary>
        public PackageVersionRange()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageVersionRange"/> class.
        /// </summary>
        /// <param name="version">The exact version.</param>
        public PackageVersionRange(PackageVersion version)
        {
            IsMinInclusive = true;
            IsMaxInclusive = true;
            MinVersion = version;
            MaxVersion = version;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageVersionRange" /> class.
        /// </summary>
        /// <param name="minVersion">The minimum version.</param>
        /// <param name="minVersionInclusive">if set to <c>true</c> the minimum version is inclusive</param>
        public PackageVersionRange(PackageVersion minVersion, bool minVersionInclusive)
        {
            IsMinInclusive = minVersionInclusive;
            MinVersion = minVersion;
        }

        /// <summary>
        /// Gets or sets the minimum version.
        /// </summary>
        /// <value>The minimum version.</value>
        [DataMember(10)]
        public PackageVersion MinVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the min version is inclusive.
        /// </summary>
        /// <value><c>true</c> if the min version is inclusive; otherwise, <c>false</c>.</value>
        [DataMember(20)]
        public bool IsMinInclusive { get; set; }

        /// <summary>
        /// Gets or sets the maximum version.
        /// </summary>
        /// <value>The maximum version.</value>
        [DataMember(30)]
        public PackageVersion MaxVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the max version is inclusive.
        /// </summary>
        /// <value><c>true</c> if the max version is inclusive; otherwise, <c>false</c>.</value>
        [DataMember(40)]
        public bool IsMaxInclusive { get; set; }

        /// <summary>
        /// The safe range is defined as the highest build and revision for a given major and minor version
        /// </summary>
        public static PackageVersionRange GetSafeRange(PackageVersion version)
        {
            return new PackageVersionRange
                {
                    IsMinInclusive = true,
                    MinVersion = version,
                    MaxVersion = new PackageVersion(new Version(version.Version.Major, version.Version.Minor + 1))
                };
        }

        /// <summary>
        /// Tries to parse a version dependency.
        /// </summary>
        /// <param name="value">The version dependency as a string.</param>
        /// <param name="result">The parsed result.</param>
        /// <returns><c>true</c> if successfuly parsed, <c>false</c> otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">value</exception>
        public static bool TryParse(string value, out PackageVersionRange result)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            var versionSpec = new PackageVersionRange();
            value = value.Trim();

            // First, try to parse it as a plain version string
            PackageVersion version;
            if (PackageVersion.TryParse(value, out version))
            {
                // A plain version is treated as an inclusive minimum range
                result = new PackageVersionRange
                    {
                        MinVersion = version,
                        IsMinInclusive = true
                    };

                return true;
            }

            // It's not a plain version, so it must be using the bracket arithmetic range syntax

            result = null;

            // Fail early if the string is too short to be valid
            if (value.Length < 3)
            {
                return false;
            }

            // The first character must be [ ot (
            switch (value.First())
            {
                case '[':
                    versionSpec.IsMinInclusive = true;
                    break;
                case '(':
                    versionSpec.IsMinInclusive = false;
                    break;
                default:
                    return false;
            }

            // The last character must be ] ot )
            switch (value.Last())
            {
                case ']':
                    versionSpec.IsMaxInclusive = true;
                    break;
                case ')':
                    versionSpec.IsMaxInclusive = false;
                    break;
                default:
                    return false;
            }

            // Get rid of the two brackets
            value = value.Substring(1, value.Length - 2);

            // Split by comma, and make sure we don't get more than two pieces
            string[] parts = value.Split(',');
            if (parts.Length > 2)
            {
                return false;
            }
            if (parts.All(string.IsNullOrEmpty))
            {
                // If all parts are empty, then neither of upper or lower bounds were specified. Version spec is of the format (,]
                return false;
            }

            // If there is only one piece, we use it for both min and max
            string minVersionString = parts[0];
            string maxVersionString = (parts.Length == 2) ? parts[1] : parts[0];

            // Only parse the min version if it's non-empty
            if (!string.IsNullOrWhiteSpace(minVersionString))
            {
                if (!PackageVersion.TryParse(minVersionString, out version))
                {
                    return false;
                }
                versionSpec.MinVersion = version;
            }

            // Same deal for max
            if (!string.IsNullOrWhiteSpace(maxVersionString))
            {
                if (!PackageVersion.TryParse(maxVersionString, out version))
                {
                    return false;
                }
                versionSpec.MaxVersion = version;
            }

            // Successful parse!
            result = versionSpec;
            return true;
        }

        /// <summary>
        /// Display a pretty version of the dependency.
        /// </summary>
        /// <returns>A pretty version of the dependency.</returns>
        public string ToPrettyPrint()
        {
            if (MinVersion != null && IsMinInclusive && MaxVersion == null && !IsMaxInclusive)
            {
                return string.Format(CultureInfo.InvariantCulture, "(>= {0})", MinVersion);
            }

            if (MinVersion != null && MaxVersion != null && MinVersion == MaxVersion && IsMinInclusive && IsMaxInclusive)
            {
                return string.Format(CultureInfo.InvariantCulture, "(= {0})", MinVersion);
            }

            var versionBuilder = new StringBuilder();
            if (MinVersion != null)
            {
                if (IsMinInclusive)
                {
                    versionBuilder.AppendFormat("(>= ");
                }
                else
                {
                    versionBuilder.Append("(> ");
                }
                versionBuilder.Append(MinVersion);
            }

            if (MaxVersion != null)
            {
                versionBuilder.Append(versionBuilder.Length == 0 ? "(" : " && ");

                if (IsMaxInclusive)
                {
                    versionBuilder.AppendFormat("<= ");
                }
                else
                {
                    versionBuilder.Append("< ");
                }
                versionBuilder.Append(MaxVersion);
            }

            if (versionBuilder.Length > 0)
            {
                versionBuilder.Append(")");
            }

            return versionBuilder.ToString();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (MinVersion != null && IsMinInclusive && MaxVersion == null && !IsMaxInclusive)
            {
                return MinVersion.ToString();
            }

            if (MinVersion != null && MaxVersion != null && MinVersion == MaxVersion && IsMinInclusive && IsMaxInclusive)
            {
                return "[" + MinVersion + "]";
            }

            var versionBuilder = new StringBuilder();
            versionBuilder.Append(IsMinInclusive ? '[' : '(');
            versionBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}, {1}", MinVersion, MaxVersion);
            versionBuilder.Append(IsMaxInclusive ? ']' : ')');

            return versionBuilder.ToString();
        }

        internal static PackageVersionRange FromVersionSpec(NuGet.IVersionSpec spec)
        {
            if (spec == null)
            {
                return null;
            }

            return new PackageVersionRange()
                {
                    MinVersion = PackageVersion.FromSemanticVersion(spec.MinVersion),
                    IsMinInclusive = spec.IsMinInclusive,
                    MaxVersion = PackageVersion.FromSemanticVersion(spec.MaxVersion),
                    IsMaxInclusive = spec.IsMaxInclusive
                };
        }
    }
}