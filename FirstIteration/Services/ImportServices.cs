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
using System.Threading.Tasks;

namespace FirstIteration.Services
{
    public class ImportServices
    {
        private const int batchSize = 500;
        public bool IsCanceled { get; set; }
        private delegate void ProcessDelegate(CsvReader cr, DataTable dt);

        public long ProcessTransactions(Stream inputStream, IProgress<long> progress)
        {
            long rowsCopied;
            string[] columnNames = new string[] { "UniqueID", "DeptID", "StaffID", "FundMasterID", "TransType", "TransDate",
            "TransTransfer", "TransAdjustment", "TransCredit", "TransCharge" };

            rowsCopied = Process(inputStream, "Transactions", columnNames, progress, (csvreader, dataTable) =>
            {
                //Parse through csv fields and store them
                //Add range to the data table
            });

            return rowsCopied;
        }

        public void ProcessDepartments(Stream inputStream, IProgress<long> progress)
        {

        }

        public void ProcessEmployess(Stream inputStream, IProgress<long> progress)
        {

        }

        private long Process(Stream inputStream, string tableName, string[] columnHeaders, IProgress<long> progress, ProcessDelegate processFile)
        {
            int readCount = 0;
            long rowsCopied = 0;

            using (var context = new transcendenceEntities())
            {
                var conn = (SqlConnection)context.Database.Connection;
                conn.Open();
                using (var transaction = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction))
                    {
                        bulkCopy.DestinationTableName = tableName;
                        bulkCopy.NotifyAfter = batchSize;
                        bulkCopy.SqlRowsCopied += (sender, e) =>
                        {
                            progress.Report(e.RowsCopied);
                            rowsCopied = e.RowsCopied;
                            e.Abort = IsCanceled;
                        };

                        List<DataColumn> dataColumns = new List<DataColumn>();
                        DataTable dataTable = new DataTable(tableName);

                        foreach (string column in columnHeaders)
                        {
                            dataColumns.Add(new DataColumn(column));
                            bulkCopy.ColumnMappings.Add(column, column);
                        }

                        dataTable.Columns.AddRange(dataColumns.ToArray());

                        using (var csvreader = new CsvReader(new StreamReader(inputStream)))
                        {
                            bool empty = !csvreader.Read();
                            while (!empty)
                            {
                                //Parse through csv fields and store them
                                //Add range to the data table
                                processFile(csvreader, dataTable);

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
                                        conn.Close();
                                        throw;
                                    }

                                    if (empty && dataTable.Rows.Count > 0)
                                        rowsCopied += dataTable.Rows.Count;

                                    dataTable.Rows.Clear();
                                    readCount = 0;
                                }
                            }
                        }//CsvReader closed                            
                    }//SqlBulkCopy closed
                    transaction.Commit();
                    conn.Close();
                }//Transaction closed
            }//dbcontext closed                
            return rowsCopied;
        }//function closed
    }
}