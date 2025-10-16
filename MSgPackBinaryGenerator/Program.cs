using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MSgPackBinaryGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var itemTableCsv = File.ReadAllLines(@"C:\Users\LeeYunSeon\source\repos\MSgPackBinaryGenerator\MSgPackBinaryGenerator\Data\ItemTable.csv");
            var itemTableSchemaCsv = File.ReadAllLines(@"C:\Users\LeeYunSeon\source\repos\MSgPackBinaryGenerator\MSgPackBinaryGenerator\Data\ItemTable_Schema.csv");

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

            var classDefinitions = new List<TableSchemaDefinition>();
            var itemTableDef = new TableSchemaDefinition("ItemTable", itemTableCsv[0], itemTableTypes);
            classDefinitions.Add(itemTableDef);

            var itemTableCsvList = itemTableCsv.ToList();
            itemTableCsvList.RemoveAt(0);
            string[] itemDataRaws = itemTableCsvList.ToArray();


            var itemTableDataGroup = new TableDataDefinition(itemDataTableColumns, itemDataRaws, itemTableDef);

            var enumCsv = File.ReadAllLines(@"C:\Users\LeeYunSeon\source\repos\MSgPackBinaryGenerator\MSgPackBinaryGenerator\Data\EnumTable.csv");
            var raws = enumCsv.ToList();
            raws.RemoveAt(0);
            var enumGroup = new EnumGroups(enumCsv[0], raws.ToArray());

            var enumGenerator = new DBEnumContainer();
            var enumSourceCode = enumGenerator.Generate(enumGroup);
            Console.WriteLine(enumSourceCode);

            var generator = new DBContainerGenerator();
            var dbContainerSourceCode = generator.Generate(new List<TableSchemaDefinition>() { itemTableDef }, enumGroup, includeUnitySupport: true);

            Console.WriteLine("--------------------------------------------------");
            var binaryExporter = new BinaryExporterGeneratorGenerator();
            var itemTableContainer = new TableContainer(classDefinitions[0], itemTableDataGroup);
            var tableContainer = new List<TableContainer>() { itemTableContainer };
            var binaryGeneratorSourceCode = binaryExporter.Generate(tableContainer, enumGroup);

            var mpcInputGenerator = new MpcInputGenerator();
            var mpcInputSourceCode = mpcInputGenerator.Generate(tableContainer, enumGroup);

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

            Console.WriteLine("-------------------------");

            // Resolver 생성 
            string resolveOutput = Path.Combine(outputDirectory, $"{gameDBResolverName}.cs");

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
                new[] { inputDllForMpc },
                outputDirectory,
                $"{gameDBResolverName}.dll"
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

            var type = binaryExporterAssembly.GetType("GameDB.TableBinaryExporter");
            if (type == null)
            {
                Console.WriteLine("❌ TableBinaryExporter 타입을 찾을 수 없습니다.");
                return;
            }

            var method = type.GetMethod("ExportItemTable", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(MessagePackSerializerOptions) }, null);
            if (method == null)
            {
                Console.WriteLine("❌ ExportItemTable 메서드를 찾을 수 없습니다.");
                return;
            }
            else
            {
                Console.WriteLine("Method Found : " + method.Name);
            }

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

                // 5. 생성된 옵션을 메서드에 파라미터로 전달합니다.
                string binaryOutputPath = Path.Combine(outputDirectory, "ItemTable.bytes");

                Console.WriteLine("\n--- Starting Serialization ---\n");
                method.Invoke(null, new object[] { binaryOutputPath, options });
                Console.WriteLine("\n--- Serialization Finished ---\n");
                // ========================= ▲▲▲ 추가된 부분 2 ▲▲▲ =========================
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolver;
            }

            #region ===:: Binary 생성에 필요하지 않은 파일들은 안전하게 마지막에 생성 (의도치 않은 빌드에 포함 방지)::===
            File.WriteAllText(Path.Combine(outputDirectory, "GameDBContainer.cs"), dbContainerSourceCode);
            File.WriteAllText(Path.Combine(outputDirectory, "BinaryExporter.cs"), binaryGeneratorSourceCode);
            #endregion
        }
    }
}