using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Python.Runtime;

namespace Python.Included
{
    public class Installer
    {
        public const string EMBEDDED_PYTHON = "python-3.7.3-embed-amd64";
        public const string PYTHON_VERSION = "python37";
        public async Task SetupPython(bool force = false)
        {
            if (Runtime.Runtime.pyversion!="3.7")
                throw new InvalidOperationException("You must compile Python.Runtime with PYTHON37 flag! Runtime version: " + Runtime.Runtime.pyversion);
            Environment.SetEnvironmentVariable("PATH", EmbeddedPythonHome);
            if (!force && Directory.Exists(EmbeddedPythonHome) && File.Exists(Path.Combine(EmbeddedPythonHome, "python.exe"))) // python seems installed, so exit
                return;
            await Task.Run(() =>
            {
                var assembly = this.GetType().Assembly;
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var zip = Path.Combine(appdata, $"{EMBEDDED_PYTHON}.zip");
                var resource_name = EMBEDDED_PYTHON;
                CopyEmbeddedResourceToFile(assembly, resource_name, zip, force);
                ZipFile.ExtractToDirectory(zip, zip.Replace(".zip", ""));
            });
        }

        private void CopyEmbeddedResourceToFile(Assembly assembly, string resourceName, string filePath, bool force = false)
        {
            if (force || !File.Exists(filePath))
            {
                var key = GetResourceKey(assembly, resourceName);
                using (Stream stream = assembly.GetManifestResourceStream(key))
                using (var file = new FileStream(filePath, FileMode.Create))
                {
                    if (stream == null)
                        throw new ArgumentException($"Resource name '{resourceName}' not found!");
                    stream.CopyTo(file);
                }
            }
        }

        /// <summary>
        /// Install a python library (.whl file) in the embedded python installation of Python.Included
        /// </summary>
        /// <param name="assembly">The assembly containing the embedded wheel</param>
        /// <param name="resource_name">Name of the embedded wheel file i.e. "numpy-1.16.3-cp37-cp37m-win_amd64.whl"</param>
        /// <param name="force"></param>
        /// <returns></returns>
        public async Task InstallWheel(Assembly assembly, string resource_name, bool force = false)
        {
            var key = GetResourceKey(assembly, resource_name);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"The resource '{resource_name}' was not found in assembly '{assembly.FullName}'");
            var module_name = resource_name.Split('-').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(module_name))
                throw new ArgumentException($"The resource name '{resource_name}' did not contain a valid module name");
            var lib = Path.Combine(EmbeddedPythonHome, "Lib");
            if (!Directory.Exists(lib))
                Directory.CreateDirectory(lib);
            var module_path = Path.Combine(lib, module_name);
            if (!force && Directory.Exists(module_path))
                return;
            var wheelPath = Path.Combine(lib, key);
            await Task.Run(() =>
            {
                CopyEmbeddedResourceToFile(assembly, key, wheelPath, force);
                ZipFile.ExtractToDirectory(wheelPath, lib);
                // modify _pth file
                var pth = Path.Combine(EmbeddedPythonHome, PYTHON_VERSION + ".pth");
                if (!File.ReadAllLines(pth).Contains("./Lib"))
                    File.AppendAllLines(pth, new[] { "./ Lib" });
            });
        }

        public string EmbeddedPythonHome
        {
            get
            {
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var install_dir = Path.Combine(appdata, EMBEDDED_PYTHON);
                return install_dir;
            }
        }

        public string GetResourceKey(Assembly assembly, string embedded_file)
        {
            return assembly.GetManifestResourceNames().FirstOrDefault(x => x.Contains(embedded_file));
        }
    }
}
