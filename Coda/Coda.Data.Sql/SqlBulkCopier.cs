﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Coda.Data.Sql
{
    public class SqlBulkCopier<TTableType> : IDisposable
    {
        public SqlBulkCopy BulkCopy { get; set; }
        public DataTable InternalTable { get; set; }

        private readonly Type[] _mappableTypes = new[] {
            typeof(int), typeof(decimal), typeof(double), typeof(string), typeof(bool), typeof(Guid),
            typeof(DateTime), typeof(DateTimeOffset), typeof(float), typeof(byte)
        };

        public SqlBulkCopier(SqlConnection db, string tableName)
        {
            BulkCopy = new SqlBulkCopy(db);
            Initialise(tableName);
        }

        protected virtual bool IsMappable(PropertyInfo property)
        {
            var nullableBaseType = Nullable.GetUnderlyingType(property.PropertyType);
            var baseType = nullableBaseType ?? property.PropertyType;
            return (!_mappableTypes.Contains(baseType) && !baseType.IsEnum);
        }

        public void Initialise(string tableName)
        {
            BulkCopy.DestinationTableName = tableName;

            // Dynamically construct a datatable and force name-based column mapping
            InternalTable = new DataTable();
            var properties = typeof(TTableType).GetProperties();
            foreach (var property in properties)
            {
                var nullableBaseType = Nullable.GetUnderlyingType(property.PropertyType);
                var baseType = nullableBaseType ?? property.PropertyType;
                if (!IsMappable(property)) continue;

                // If it's nullable as a base then the type used for mapping should be a string
                InternalTable.Columns.Add(property.Name, nullableBaseType != null ? typeof(string) : baseType);
            }

            // Remap all of the columns by name
            foreach (DataColumn column in InternalTable.Columns)
                BulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        public void AddRow(TTableType row)
        {
            var newRow = InternalTable.NewRow();
            foreach (DataColumn column in InternalTable.Columns)
            {
                // Get the value from the row itself
                var propertyValue = row.GetType().GetProperty(column.ColumnName).GetValue(row);
                newRow[column] = propertyValue;
            }
            InternalTable.Rows.Add(newRow);
        }

        public void AddRows(params TTableType[] rows)
        {
            foreach (var row in rows)
                AddRow(row);
        }

        public void WriteToServer()
        {
            BulkCopy.WriteToServer(InternalTable);
        }

        public async Task WriteToServerAsync()
        {
            await BulkCopy.WriteToServerAsync(InternalTable);
        }

        public void Dispose()
        {
            ((IDisposable)BulkCopy).Dispose();
        }
    }
}
