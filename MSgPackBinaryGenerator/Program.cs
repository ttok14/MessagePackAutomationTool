using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace MSgPackBinaryGenerator
{
    class Program
    {
        public class SheetEntry
        {
            public string path;
            public List<string> lines;

            public string Name => Path.GetFileNameWithoutExtension(path);
            public SheetEntry(string path)
            {
                this.path = path;
                lines = File.ReadAllLines(path).ToList();
            }
        }

        public class DataTable
        {
            public SheetEntry DataSheet { get; private set; }
            public SheetEntry SchemaSheet { get; private set; }

            public TableContainer TableContainer { get; set; }

            public DataTable(string dataPath, string schemaPath)
            {
                DataSheet = new SheetEntry(dataPath);
                SchemaSheet = new SheetEntry(schemaPath);
            }
        }

        public class EnumTable
        {
            public SheetEntry Sheet { get; set; }
            public EnumGroups Groups { get; set; }

            public EnumTable(string path)
            {
                Sheet = new SheetEntry(path);
            }
        }

        static int Main(string[] args)
        {
            string inputDirectory = string.Empty;
            string outputDirectory = string.Empty;

            inputDirectory = Helper.ExtractCommandArgument(args, "-i");
            if (string.IsNullOrEmpty(inputDirectory))
            {
                Console.WriteLine($"사용법 : Program.exe -i [InputDirectory] -o [OutputDirectory]");
                Console.WriteLine($"[InputDirectory]: 테이블/Enum 의 csv 포맷 파일들을 찾을 디렉터리");
                Console.WriteLine($"[OutputDirectory] : 메시지팩 결과물 생성 디렉터리");
                return 1;
            }

            outputDirectory = Helper.ExtractCommandArgument(args, "-o");

            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = Directory.GetCurrentDirectory();
                Console.WriteLine($"결과 파일 생성 디렉토리 자동 설정\n{outputDirectory}");
            }

            List<DataTable> dataTableEntries = Directory.GetFiles(inputDirectory).
                Where(t => t.EndsWith("EnumTable.csv") == false && t.EndsWith("_Schema.csv") == false).
                Select(t => new DataTable(t, t.Replace(".csv", string.Empty) + "_Schema.csv")).
                ToList();
            EnumTable enumTableEntry = new EnumTable(Path.Combine(inputDirectory, "EnumTable.csv"));

            foreach (var dataTable in dataTableEntries)
            {
                string[] dataTableColumns = dataTable.DataSheet.lines[0].Split(',');
                string typesToDataTableColumns = string.Empty;
                foreach (var dataCol in dataTableColumns)
                {
                    for (int i = 1; i < dataTable.SchemaSheet.lines.Count; i++)
                    {
                        string[] keyAndType = dataTable.SchemaSheet.lines[i].Split(',');
                        if (dataCol == keyAndType[0])
                        {
                            typesToDataTableColumns += keyAndType[1] + ",";
                            break;
                        }
                    }
                }

                typesToDataTableColumns = typesToDataTableColumns.Trim(',');

                var tableSchemaDef = new TableSchemaDefinition(dataTable.DataSheet.Name, dataTable.DataSheet.lines[0], typesToDataTableColumns);
                string[] tableDataRaws = dataTable.DataSheet.lines.Skip(1).ToArray();
                dataTable.TableContainer = new TableContainer(tableSchemaDef,
                    new TableDataDefinition(dataTableColumns, tableDataRaws, tableSchemaDef));
            }

            var enumRaws = enumTableEntry.Sheet.lines.Skip(1).ToArray();
            enumTableEntry.Groups = new EnumGroups(enumTableEntry.Sheet.lines[0], enumRaws);

            var enumGenerator = new DBEnumContainer();
            var enumSourceCode = enumGenerator.Generate(enumTableEntry.Groups);

            var dataContainers = dataTableEntries.Select(t => t.TableContainer).ToArray();
            var dbContainerGenerator = new DBContainerGenerator();
            var dbContainerSourceCode = dbContainerGenerator.Generate(dataTableEntries.Select(t => t.TableContainer.SchemaData).ToList(), enumTableEntry.Groups);

            var binaryExporter = new BinaryExporterGeneratorGenerator();
            var binaryGeneratorSourceCode = binaryExporter.Generate(dataContainers, enumTableEntry.Groups);

            var mpcInputGenerator = new MpcInputGenerator();
            var mpcInputSourceCode = mpcInputGenerator.Generate(dataContainers, enumTableEntry.Groups);

            return StartPipeline(
                outputDirectory: outputDirectory,
                dataContainers,
                mpcInputSourceCode: mpcInputSourceCode,
                dbContainerSourceCode: dbContainerSourceCode,
                enumSourceCode: enumSourceCode,
                binaryGeneratorSourceCode: binaryGeneratorSourceCode);
        }
        static int StartPipeline(
            string outputDirectory,
            TableContainer[] dataTableContainers,
            string mpcInputSourceCode,
            string dbContainerSourceCode,
            string enumSourceCode,
            string binaryGeneratorSourceCode)
        {
            Console.WriteLine($"OutputDirectory : {outputDirectory}");

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            outputDirectory = $"{outputDirectory}/MsgPackResult_{timestamp}";
            Directory.CreateDirectory(outputDirectory);

            // .cs 파일을 만듬 (여기에 mpc 에서 Formatter 생성할때 필요한 클래스들이 위치)
            string srcPath = Path.Combine(outputDirectory, "MpcInput.cs");
            File.WriteAllText(srcPath, mpcInputSourceCode);

            //Console.WriteLine($"Saved source code to: {srcPath}");

            string mpcInputProjectName = "MpcInputProject";
            string gameDBResolverName = "GameDBResolver";

            // mpc 가 resolver 를 만들때 필요한 .csproj 및 dll 을 생성
            string inputProjectForMpc = CsprojGenerator.GenerateProject(outputDirectory, mpcInputProjectName);
            // 여기서 이제 .csproj를 보고 컴파일을해 어셈블리를 생성 
            // ** 이때 , 중요한거는 .NET SDK 버전 이후로 (이전에는 non-sdk, e.g .Net Framework)
            // .csproj 를 빌드할때는 .csproj 에 직접 <Compile Include="ItemTable.cs" />.. 이런식으로
            // 추가하지 않아도 동일 경로 + 하위 경로들에 위치한 모든 .cs 파일을 빌드에 포함시킨다고 함. 
            // 이런 이유로 위에서 만든 MpcInput.cs 파일을 직접적으로 이 빌드에 연결시키지 않고 그냥 파일을
            // 동일 디렉터리에 생성한 것 만으로도 빌드에 포함되는 것 . 
            string inputDllForMpc = DotnetBuilder.Build(inputProjectForMpc);

            // Resolver 생성 
            string resolveOutput = Path.Combine(outputDirectory, $"{gameDBResolverName}.cs");

            // mpc 도 내부적으로 MSBuild 로 빌드를 하기 때문에 
            // 이 시점 이전에 의도치않은 .cs 파일 생성은 주의해야함 (e.g GameDBContainer.cs)
            MpcRunner.Run(inputProjectForMpc, resolveOutput);

            // Console.WriteLine("-------------------------");

            var resolverSourceCode = File.ReadAllText(resolveOutput);

            // Resolver 어셈블리 필요. 컴파일해둠.
            var resolverAssembly = RuntimeCompiler.CompileSource(
                resolverSourceCode,
                new[] { inputDllForMpc }
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
                    // 클래스/Enum 들 참조
                    inputDllForMpc,
                    // Serialize 할때 Resolver 참조
                    resolverAssembly.Location,
                    // 향후 확장성 고려해서 일단 지금 어셈도 추가
                    currentDllPath
                });

            var assemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { mpcInputProjectName, inputDllForMpc },
                { gameDBResolverName, resolverAssembly.Location }
            };

            // 만약 다른 어셈블리가 참조하려는 어셈블리가 존재하지 않는다면 
            // 최종적으로 오류를 발생시키기 전 이 이벤트 콜백이 호출됨.
            // 이 상황을 대비해서 여기에 연결해놓고 만약 런타임에 어셈블리가
            // 요청되면 여기서 현재 실행 어셈블리에 로드하는 방식으로 대응
            ResolveEventHandler assemblyResolver = (sender, args) =>
            {
                string assemblyName = new AssemblyName(args.Name).Name;
                if (assemblyPaths.TryGetValue(assemblyName, out string path))
                {
                    return Assembly.LoadFrom(path);
                }
                return null;
            };

            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolver;

            var type = binaryExporterAssembly.GetType("GameDB.TableBinaryExporter");
            if (type == null)
            {
                Console.WriteLine("❌ TableBinaryExporter 타입을 찾을 수 없습니다.");
                return 1;
            }

            try
            {
                // 1. GameDBResolver.Instance를 리플렉션으로 가져옵니다.
                var gameDBResolverType = resolverAssembly.GetType("GameDB.Resolvers.GameDBContainerResolver");
                var gameDBResolverInstance = (IFormatterResolver)gameDBResolverType.GetField("Instance").GetValue(null);

                // 2. 각 리졸버를 LoggingResolver로 감싸서 디버깅 로그를 출력하도록 합니다.
                var loggingGameDBResolver = new LoggingResolver(gameDBResolverInstance, "GameDBResolver");
                var loggingStandardResolver = new LoggingResolver(StandardResolver.Instance, "StandardResolver");

                // 3. 로그를 출력하는 리졸버들을 조합합니다.
                var resolver = CompositeResolver.Create(
                    loggingGameDBResolver,
                    loggingStandardResolver
                );

                // 4. 이 리졸버를 포함하는 새로운 옵션 객체를 만듭니다.
                var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
                string binaryOutputDirectory = Path.Combine(outputDirectory, "binaries");
                Directory.CreateDirectory(binaryOutputDirectory);

                foreach (var container in dataTableContainers)
                {
                    var method = type.GetMethod($"Export{container.SchemaData.TableName}", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(MessagePackSerializerOptions) }, null);
                    if (method == null)
                    {
                        Console.WriteLine($"❌ Export{container.SchemaData.TableName} 메서드를 찾을 수 없습니다.");
                        return 1;
                    }
                    else
                    {
                        Console.WriteLine("Method Found : " + method.Name);
                    }

                    // 5. 생성된 옵션을 메서드에 파라미터로 전달합니다.
                    string binaryOutputPath = Path.Combine(binaryOutputDirectory, $"{container.SchemaData.TableName}.bin");

                    Console.WriteLine("\n--- Starting Serialization ---\n");
                    method.Invoke(null, new object[] { binaryOutputPath, options });
                    Console.WriteLine("\n--- Serialization Finished ---\n");
                }

                // 메타데이터 생성하기
                var metadata = new MetadataGenerator().Generate(binaryOutputDirectory);
                var jsonOptions = new JsonSerializerOptions() { WriteIndented = true };
                var jsonString = JsonSerializer.Serialize(metadata, jsonOptions);
                var jsonOutputDirectory = Path.Combine(outputDirectory, "table_metadata.json");
                File.WriteAllText(jsonOutputDirectory, jsonString);
                Console.WriteLine($"Metadata (JSON) 저장 완료 : {jsonOutputDirectory}");
                Console.WriteLine();
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolver;
            }

            #region ===:: Binary 생성에 필요하지 않은 아티팩트 파일 생성 (의도치 않은 빌드에 포함 방지)::===
            File.WriteAllText(Path.Combine(outputDirectory, "GameDBContainer.cs"), dbContainerSourceCode);
            File.WriteAllText(Path.Combine(outputDirectory, "BinaryExporter.cs"), binaryGeneratorSourceCode);
            File.WriteAllText(Path.Combine(outputDirectory, "GameDBEnums.cs"), enumSourceCode);
            #endregion

            foreach (var file in Directory.GetFiles(outputDirectory))
            {
                Console.WriteLine($"최종 파일들 : {file}");
            }

            return 0;
        }
    }
}