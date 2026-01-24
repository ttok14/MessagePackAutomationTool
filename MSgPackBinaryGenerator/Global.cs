using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public enum Platform
    {
        Unity = 0,
        Native,
    }

    public class DeploymentInfo
    {
        public const string METADATA_COPY_PATH_KEY = "METADATA_COPY_TO";
        public const string COMPONENT_DEPLOYMENT_COPY_PATH_KEY = "COMPONENT_COPY_TO";
        public const string BINARY_DEPLOYMENT_COPY_PATH_KEY = "BINARY_COPY_TO";

        public string MetadataDeploymentCopyPath;
        public string ComponentsDeploymentCopyPath;
        public string BinariesDeploymentCopyPath;
    }

    public static class Global
    {
        public static Platform CurrentPlatform = Platform.Unity;

        public static DeploymentInfo Config = new DeploymentInfo();

        public static Dictionary<string, List<TableSchemaDefinition>> TableSchemaByTableName { get; set; }
        public static Dictionary<string, List<EnumGroups>> EnumSchemaByEnumName { get; set; }
        public static string GameDBContainerSourceCode { get; set; }
        public static string EnumDBSourceCode { get; set; }
    }
}
