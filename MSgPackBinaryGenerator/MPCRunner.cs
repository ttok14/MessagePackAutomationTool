using System;
using System.Diagnostics;
using System.IO;

namespace MSgPackBinaryGenerator
{
    public static class MpcRunner
    {
        public static bool Run(string inputProjectOrDll, string outputPath, string resolverName = "GameDBContainerResolver")
        {
            if (!File.Exists(inputProjectOrDll))
            {
                Console.WriteLine($"❌ Input not found: {inputProjectOrDll}");
                return false;
            }

            string mpcArgs =
                $"-i \"{inputProjectOrDll}\" " +
                $"-o \"{outputPath}\" " +
                $"-r \"{resolverName}\" " +
                $"-n GameDB " +
                $"-m resolver";

            var psi = new ProcessStartInfo
            {
                FileName = "mpc",
                Arguments = mpcArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Console.WriteLine(stdout);
            if (proc.ExitCode != 0)
            {
                Console.WriteLine($"❌ MPC failed:\n{stderr}");
                return false;
            }

            Console.WriteLine($"✅ MPC completed successfully → {outputPath}");
            return true;
        }
    }
}
