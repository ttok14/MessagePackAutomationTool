using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public class DBContainerGenerator
    {
        CodeStringBuilder _builder = new CodeStringBuilder(2048);

        public override string ToString()
        {
            return _builder.Current.ToString();
        }

        public string Generate(List<TableSchemaContents> schemaData)
        {
            _builder.AppendLine("//*** Auto Generation Code ***");
            _builder.AppendLine();
            _builder.AppendLine("using System;");
            _builder.AppendLine("using System.Collections.Generic;");
            _builder.AppendLine("using UnityEngine;");
            _builder.AppendLine("using MessagePack;");
            _builder.AppendLine();
            _builder.AppendLine("namespace GameDB");
            _builder.OpenBracket();
            {
                _builder.AppendLine("public class GameDBContainer");
                _builder.OpenBracket();
                {
                    // 필드 추가
                    //   e.g public Dictionary<uint, Ability_Table> Ability_Table_data = new Dictionary<uint, Ability_Table>();

                    for (int i = 0; i < schemaData.Count; i++)
                    {
                        _builder.AppendLine($"public Dictionary<{schemaData[i].IDType}, {schemaData[i].TableName}> {schemaData[i].TableName}_data = new Dictionary<{schemaData[i].IDType}, {schemaData[i].TableName}>();");
                        _builder.AppendLine();
                    }

                }
                _builder.CloseBracket();

                _builder.AppendLine();

                // MessagePack 클래스들 정의

                for (int i = 0; i < schemaData.Count; i++)
                {
                    var schema = schemaData[i];

                    _builder.AppendLine("[MessagePackObject]");
                    _builder.AppendLine($"public class {schema.TableName}");
                    _builder.OpenBracket();
                    {
                        for (int j = 0; j < schema.Fields.Count; j++)
                        {
                            var field = schema.Fields[j];
                            _builder.AppendLine($"[Key({j})]");
                            _builder.AppendLine($"public {field.TypeName} {field.Name}");
                        }

                        _builder.AppendLine();

                        _builder.AppendLine("[UnityEngine.Scripting.Preserve]");
                        _builder.AppendLine($"public static Dictionary<{schema.IDType}, {schema.TableName}> Deserialize(ref byte[] _readBytes)");
                        _builder.OpenBracket();
                        {
                            _builder.AppendLine($"Dictionary<{schema.IDType}, {schema.TableName}> dicTables = new Dictionary<{schema.IDType}, {schema.TableName}>();");
                            _builder.AppendLine($"MessagePackReader reader = new MessagePackReader(new System.ReadOnlyMemory<byte>(_readBytes));");
                            _builder.AppendLine("int tableCount = MessagePackSerializer.Deserialize<int>(ref reader);");
                            _builder.AppendLine("for (int i = 0; i < tableCount; i++)");
                            _builder.OpenBracket();
                            {
                                _builder.AppendLine($"var table = MessagePackSerializer.Deserialize<{schema.TableName}>(ref reader);");

                                _builder.AppendLine($"dicTables.Add(table.{schema.IDName}, table);");
                            }
                            _builder.CloseBracket();

                            _builder.AppendLine("return dicTables;");
                        }
                        _builder.CloseBracket();
                    }
                    _builder.CloseBracket();
                }
            }
            _builder.CloseBracket();

            return _builder.Current.ToString();
        }
    }
}