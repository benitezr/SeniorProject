using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CsvHelper;
using FirstIteration.Models;
using System.Data.Entity.Core.EntityClient;
using System.IO;
using System.Data.SqlClient;
using System.Data;

namespace FirstIteration.Services
{
    public class ImportServices
    {
        private const int batchSize = 500;
        public bool IsCanceled { get; set; }

        public void ProcessTransactions(Stream inputStream)
        {
            int readCount = 0;
            long rowsCopied = 0;
            string tableName = "Transactions";
            string[] columnNames = new string[] { "UniqueID", "DeptID", "StaffID", "FundMasterID", "TransType", "TransDate",
                "TransTransfer", "TransAdjustment", "TransCredit", "TransCharge", "TransAmount" };

            using (var csvreader = new CsvReader(new StreamReader(inputStream)))
            {
                string connStr = GetConnectionString();

                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction(IsolationLevel.Serializable))
                    {
                        using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction))
                        {
                            bulkCopy.DestinationTableName = tableName;
                            bulkCopy.NotifyAfter = batchSize;
                            bulkCopy.SqlRowsCopied += (sender, e) =>
                            {
                                rowsCopied = e.RowsCopied;
                                e.Abort = IsCanceled;
                            };

                            List<DataColumn> dataColumns = new List<DataColumn>();
                            DataTable dataTable = new DataTable(tableName);
                            bool empty;

                            foreach (string column in columnNames)
                            {
                                dataColumns.Add(new DataColumn(column));
                                bulkCopy.ColumnMappings.Add(column, column);
                            }

                            dataTable.Columns.AddRange(dataColumns.ToArray());
                            empty = !csvreader.Read();

                            while (!empty)
                            {
                                //Parse file and calculate TransAmount
                                //Add row to dataTable
                                readCount++;
                                empty = !csvreader.Read();

                                if (readCount == batchSize || empty)
                                {
                                    try
                                    {
                                        bulkCopy.WriteToServer(dataTable);
                                    }
                                    catch (Exception e)
                                    {
                                        transaction.Rollback();
                                        bulkCopy.Close();
                                        dataTable.Rows.Clear();
                                        throw;
                                    }

                                    if (empty && dataTable.Rows.Count > 0)
                                        rowsCopied += dataTable.Rows.Count;

                                    dataTable.Rows.Clear();
                                    readCount = 0;
                                }
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
        }

        private string GetConnectionString()
        {
            var context = new transcendenceEntities();
            EntityConnection connection = (EntityConnection)context.Database.Connection;
            return connection.StoreConnection.ConnectionString;
        }
    }
}