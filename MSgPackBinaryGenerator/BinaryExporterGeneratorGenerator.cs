using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using UnityEngine;

namespace MSgPackBinaryGenerator
{
    public class BinaryExporterGeneratorGenerator
    {
        CodeStringBuilder _builder = new CodeStringBuilder(2048);

        public override string ToString()
        {
            return _builder.Current.ToString();
        }

        public string Generate(TableContainer[] tableContainer, EnumGroups enumGroup)
        {
            _builder.AppendLine("using System;");
            _builder.AppendLine("using System.Collections.Generic;");
            _builder.AppendLine("using System.IO;");
            _builder.AppendLine("using MessagePack;");
            _builder.AppendLine("using MessagePack.Resolvers;");
            _builder.AppendLine("using System.Buffers;");
            _builder.AppendLine("using UnityEngine;");
            _builder.AppendLine();
            _builder.AppendLine("namespace GameDB");
            _builder.OpenBracket();
            {
                _builder.AppendLine("public static class TableBinaryExporter");
                _builder.OpenBracket();
                {
                    foreach (var container in tableContainer)
                    {
                        _builder.AppendLine($"public static void Export{container.SchemaData.TableName}(string outputPath, MessagePackSerializerOptions options)");
                        _builder.OpenBracket();
                        {
                            _builder.AppendLine($"var dic = new Dictionary<{container.SchemaData.IDType}, {container.SchemaData.TableName}>");
                            _builder.OpenBracket();
                            {
                                // 데이터 1줄씩(raw) 순회 
                                foreach (var element in container.TableData.DataList)
                                {
                                    for (int i = 0; i < element.Records.Count; i++)
                                    {
                                        bool isID = i == 0;
                                        var record = element.Records[i];
                                        bool isArray = record.SchemaData.IsArray;
                                        string value = Helper.ValueStringToCode(record.SchemaData.TypeName, record.Value,
                                            isEnumFlagChecker:
                                            (rawTypeName) =>
                                            {
                                                if (enumGroup.Enums.ContainsKey(rawTypeName) == false)
                                                {
                                                    Console.WriteLine($"Given rawTypeName does not exist in enum group : {rawTypeName}");
                                                    return false;
                                                }
                                                return enumGroup.Enums[rawTypeName].IsFlags;
                                            });

                                        if (isArray)
                                        {
                                            // e.g new Int[] { 1,2,3 
                                            value = $"new {record.SchemaData.TypeName} {{ " + value + $"}}";
                                        }

                                        if (isID)
                                        {
                                            // e.g [2] = new ItemTable { 
                                            // e.g [2] = new int[] {
                                            _builder.AppendIndent($"[{value}] = new {container.SchemaData.TableName} {{ ");
                                        }

                                        // e.g [2] = new ItemTable { ID = 2, ...
                                        // e.g [2] = new ItemTable { IdList = new int() { 1,2,3 },
                                        _builder.Append($"{record.SchemaData.Name} = {value},");
                                    }

                                    _builder.Append($"}},");
                                    _builder.AppendLine();
                                }
                            }
                            _builder.CloseBracket(true);

                            _builder.AppendLine();
                            _builder.AppendLine("var bufferWriter = new ArrayBufferWriter<byte>();");
                            _builder.AppendLine("var msgPackWriter = new MessagePackWriter(bufferWriter);");

                            _builder.AppendLine($"msgPackWriter.Write(dic.Count);");

                            _builder.AppendLine("foreach (var kv in dic)");
                            _builder.OpenBracket();
                            {
                                _builder.AppendLine("MessagePackSerializer.Serialize(ref msgPackWriter, kv.Value, options);");
                            }
                            _builder.CloseBracket();

                            _builder.AppendLine($"msgPackWriter.Flush();");

                            _builder.AppendLine("byte[] bytes = bufferWriter.WrittenSpan.ToArray();");

                            _builder.AppendLine("File.WriteAllBytes(outputPath, bytes);");
                            _builder.AppendLine($"Console.WriteLine($\"Binary size: {{bytes.Length}} bytes\");");
                        }
                        _builder.CloseBracket();
                        _builder.AppendLine();
                    }
                }
                _builder.CloseBracket();
            }
            _builder.CloseBracket();

            return _builder.Current.ToString();
        }
    }
}