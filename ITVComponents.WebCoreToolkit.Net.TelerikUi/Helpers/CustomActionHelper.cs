﻿using System;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers
{
    public static class CustomActionHelper
    {
        /// <summary>
        /// a randomizer that is used to generate random extensions
        /// </summary>
        private static Random rnd;

        /// <summary>
        /// a locker that avoids threading problems
        /// </summary>
        private static object locker;

        /// <summary>
        /// the last id that was generated by this helper
        /// </summary>
        private static int id;

        /// <summary>
        /// Initializes static members of the CustomActionHelper class
        /// </summary>
        static CustomActionHelper()
        {
            rnd = new Random();
            locker = new object();
            id = -1;
        }

        /// <summary>
        /// Creates a truely unique name for shitty kendo custom commands
        /// </summary>
        /// <param name="baseName">the base name that is used to identify an action</param>
        /// <returns>the random name for the provided baseName</returns>
        public static string RandomName(string baseName)
        {
            int localId;
            int rn;
            lock (locker)
            {
                localId = ++id;
                rn = rnd.Next(0, 131072);
            }

            return $"{baseName}_{localId}_{rn}";
        }
    }
}
