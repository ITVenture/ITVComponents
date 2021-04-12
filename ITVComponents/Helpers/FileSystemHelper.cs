using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ITVComponents.Helpers
{
    /// <summary>
    /// Helper class for some filesystem operations
    /// </summary>
    public static class FileSystemHelper
    {
        /// <summary>
        /// Determines whether a specific path leads to an existing directory
        /// </summary>
        /// <param name="path">the path for which to check whether it is a directory</param>
        /// <returns>a value indicating whether the given path exists and is a directory</returns>
        public static bool IsDirectory(string path)
        {
            return WhatIs(path) == PathType.Directory;
        }

        /// <summary>
        /// Determines whether a specific path leads to an existing file
        /// </summary>
        /// <param name="path">the path the check for file-existance</param>
        /// <returns>a value indicating whether the path is a file that exists</returns>
        public static bool IsFile(string path)
        {
            return WhatIs(path) == PathType.File;
        }

        /// <summary>
        /// Determines whether a specific path is a file, a directory, or inexistent
        /// </summary>
        /// <param name="path">the path to check for the kind of it</param>
        /// <returns>a value indicating whether the path exists and what type it is</returns>
        public static PathType WhatIs(string path)
        {
            if (File.Exists(path))
            {
                return PathType.File;
            }

            if (Directory.Exists(path))
            {
                return PathType.Directory;
            }

            return PathType.Inexistent;
        }

        public enum PathType
        {
            File,
            Directory,
            Inexistent
        }
    }
}
