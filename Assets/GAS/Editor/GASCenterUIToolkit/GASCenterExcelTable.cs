using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;

namespace GAS.Editor
{
    internal sealed class GASCenterExcelTable
    {
        private readonly string _excelPath;
        private readonly Dictionary<int, int> _idToRowMap = new();

        public GASCenterExcelTable(string excelPath)
        {
            _excelPath = excelPath;
        }

        public Dictionary<string, int> HeaderMap { get; private set; } = new();
        public Dictionary<int, Dictionary<string, object>> Rows { get; private set; } = new();

        public IReadOnlyCollection<int> Ids => Rows.Keys;

        public void Load()
        {
            HeaderMap = new Dictionary<string, int>();
            Rows = new Dictionary<int, Dictionary<string, object>>();
            _idToRowMap.Clear();

            using var package = new ExcelPackage(new FileInfo(_excelPath));
            var worksheet = package.Workbook.Worksheets[1];

            for (var i = 0; i < 500; i++)
            {
                var rawHeader = worksheet.Cells[1, i + 1].Value?.ToString();
                if (string.IsNullOrEmpty(rawHeader))
                {
                    continue;
                }

                var header = rawHeader.Split('#')[0];
                if (!string.IsNullOrEmpty(header))
                {
                    HeaderMap[header] = i + 1;
                }
            }

            var safeCnt = 99999;
            var row = 4;
            while (safeCnt > 0 && worksheet.Cells[row, 2].Value != null)
            {
                safeCnt--;
                if (!int.TryParse(worksheet.Cells[row, 2].Value.ToString(), out var id))
                {
                    row++;
                    continue;
                }

                var rowData = new Dictionary<string, object>();
                foreach (var pair in HeaderMap)
                {
                    rowData[pair.Key] = worksheet.Cells[row, pair.Value].Value;
                }

                for (var column = 1; column <= worksheet.Dimension.End.Column; column++)
                {
                    rowData[$"__column_{column}"] = worksheet.Cells[row, column].Value;
                }

                Rows[id] = rowData;
                _idToRowMap[id] = row;
                row++;
            }
        }

        public int AllocateRow(int id)
        {
            if (_idToRowMap.TryGetValue(id, out var existingRow))
            {
                return existingRow;
            }

            var nextRow = _idToRowMap.Values.Count > 0 ? _idToRowMap.Values.Max() + 1 : 4;
            _idToRowMap[id] = nextRow;
            return nextRow;
        }

        public void SaveRow(int id, IReadOnlyDictionary<string, object> values)
        {
            using var package = new ExcelPackage(new FileInfo(_excelPath));
            var worksheet = package.Workbook.Worksheets[1];
            var row = AllocateRow(id);

            foreach (var pair in values)
            {
                if (HeaderMap.TryGetValue(pair.Key, out var column))
                {
                    worksheet.Cells[row, column].Value = pair.Value;
                }
            }

            package.Save();
            Load();
        }

        public void SaveRowWithRawColumns(
            int id,
            IReadOnlyDictionary<string, object> values,
            IReadOnlyDictionary<int, object> rawValues)
        {
            using var package = new ExcelPackage(new FileInfo(_excelPath));
            var worksheet = package.Workbook.Worksheets[1];
            var row = AllocateRow(id);

            foreach (var pair in values)
            {
                if (HeaderMap.TryGetValue(pair.Key, out var column))
                {
                    worksheet.Cells[row, column].Value = pair.Value;
                }
            }

            foreach (var pair in rawValues)
            {
                worksheet.Cells[row, pair.Key].Value = pair.Value;
            }

            package.Save();
            Load();
        }

        public bool DeleteRow(int id)
        {
            if (!_idToRowMap.TryGetValue(id, out var row))
            {
                return false;
            }

            if (row < 4)
            {
                return false;
            }

            using var package = new ExcelPackage(new FileInfo(_excelPath));
            var worksheet = package.Workbook.Worksheets[1];
            worksheet.DeleteRow(row, 1);
            package.Save();
            Load();
            return true;
        }

        public void SaveRawColumns(int id, IReadOnlyDictionary<int, object> values)
        {
            using var package = new ExcelPackage(new FileInfo(_excelPath));
            var worksheet = package.Workbook.Worksheets[1];
            var row = AllocateRow(id);

            foreach (var pair in values)
            {
                worksheet.Cells[row, pair.Key].Value = pair.Value;
            }

            package.Save();
            Load();
        }

        public Dictionary<string, object> GetRowOrEmpty(int id)
        {
            return Rows.TryGetValue(id, out var row)
                ? new Dictionary<string, object>(row)
                : new Dictionary<string, object>();
        }
    }
}
