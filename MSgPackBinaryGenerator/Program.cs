using MessagePack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
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
                Directory.GetCurrentDirectory(),
                mpcInputSourceCode: mpcInputSourceCode,
                dbContainerSourceCode: dbContainerSourceCode,
                enumSourceCode: enumSourceCode,
                binaryGeneratorSourceCode: binaryGeneratorSourceCode);
        }

        static void StartPipeline(
            string outputDirectory,
            string mpcInputSourceCode,
            string dbContainerSourceCode,
            string enumSourceCode,
            string binaryGeneratorSourceCode)
        {
            Console.WriteLine($"OutputDirectory : {outputDirectory}");

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            outputDirectory = $"{outputDirectory}/Result_{timestamp}";
            Directory.CreateDirectory(outputDirectory);

            // .cs 파일을 만듬 (여기에 mpc 에서 Formatter 생성할때 필요한 클래스들이 위치)
            string srcPath = Path.Combine(outputDirectory, "MpcInput.cs");
            File.WriteAllText(srcPath, mpcInputSourceCode);

            //Console.WriteLine($"Saved source code to: {srcPath}");

            // mpc 가 resolver 를 만들때 필요한 .csproj 및 dll 을 생성
            string inputProjectForMpc = CsprojGenerator.GenerateProject(outputDirectory, "MpcInputProject");
            // 여기서 이제 .csproj를 보고 컴파일을해 어셈블리를 생성 
            // ** 이때 , 중요한거는 .NET SDK 버전 이후로 (이전에는 non-sdk, e.g .Net Framework)
            // .csproj 를 빌드할때는 .csproj 에 직접 <Compile Include="ItemTable.cs" />.. 이런식으로
            // 추가하지 않아도 동일 경로 + 하위 경로들에 위치한 모든 .cs 파일을 빌드에 포함시킨다고 함. 
            // 이런 이유로 위에서 만든 MpcInput.cs 파일을 직접적으로 이 빌드에 연결시키지 않고 그냥 파일을
            // 동일 디렉터리에 생성한 것 만으로도 빌드에 포함되는 것 . 
            string inputDllForMpc = DotnetBuilder.Build(inputProjectForMpc);

            Console.WriteLine("-------------------------");

            // Resolver 생성 
            string resolveOutput = Path.Combine(outputDirectory, "GameDBResolver.cs");

            // mpc 도 내부적으로 MSBuild 로 빌드를 하기 때문에 
            // 이 시점 이전에 의도치않은 .cs 파일 생성은 주의해야함 (e.g GameDBContainer.cs)
            MpcRunner.Run(inputProjectForMpc, resolveOutput);

            Console.WriteLine("-------------------------");

            Console.WriteLine(binaryGeneratorSourceCode);
            //RuntimeCompiler.CompileSource(mpcInputSourceCode);
            var resolverSourceCode = File.ReadAllText(resolveOutput);

            // resolver 컴파일해서 어셈블리 겟 
            // var resolverAssembly = RuntimeCompiler.CompileSource(resolverSourceCode, new string[] { mpcInputDllPath });

            // mpc 결과물인 resolver 는 inputDll 에 있는 타입들 (e.g ItemTable, E_ItemType 등..) 
            // 을 사용하기 때문에 inputDll 을 넣어줘야 resolverDll 컴파일 가능.
            var resolverAssembly = RuntimeCompiler.CompileSourceToDll(
                resolverSourceCode,
                new string[] { inputDllForMpc },
                outputDirectory,
                "GameDBResolver.dll"
            );

            var currentDllPath = Assembly.GetExecutingAssembly().Location;

            // !참고로 mpc 가 만든 Resolver 는 내부적으로 이런식으로 global::GameDB.E_ItemType) mpcInput 을 참조중임!
            // 즉 이게 무슨 말이냐면 , Resolver 어셈블리를 로드하는 순간 Resolver 가 의존하는 mpcInput 어셈블리도 로드를 한다는 의미.
            // 그렇기에 다음 코드의 additionalReference 에 inputDllForMpc 을 추가하면 
            // 그 안에 중복이 생기기때문에 에러가 발생함.
            var binaryExporterAssembly = RuntimeCompiler.CompileSource(
                binaryGeneratorSourceCode,
                new string[]
                {
                    /* inputDllForMpc,*/ 
                    resolverAssembly.Location,
                    currentDllPath
                });

            var type = binaryExporterAssembly.GetType("GameDB.TableBinaryExporter");
            if (type == null)
            {
                Console.WriteLine("❌ TableBinaryExporter 타입을 찾을 수 없습니다.");
                return;
            }

            // ExportItemTable 메서드를 찾아서 호출
            var method = type.GetMethod("ExportItemTable", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                Console.WriteLine("❌ ExportItemTable 메서드를 찾을 수 없습니다.");
                return;
            }
            else
            {
                Console.WriteLine("Method Found : " + method.Name);
            }

            Console.WriteLine(Directory.GetCurrentDirectory());
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "pipeline", "ItemTable.bytes");

            #region ===:: 실제 Binary 파일 조립 ::====
            Assembly.LoadFrom(inputDllForMpc);
            method.Invoke(null, new object[] { outputPath });
            #endregion

            #region ===:: Binary 생성에 필요하지 않은 파일들은 안전하게 마지막에 생성 (의도치 않은 빌드에 포함 방지)::===
            File.WriteAllText(Path.Combine(outputDirectory, "GameDBContainer.cs"), dbContainerSourceCode);
            #endregion

            //------- 빌드 결과에 필요없는 중간 생성 파일 전부 삭제 ------//
            string[] fileCleanList = { "MpcInputProject.csproj", "MpcInput.cs" };
            foreach (var name in fileCleanList)
            {
                string fullPath = Path.Combine(outputDirectory, name);
                if (File.Exists(fullPath)) // 파일이 존재하는지 확인 후 삭제
                {
                    Console.WriteLine($"Deleting File : {fullPath}");
                    File.Delete(fullPath);
                }
            }

            // 직접 삭제해야할 리스트 (어셈블리 언로드 불가능하기 때문에 런타임에 삭제 못함. 파일락)
            /// - GameDBResolver.dll
            /// - bin / obj 폴더 
        }
    }
}
