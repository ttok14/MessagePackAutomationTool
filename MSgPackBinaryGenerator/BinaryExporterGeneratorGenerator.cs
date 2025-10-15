using System;
using System.Collections.Generic;
using System.Text;

namespace MSgPackBinaryGenerator
{
    public class BinaryExporterGeneratorGenerator
    {
        CodeStringBuilder _builder = new CodeStringBuilder(2048);

        public override string ToString()
        {
            return _builder.Current.ToString();
        }

        public string Generate(List<TableContainer> tableContainer, EnumGroups enumGroup)
        {
            _builder.AppendLine("using System;");
            _builder.AppendLine("using System.Collections.Generic;");
            _builder.AppendLine("using System.IO;");
            _builder.AppendLine("using MessagePack;");
            _builder.AppendLine();
            _builder.AppendLine("namespace GameDB");
            _builder.OpenBracket();
            {
                _builder.AppendLine(enumGroup.ToSourceCode(EnumGroupsSourceCodeForm.Groups));

                foreach (var container in tableContainer)
                {
                    _builder.AppendLine();
                    _builder.AppendLine(container.SchemaData.ToSourceCode(DataTableSourceCodeForm.DeclarationOnlyFields));
                    _builder.AppendLine();
                }

                _builder.AppendLine("public static class TableBinaryExporter");
                _builder.OpenBracket();
                {
                    foreach (var container in tableContainer)
                    {
                        _builder.AppendLine($"public static void Export{container.SchemaData.TableName}(string outputPath)");
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
                                        string value = record.Value;

                                        if (record.SchemaData.Type == DataRecordDataType.Enum)
                                        {
                                            value = $"{record.SchemaData.TypeName}.{value}";
                                        }
                                        else if (record.SchemaData.Type == DataRecordDataType.String)
                                        {
                                            value = $"\"{value}\"";
                                        }

                                        if (isID)
                                        {
                                            _builder.AppendIndent($"[{value}] = new {container.SchemaData.TableName} {{ ");
                                        }

                                        _builder.Append($"{record.SchemaData.Name} = {value},");
                                    }

                                    // 하나 element 할당 후 닫는 부분
                                    _builder.Append($"}},");
                                    _builder.AppendLine();
                                }
                            }
                            _builder.CloseBracket(true);

                            _builder.AppendLine($"byte[] bytes = MessagePackSerializer.Serialize(dic);");
                            _builder.AppendLine($"File.WriteAllBytes(outputPath, bytes);");
                            _builder.AppendLine();
                            // _builder.AppendLine("✅ Export complete: {outputPath}");
                        }
                        _builder.CloseBracket();
                    }
                }
                _builder.CloseBracket();
            }
            _builder.CloseBracket();

            return _builder.Current.ToString();
        }
    }
}
