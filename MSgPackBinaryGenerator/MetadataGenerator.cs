using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;

namespace MSgPackBinaryGenerator
{
    public class MetadataGenerator
    {
        public MasterMetadata Generate(string binaryDirectory)
        {
            MasterMetadata result = new MasterMetadata();
            List<string> results = new List<string>();

            var filePaths = Directory.GetFiles(binaryDirectory, $"*.{Constants.BinaryOutputExtension}").OrderBy(p => p);
            foreach (var path in filePaths)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var fileInfo = new FileInfo(path);

                result.Files.Add(new FileMetadata()
                {
                    Name = name,
                    ByteSize = fileInfo.Length,
                    Hash = Helper.GetHashByFilePath(path)
                });
            }

            StringBuilder combinedHash = new StringBuilder();
            foreach (var file in result.Files)
            {
                combinedHash.Append(file.Hash);
            }

            result.TotalHash = Helper.GetHashByString(combinedHash.ToString());
            result.Version = DateTime.Now.ToString("yyMMdd_HHmmss");

            return result;
        }
    }
}
