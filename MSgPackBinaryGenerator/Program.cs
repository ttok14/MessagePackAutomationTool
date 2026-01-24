using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Unity;
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
                Console.WriteLine($"사용법 : Program.exe -i [InputDirectory] -o [OutputDirectory] -p [Platform]");
                Console.WriteLine($"[InputDirectory]: 테이블/Enum 의 csv 포맷 파일들을 찾을 디렉터리");
                Console.WriteLine($"[OutputDirectory] : 메시지팩 결과물 생성 디렉터리");
                return 1;
            }

            outputDirectory = Helper.ExtractCommandArgument(args, "-o");

            if (string.IsNullOrEmpty(outputDirectory))
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), $"Result_{timestamp}");
                Console.WriteLine($"결과 파일 생성 디렉토리 자동 설정\n{outputDirectory}");
            }

            string strPlatform = Helper.ExtractCommandArgument(args, "-p");
            if (string.IsNullOrEmpty(strPlatform))
            {
                Console.WriteLine($"플랫폼 자동 설정 -> Unity");
                Global.CurrentPlatform = Platform.Unity;
            }
            else
            {
                if (Enum.TryParse(strPlatform, out Global.CurrentPlatform) == false)
                {
                    Console.WriteLine($"주어진 플랫폼이 유효하지 않습니다. ({strPlatform})");
                    Console.WriteLine("사용 가능 플랫폼 리스트 : " + string.Join(",", Enum.GetValues(typeof(Platform)).Cast<Platform>()));
                    return 55;
                }
            }

            SetupConfig();

            Console.WriteLine($"입력 데이터 경로 : {inputDirectory}");
            Console.WriteLine($"출력 데이터 경로 : {outputDirectory}");
            Console.WriteLine($"플랫폼 : {Global.CurrentPlatform}");

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

                //var tableSchemaDef = new TableSchemaDefinition(dataTable.DataSheet.Name, dataTable.DataSheet.lines[0], typesToDataTableColumns);
                //string[] tableDataRaws = dataTable.DataSheet.lines.Skip(1).ToArray();
                //dataTable.TableContainer = new TableContainer(tableSchemaDef,
                //    new TableDataDefinition(dataTableColumns, tableDataRaws, tableSchemaDef));

                var tableSchemaDef = new TableSchemaDefinition(dataTable.DataSheet.Name, dataTable.DataSheet.lines[0], typesToDataTableColumns);
                dataTable.TableContainer = new TableContainer(tableSchemaDef,
                    new TableDataDefinition(dataTable.DataSheet.path, dataTableColumns, tableSchemaDef));
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

        static void SetupConfig()
        {
            // 어셈블리 위치에 Config.txt 파일 위치시킴됨
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.txt");

            if (File.Exists(configFilePath) == false)
                return;

            Console.WriteLine($"■■■■ Config 파일 로드합니다. ■■■");

            string[] configLines = File.ReadAllLines(configFilePath);

            for (int i = 0; i < configLines.Length; i++)
            {
                var split = configLines[i].Split('=');
                if (split.Length != 2)
                {
                    Console.WriteLine($"Config 유효하지 않은 라인 검출(Key=Value 형식이어야 합니다) : {configLines[i]}");
                    continue;
                }

                var key = split[0];
                // 앞뒤에 붙은 스페이스와 따옴표만 삭제
                var value = split[1].Trim().Trim('"');

                // 파싱 
                if (key == DeploymentInfo.COMPONENT_DEPLOYMENT_COPY_PATH_KEY)
                    Global.Config.ComponentsDeploymentCopyPath = value;
                else if (key == DeploymentInfo.BINARY_DEPLOYMENT_COPY_PATH_KEY)
                    Global.Config.BinariesDeploymentCopyPath = value;
                else if (key == DeploymentInfo.METADATA_COPY_PATH_KEY)
                    Global.Config.MetadataDeploymentCopyPath = value;
            }
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

            string errorDebugDirectory = Path.Combine(outputDirectory, "ErrorReport");

            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(errorDebugDirectory);

            // .cs 파일을 만듬 (여기에 mpc 에서 Formatter 생성할때 필요한 클래스들이 위치)
            string srcPath = Path.Combine(outputDirectory, "MpcInput_Artifact.cs");
            File.WriteAllText(srcPath, mpcInputSourceCode);

            //Console.WriteLine($"Saved source code to: {srcPath}");

            string mpcInputProjectName = "MpcInputProject";
            string gameDBResolverName = "GameDBResolver";

            // mpc 가 resolver 를 만들때 필요한 .csproj 및 dll 을 생성
            string inputProjectForMpc = CsprojGenerator.GenerateProject(outputDirectory, mpcInputProjectName);

            // 여기서 이제 .csproj를 보고 컴파일을해 어셈블리를 생성 

            // **
            //  참고 1. .NET SDK 버전 이후로 (이전에는 non-sdk, e.g .Net Framework)
            //      .csproj 를 빌드할때는 .csproj 에 직접 <Compile Include="ItemTable.cs" />.. 이런식으로
            //      추가하지 않아도 동일 경로 + 하위 경로들에 위치한 모든 .cs 파일을 빌드에 포함시킨다고 함. 
            //      이런 이유로 ■■ 위에서 만든 MpcInput.cs 파일 ■■ 을 직접적으로 이 빌드에 연결시키지 않고 그냥 파일을
            //      동일 디렉터리에 생성한 것 만으로도 빌드에 포함되는 것 . 

            //  참고 2. 만약 여기서 Build 에러가 나면 mpc 에 인자로 들어간 소스코드 (MpcInputGenerator 가 생성한 코드) 가
            //      참조 이슈가 있다거나 하는 경우가 잦음 (e.g MpcInputGenerator 에서 Unity 타입을 사용하려는데 참조를 못하고있거나
            //      using UnityEngine; 문을 빠뜨렸거나 등..)

            string inputDllForMpc = string.Empty;
            try
            {
                inputDllForMpc = DotnetBuilder.Build(inputProjectForMpc);
            }
            catch (Exception exp)
            {
                Console.WriteLine($"■■■■ 여기서 에러가 났다면, mpc 가 MpcInput 코드를 읽다가 컴파일 에러났을 확률이 높음 (잘못된 테이블 데이터 등으로 인해) 먼저, 생성된 MpcInput_Artifact.cs 파일을 확인 및 오류문을 잘 보고 테이블 데이터 등 재점검 바람 ■■■■ Exception : {exp.Message}");
                return 2;
            }

            // Resolver 생성 
            string resolverOutput = Path.Combine(outputDirectory, $"{gameDBResolverName}.cs");

            // mpc 도 내부적으로 MSBuild 로 빌드를 하기 때문에 
            // 이 시점 이전에 의도치않은 .cs 파일 생성은 주의(자동빌드포함)해야함 (e.g GameDBContainer.cs)
            MpcRunner.Run(inputProjectForMpc, resolverOutput);

            // Console.WriteLine("-------------------------");

            var resolverSourceCode = File.ReadAllText(resolverOutput);
            Assembly resolverAssembly = null;
            bool err = false;

            // 실제로 바이너리로 messagePack 내 테이블 데이터들을 Serialize 하려면 
            // Resolver 가 필요, 즉 내 테이블을 인자로 읽어서 mpc 가 생성한 전용 Resolver 를 어셈블리 필요함. 컴파일해둠.
            resolverAssembly = RuntimeCompiler.CompileSource(
                "■■■■■■■ Resolver Compile ■■■■■■",
                resolverSourceCode,
                new[] { inputDllForMpc },
                onError: (msg) =>
                {
                    Console.WriteLine(msg);
                    err = true;
                });

            if (err)
            {
                File.WriteAllText(Path.Combine(errorDebugDirectory, $"{gameDBResolverName}.cs"), resolverSourceCode);
                File.WriteAllText(Path.Combine(errorDebugDirectory, "GameDBContainer.cs"), dbContainerSourceCode);
                File.WriteAllText(Path.Combine(errorDebugDirectory, "GameDBEnums.cs"), enumSourceCode);
                return 3;
            }

            var currentDllPath = Assembly.GetExecutingAssembly().Location;

            // !참고로 mpc 가 만든 Resolver 는 내부적으로 이런식으로 global::GameDB.E_ItemType) mpcInput 을 참조중임!
            // 즉 이게 무슨 말이냐면 , Resolver 어셈블리를 로드하는 순간 Resolver 가 의존하는 mpcInput 어셈블리도 로드를 한다는 의미.
            // 그렇기에 다음 코드의 additionalReference 에 inputDllForMpc 을 추가하면 
            // 그 안에 중복이 생기기때문에 에러가 발생함.
            Assembly binaryExporterAssembly = null;
            binaryExporterAssembly = RuntimeCompiler.CompileSource(
                "■■■■■■ Binary Exporter Compile ■■■■■■",
                binaryGeneratorSourceCode,
                new string[]
                {
                    // 클래스/Enum 들 참조
                    inputDllForMpc,
                    // Serialize 할때 Resolver 참조
                    resolverAssembly.Location,
                    // 향후 확장성 고려해서 일단 지금 어셈도 추가
                    currentDllPath
                }, onError: (msg) =>
                {
                    Console.WriteLine(msg);
                    err = true;
                });

            if (err)
            {
                File.WriteAllText(Path.Combine(errorDebugDirectory, $"{gameDBResolverName}.cs"), resolverSourceCode);
                File.WriteAllText(Path.Combine(errorDebugDirectory, "GameDBContainer.cs"), dbContainerSourceCode);
                File.WriteAllText(Path.Combine(errorDebugDirectory, "GameDBEnums.cs"), enumSourceCode);
                File.WriteAllText(Path.Combine(errorDebugDirectory, "BinaryExporter_Artifact.cs"), binaryGeneratorSourceCode);
                return 4;
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

            var type = binaryExporterAssembly.GetType("GameDB.TableBinaryExporter");
            if (type == null)
            {
                Console.WriteLine("❌ TableBinaryExporter 타입을 찾을 수 없습니다.");
                return 5;
            }

            string binaryOutputDirectory = "";
            string metadataPath = "";

            try
            {
                // 1. GameDBResolver.Instance를 리플렉션으로 가져옵니다.
                var gameDBResolverType = resolverAssembly.GetType("GameDB.Resolvers.GameDBContainerResolver");
                var gameDBResolverInstance = (IFormatterResolver)gameDBResolverType.GetField("Instance").GetValue(null);

                // 2. 각 리졸버를 LoggingResolver로 감싸서 디버깅 로그를 출력하도록 합니다.
                var loggingUnityResolver = new LoggingResolver(UnityResolver.Instance, "UnityResolver");
                var loggingGameDBResolver = new LoggingResolver(gameDBResolverInstance, "GameDBResolver");
                var loggingStandardResolver = new LoggingResolver(StandardResolver.Instance, "StandardResolver");

                // 3. 로그를 출력하는 리졸버들을 조합합니다.
                MyCompositeResolver.Instance.Register(
                    loggingUnityResolver,
                    loggingGameDBResolver,
                    loggingStandardResolver
                );

                // 4. 이 리졸버를 포함하는 새로운 옵션 객체를 만듭니다.
                var options = MessagePackSerializerOptions.Standard.WithResolver(MyCompositeResolver.Instance);
                MessagePackSerializer.DefaultOptions = options;
                binaryOutputDirectory = Path.Combine(outputDirectory, "binaries");
                Directory.CreateDirectory(binaryOutputDirectory);

                foreach (var container in dataTableContainers)
                {
                    var method = type.GetMethod($"Export{container.SchemaData.TableName}", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(MessagePackSerializerOptions) }, null);
                    if (method == null)
                    {
                        Console.WriteLine($"❌ Export{container.SchemaData.TableName} 메서드를 찾을 수 없습니다.");
                        return 6;
                    }
                    else
                    {
                        Console.WriteLine("Method Found : " + method.Name);
                    }

                    // 5. 생성된 옵션을 메서드에 파라미터로 전달합니다.
                    string binaryOutputPath = Path.Combine(binaryOutputDirectory, $"{container.SchemaData.TableName}.{Constants.BinaryOutputExtension}");

                    Console.WriteLine("\n--- Starting Serialization ---\n");
                    method.Invoke(null, new object[] { binaryOutputPath, options });
                    Console.WriteLine("\n--- Serialization Finished ---\n");
                }

                // 메타데이터 생성하기
                var metadata = new MetadataGenerator().Generate(binaryOutputDirectory);
                var jsonOptions = new JsonSerializerOptions() { WriteIndented = true };
                var metadataJson = JsonSerializer.Serialize(metadata, jsonOptions);
                metadataPath = Path.Combine(outputDirectory, "table_metadata.json");
                File.WriteAllText(metadataPath, metadataJson);
                Console.WriteLine($"Metadata (JSON) 저장 완료 : {metadataPath}");
                Console.WriteLine();
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolver;
            }

            string mainComponentsDirectory = Path.Combine(outputDirectory, "components");

            Directory.CreateDirectory(mainComponentsDirectory);

            #region ===:: Binary 생성에 필요하지 않은 아티팩트 파일 생성 (의도치 않은 빌드에 포함 방지)::===
            File.Move(resolverOutput, Path.Combine(mainComponentsDirectory, $"{gameDBResolverName}.cs"));
            File.WriteAllText(Path.Combine(mainComponentsDirectory, "GameDBContainer.cs"), dbContainerSourceCode);
            File.WriteAllText(Path.Combine(mainComponentsDirectory, "GameDBEnums.cs"), enumSourceCode);
            File.WriteAllText(Path.Combine(outputDirectory, "BinaryExporter_Artifact.cs"), binaryGeneratorSourceCode);
            #endregion

            foreach (var file in Directory.GetFiles(outputDirectory))
            {
                Console.WriteLine($"최종 파일들 : {file}");
            }

            RunDeployments(outputDirectory, mainComponentsDirectory, binaryOutputDirectory, metadataPath);

            return 0;
        }

        private static void RunDeployments(string outputDirectory, string mainComponentsDirectory, string binaryOutputDirectory, string metadataPath)
        {
            if (string.IsNullOrEmpty(Global.Config.ComponentsDeploymentCopyPath) &&
                string.IsNullOrEmpty(Global.Config.MetadataDeploymentCopyPath) &&
                string.IsNullOrEmpty(Global.Config.BinariesDeploymentCopyPath))
            {
                return;
            }

            Console.WriteLine($"■■■■ 최종 자동 배포를 시도합니다. ■■■");

            if (string.IsNullOrEmpty(Global.Config.ComponentsDeploymentCopyPath) == false)
                Deploy(mainComponentsDirectory,
                    Global.Config.ComponentsDeploymentCopyPath,
                    ".cs",
                    DeploymentInfo.COMPONENT_DEPLOYMENT_COPY_PATH_KEY,
                    isFile: false);

            if (string.IsNullOrEmpty(Global.Config.BinariesDeploymentCopyPath) == false)
                Deploy(binaryOutputDirectory,
                    Global.Config.BinariesDeploymentCopyPath,
                    $".{Constants.BinaryOutputExtension}",
                    DeploymentInfo.BINARY_DEPLOYMENT_COPY_PATH_KEY,
                    isFile: false);

            if (string.IsNullOrEmpty(Global.Config.MetadataDeploymentCopyPath) == false)
                Deploy(metadataPath,
                    Global.Config.MetadataDeploymentCopyPath,
                    ".json",
                    DeploymentInfo.METADATA_COPY_PATH_KEY,
                    isFile: true);
        }

        private static void Deploy(string srcPath, string dstDir, string extension, string debugKeyInfo, bool isFile)
        {
            Console.WriteLine($"배포 시도중 | 원본 패스 : {srcPath}, 대상 패스 : {dstDir}, 파일 or 폴더 : {isFile}");

            // 배포 타겟 위치에 이미 해당 확장자의 파일들은
            // Old Version 으로 간주하고 삭제 (안정성 문제로 최상위 디렉터리의
            // 바로 아래만 삭제)
            if (Directory.Exists(dstDir))
            {
                foreach (var existingPath in Directory.GetFiles(dstDir, "*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(existingPath).ToLower() == extension.ToLower())
                        File.Delete(existingPath);
                }
            }

            if (isFile)
            {
                if (File.Exists(srcPath) == false)
                {
                    Console.WriteLine($"배포 실패 (Key정보 : {debugKeyInfo}), 원본 패스가 유효하지 않음 | SrcPath : {srcPath}");
                    return;
                }

                if (Directory.Exists(dstDir) == false)
                    Directory.CreateDirectory(dstDir);

                try
                {
                    File.Copy(srcPath, Path.Combine(dstDir, Path.GetFileName(srcPath)), overwrite: true);
                }
                catch (Exception exp)
                {
                    Console.WriteLine($"파일 배포작업 실패 | 예외 : {exp.ToString()}");
                }
            }
            else
            {
                if (Directory.Exists(srcPath) == false)
                {
                    Console.WriteLine($"배포 실패 (Key정보 : {debugKeyInfo}), 원본 패스가 유효하지 않음 | SrcPath : {srcPath}");
                    return;
                }

                if (Directory.Exists(dstDir) == false)
                    Directory.CreateDirectory(dstDir);

                foreach (var subDir in Directory.GetDirectories(srcPath, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(subDir.Replace(srcPath, dstDir));
                }

                try
                {
                    // 카피 대상에서 파일 경로들을(하위 폴더가 존재한다면 모두 포함)을 모두 순회
                    foreach (var path in Directory.GetFiles(srcPath, "*", SearchOption.AllDirectories))
                    {
                        File.Copy(path, path.Replace(srcPath, dstDir), overwrite: true);
                    }
                }
                catch (Exception exp)
                {
                    Console.WriteLine($"작업 실패 (File 배포중) | 예외 : {exp.ToString()}");
                }
            }
        }
    }
}