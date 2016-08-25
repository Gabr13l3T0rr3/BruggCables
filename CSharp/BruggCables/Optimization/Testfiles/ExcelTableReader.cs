using Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace Optimization.Testfiles
{
    class ExcelTableReader
    {
        private static IEnumerable<DataRow> DataTableToRowList(DataTable table)
        {
            IEnumerable<DataRow> rows = table.Rows.Cast<DataRow>();

            // set column names for easy row access
            foreach (DataColumn column in table.Columns)
            {
                column.ColumnName = rows.First()[column].ToString();
            }
            return rows.Skip(1);
        }

        public static Dictionary<string, IEnumerable<DataRow>> LoadWorksheets(string path, string[] worksheets = null)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            //using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateOpenXmlReader(fs))
            {
                var ret = new Dictionary<string, IEnumerable<DataRow>>();
                var tables = reader.AsDataSet().Tables.Cast<DataTable>();
                if (worksheets != null)
                {
                    foreach (var ws in worksheets)
                        if (!tables.Select(t => t.TableName).Contains(ws))
                            throw new FormatException($"Table {path} does not contain worksheet \"{ws}\"");
                    tables = tables.Where(t => worksheets.Contains(t.TableName));
                }

                foreach (var table in tables)
                    ret[table.TableName] = DataTableToRowList(table);

                return ret;
            }
        }
    }
}
