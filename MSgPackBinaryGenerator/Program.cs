using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.CodeDom;
using System.Reflection;
using Microsoft.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using MessagePack;
using System.Runtime.InteropServices;

namespace MSgPackBinaryGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var itemTableCsv = File.ReadAllLines(@"C:\Users\LeeYunSeon\source\repos\MSgPackBinaryGenerator\MSgPackBinaryGenerator\Data\ItemTable.csv");
            var itemTableSchemaCsv = File.ReadAllLines(@"C:\Users\LeeYunSeon\source\repos\MSgPackBinaryGenerator\MSgPackBinaryGenerator\Data\ItemTable_Schema.csv");

            //Console.WriteLine(itemTableCsv);
            //Console.WriteLine(itemTableSchemaCsv);

            string itemTableTypes = "";
            string[] itemDataTableColumns = itemTableCsv[0].Split(',');

            for (int i = 0; i < itemDataTableColumns.Length; i++)
            {
                for (int j = 1; j < itemTableSchemaCsv.Length; j++)
                {
                    var columnName = itemTableSchemaCsv[j].Split(',')[0];
                    var typeName = itemTableSchemaCsv[j].Split(',')[1];

                    if (itemDataTableColumns[i] == columnName)
                    {
                        itemTableTypes += $",{typeName}";
                    }
                }
            }

            itemTableTypes = itemTableTypes.Trim(',');

            //Console.WriteLine(itemTableCsv[0]);
            //Console.WriteLine(itemTableTypes);
            var generator = new DBContainerGenerator();

            var classDefinitions = new List<TableSchemaDefinition>();
            var itemTableDef = new TableSchemaDefinition("ItemTable", itemTableCsv[0], itemTableTypes);
            classDefinitions.Add(itemTableDef);

            var dbContainerSourceCode = generator.Generate(new List<TableSchemaDefinition>() { itemTableDef });
            //Console.WriteLine(dbContainerSourceCode);
            //Console.WriteLine(dbContainerSourceCode);

            // 데이터 
            var itemTableCsvList = itemTableCsv.ToList();
            itemTableCsvList.RemoveAt(0);
            string[] itemDataRaws = itemTableCsvList.ToArray();


            var itemTableDataGroup = new TableDataDefinition(itemDataTableColumns, itemDataRaws, itemTableDef);

            //-------------------------------------//

            var enumCsv = File.ReadAllLines(@"C:\Users\LeeYunSeon\source\repos\MSgPackBinaryGenerator\MSgPackBinaryGenerator\Data\EnumTable.csv");
            var raws = enumCsv.ToList();
            raws.RemoveAt(0);
            var enumGroup = new EnumGroups(enumCsv[0], raws.ToArray());

            var enumGenerator = new DBEnumContainer();
            var enumSourceCode = enumGenerator.Generate(enumGroup);
            Console.WriteLine(enumSourceCode);
            //Console.WriteLine(enumSourceCode.ToString());

            //Console.WriteLine(dbContainerSourceCode);
            //Console.WriteLine(enumSourceCode);

            //------------------------------//
            //Console.WriteLine(dbContainerSourceCode);
            //CompileSource(dbContainerSourceCode);
            // CompileSource(enumSourceCode);

            //---------------------------------------//

            //----------------------------------------//

            Console.WriteLine("--------------------------------------------------");
            var binaryExporter = new BinaryExporterGeneratorGenerator();
            var itemTableContainer = new TableContainer(classDefinitions[0], itemTableDataGroup);
            var tableContainer = new List<TableContainer>() { itemTableContainer };
            var binaryGeneratorSourceCode = binaryExporter.Generate(tableContainer, enumGroup);
            //  Console.WriteLine(binaryGeneratorSourceCode);

            // RuntimeCompiler.CompileSource(binaryGeneratorSourceCode);

            var mpcInputGenerator = new MpcInputGenerator();
            var mpcInputSourceCode = mpcInputGenerator.Generate(tableContainer, enumGroup);

            // Console.WriteLine(mpcInputSourceCode);
            // RuntimeCompiler.CompileSourceToDll(mpcInputSourceCode, $@"{Directory.GetCurrentDirectory()}/.dll");

            StartPipeline(
                mpcInputSourceCode: mpcInputSourceCode,
                dbContainerSourceCode: dbContainerSourceCode,
                enumSourceCode: enumSourceCode,
                binaryGeneratorSourceCode: binaryGeneratorSourceCode);
        }

        static void StartPipeline(
            string mpcInputSourceCode,
            string dbContainerSourceCode,
            string enumSourceCode,
            string binaryGeneratorSourceCode)
        {
            string workingDirectory = $"{Directory.GetCurrentDirectory()}/pipeline";
            Directory.CreateDirectory(workingDirectory);
            string srcPath = Path.Combine(workingDirectory, "MpcInput.cs");
            File.WriteAllText(srcPath, mpcInputSourceCode);
            Console.WriteLine($"Saved source code to: {srcPath}");

            string csprojPath = CsprojGenerator.GenerateProject(workingDirectory, "MpcInputProject");
            string dllPath = DotnetBuilder.Build(csprojPath);

            // Resolver 생성 
            string resolveOutput = Path.Combine(workingDirectory, "GameDBResolver.cs");
            MpcRunner.Run(csprojPath, resolveOutput);

            var resolverAssembly = RuntimeCompiler.CompileSource(resolveOutput);

            var binaryExporterAssembly = RuntimeCompiler.CompileSource(binaryGeneratorSourceCode);

            //string mpcResolverDllPath = $@"{workingDirectory}/mpcInputAssembly.dll";
            //RuntimeCompiler.CompileSourceToDll(mpcInputSourceCode, mpcResolverDllPath);

            //MpcRunner.RunMpcProcess(mpcResolverDllPath, $"{workingDirectory}/GameDBContainerResolver.cs");
        }
    }
}
