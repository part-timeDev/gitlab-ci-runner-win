using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using gitlab_ci_runner.helper;

namespace gitlab_ci_runner.conf
{
    class Config
    {
        /// <summary>
        /// URL to the Gitlab CI coordinator
        /// </summary>
        public static string url;

        /// <summary>
        /// Gitlab CI runner auth token
        /// </summary>
        public static string token;

        /// <summary>
        /// Working dir for the builds
        /// </summary>
        public static string workingdir;

        /// <summary>
        /// Configuration Path
        /// </summary>
        private static string confPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\runner.cfg";

        /// <summary>
        /// Load the configuration
        /// </summary>
        public static void loadConfig()
        {
            if (File.Exists(confPath))
            {
                IniFile ini = new IniFile(confPath);
                url = ini.IniReadValue("main", "url");
                token = ini.IniReadValue("main", "token");
                workingdir = ini.IniReadValue("main", "workingdir");
            }
        }

        /// <summary>
        /// Save the configuration
        /// </summary>
        public static void saveConfig()
        {
            if (File.Exists(confPath))
            {
                File.Delete(confPath);
            }

            IniFile ini = new IniFile(confPath);
            ini.IniWriteValue("main", "url", url);
            ini.IniWriteValue("main", "token", token);
            ini.IniWriteValue("main", "workingdir", workingdir);
        }

        /// <summary>
        /// Is the runner already configured?
        /// </summary>
        /// <returns>true if configured, false if not</returns>
        public static bool isConfigured()
        {
            if (!String.IsNullOrEmpty(url) && !String.IsNullOrEmpty(token) && !String.IsNullOrEmpty(workingdir))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
