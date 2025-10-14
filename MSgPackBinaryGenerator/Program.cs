using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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
            string[] columns = itemTableCsv[0].Split(',');

            for (int i = 0; i < columns.Length; i++)
            {
                for (int j = 1; j < itemTableSchemaCsv.Length; j++)
                {
                    var columnName = itemTableSchemaCsv[j].Split(',')[0];
                    var typeName = itemTableSchemaCsv[j].Split(',')[1];

                    if (columns[i] == columnName)
                    {
                        itemTableTypes += $",{typeName}";
                    }
                }
            }

            itemTableTypes = itemTableTypes.Trim(',');

            Console.WriteLine(itemTableCsv[0]);
            Console.WriteLine(itemTableTypes);
            var generator = new DBContainerGenerator();
            var itemTableFields = new TableSchemaContents("ItemTable", itemTableCsv[0], itemTableTypes);
            var r = generator.Generate(new List<TableSchemaContents>() { itemTableFields });

            Console.WriteLine(r.ToString());

            //-------------------------------------//

            var enumCsv = File.ReadAllLines(@"C:\Users\LeeYunSeon\source\repos\MSgPackBinaryGenerator\MSgPackBinaryGenerator\Data\EnumTable.csv");
            var raws = enumCsv.ToList();
            raws.RemoveAt(0);
            var enums = new EnumSchemaContents(enumCsv[0], raws.ToArray());

            var enumGenerator = new DBEnumContainer();
            var enumRes = enumGenerator.Generate(enums);
            Console.WriteLine(enumRes.ToString());
        }
    }
}
