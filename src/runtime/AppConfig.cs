using System;
using System.Linq;
using System.Reflection;

namespace Python.Runtime
{
    static class AppConfig
    {
        /// <summary>
        /// Override the AppDomain's config file path and reset the
        /// ConfigurationManager cache so the CLR re-reads binding redirects.
        /// Uses reflection to avoid a compile-time dependency on System.Configuration.
        /// </summary>
        public static void SetConfigFile(string path)
        {
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", path);
            ResetConfigMechanism();
        }

        private static void ResetConfigMechanism()
        {
            var configManager = Type.GetType(
                "System.Configuration.ConfigurationManager, System.Configuration, "
                + "Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            if (configManager == null)
                return;

            configManager
                .GetField("s_initState", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, 0);

            configManager
                .GetField("s_configSystem", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);

            configManager.Assembly
                .GetTypes()
                .FirstOrDefault(x => x.FullName == "System.Configuration.ClientConfigPaths")
                ?.GetField("s_current", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
        }
    }
}
