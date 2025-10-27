using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public static class DotnetBuilder
    {
        // dotnet cli 로 내부적으로는 MBBuild 로 .csproj 빌드
        // 주의점은 , 현 .NET SDK 환경에서는 해당 .csproj path 에 있는 .cs 파일들을
        // 자동으로 빌드에 포함시키기 때문에 빌드 시점에 의도치 않은 .cs 파일들이 있으면 에러 발생할 수 있음
        public static string Build(string csprojPath)
        {
            string projectDir = Path.GetDirectoryName(csprojPath);
            string dllPath = Path.Combine(projectDir, "bin", "Release", "net9.0", $"{Path.GetFileNameWithoutExtension(csprojPath)}.dll");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csprojPath}\" -c Release /v:d",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
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

            string[] lines = stdout.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            Console.WriteLine("--- 추출된 빌드 에러들 (Compilers Errors) ---");
            bool hasError = false;

            // stdout 가 매우 길기에 (빌드 과정 모든 로그 기록) 
            // C# 에러관련부분만 최대한 추출
            foreach (string line in lines)
            {
                if (line.Contains("error") && (line.Contains("CS") || line.Contains("MSB")))
                {
                    Console.WriteLine(line.Trim());
                    hasError = true;
                }
            }

            if (!hasError)
            {
                Console.WriteLine("(■■■■ 전체 stdout (detail log) (■■■■");
                Console.WriteLine(stdout);
            }
            else
            {
                Console.WriteLine($"■■■■ 에러 : {stderr}");
            }

            throw new Exception($"dotnet build failed (■■■■ MpcInput_Artifact 및 각 파일들 확인, 컴파일 에러가 발생하는 코드를 만들어 냈을 확률이 큼 (처리안된 부분이겠지..?) ■■■■)");
        }
    }
}
