﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CssAutoPrefixer
{
    internal class NodeProcess
    {
        public const string Packages = "postcss-cli autoprefixer";

        private static string _installDir = Path.Combine(Path.GetTempPath(), Vsix.Name, Packages.GetHashCode().ToString());
        private static string _executable = Path.Combine(_installDir, "node_modules\\.bin\\postcss.cmd");

        public bool IsInstalling
        {
            get;
            private set;
        }

        public bool IsReadyToExecute()
        {
            return File.Exists(_executable);
        }

        public async Task<bool> EnsurePackageInstalled()
        {
            if (IsInstalling)
                return false;

            if (IsReadyToExecute())
                return true;

            bool success = await Task.Run(() =>
             {
                 IsInstalling = true;

                 try
                 {
                     if (!Directory.Exists(_installDir))
                         Directory.CreateDirectory(_installDir);

                     var start = new ProcessStartInfo("cmd", $"/c npm install {Packages}")
                     {
                         WorkingDirectory = _installDir,
                         UseShellExecute = false,
                         RedirectStandardOutput = true,
                         CreateNoWindow = true,
                     };

                     ModifyPathVariable(start);

                     using (var proc = Process.Start(start))
                     {
                         proc.WaitForExit();
                         return proc.ExitCode == 0;
                     }
                 }
                 catch (Exception ex)
                 {
                     Logger.Log(ex);
                     return false;
                 }
                 finally
                 {
                     IsInstalling = false;
                 }
             });

            return success;
        }

        public async Task<string> ExecuteProcess(string input)
        {
            if (!await EnsurePackageInstalled())
                return null;

            var start = new ProcessStartInfo("cmd", $"/c \"{_executable}\" --use autoprefixer")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            ModifyPathVariable(start);

            try
            {
                var sb = new StringBuilder();

                using (var proc = Process.Start(start))
                {
                    using (StreamWriter stream = proc.StandardInput)
                    {
                        await stream.WriteAsync(input);
                    }

                    while (!proc.StandardOutput.EndOfStream)
                    {
                        string line = await proc.StandardOutput.ReadLineAsync();
                        sb.AppendLine(line);
                    }

                    proc.WaitForExit();
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return null;
            }
        }

        private static void ModifyPathVariable(ProcessStartInfo start)
        {
            string path = start.EnvironmentVariables["PATH"];

            var process = Process.GetCurrentProcess();
            string ideDir = Path.GetDirectoryName(process.MainModule.FileName);

            if (Directory.Exists(ideDir))
            {
                string parent = Directory.GetParent(ideDir).Parent.FullName;

                string rc2Preview1Path = new DirectoryInfo(Path.Combine(parent, @"Web\External")).FullName;

                if (Directory.Exists(rc2Preview1Path))
                {
                    path += ";" + rc2Preview1Path;
                    path += ";" + rc2Preview1Path + "\\git";
                }
                else
                {
                    path += ";" + Path.Combine(ideDir, @"Extensions\Microsoft\Web Tools\External");
                    path += ";" + Path.Combine(ideDir, @"Extensions\Microsoft\Web Tools\External\git");
                }
            }

            start.EnvironmentVariables["PATH"] = path;
        }
    }
}
