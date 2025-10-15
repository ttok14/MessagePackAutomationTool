using System;
using System.Diagnostics;
using System.IO;

namespace MSgPackBinaryGenerator
{
    public static class DotnetBuilder
    {
        public static string Build(string csprojPath)
        {
            string projectDir = Path.GetDirectoryName(csprojPath);
            string dllPath = Path.Combine(projectDir, "bin", "Release", "net9.0", $"{Path.GetFileNameWithoutExtension(csprojPath)}.dll");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csprojPath}\" -c Release",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                Console.WriteLine($"✅ Build succeeded: {dllPath}");
                return dllPath;
            }

            Console.WriteLine("❌ Build failed!");
            Console.WriteLine(stderr);
            throw new Exception("dotnet build failed");
        }
    }
}
