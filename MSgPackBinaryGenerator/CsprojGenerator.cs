using System;
using System.IO;

namespace MSgPackBinaryGenerator
{
    public static class CsprojGenerator
    {
        public static string GenerateProject(string outputDir, string projectName)
        {
            Directory.CreateDirectory(outputDir);
            string csprojPath = Path.Combine(outputDir, $"{projectName}.csproj");

            string csprojXml = $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""MessagePack"" Version=""3.1.4"" />
    <PackageReference Include=""MessagePack.Annotations"" Version=""3.1.4"" />
  </ItemGroup>
</Project>";

            File.WriteAllText(csprojPath, csprojXml);
            Console.WriteLine($"✅ Generated .csproj: {csprojPath}");
            return csprojPath;
        }
    }
}
