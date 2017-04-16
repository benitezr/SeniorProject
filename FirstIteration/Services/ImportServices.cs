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
        private const int batchSize = 500, notifyAfter = 10;
        public bool IsCanceled { get; set; }
        private delegate void ProcessDelegate(CsvReader cr, DataTable dt);
        private Dictionary<string, int> departments;

        public long ProcessTransactions(Stream inputStream, IProgress<long> progress)
        {
            long rowsCopied;
            //Dictionary<string, string> columnMaps = new Dictionary<string, string> { {"UniqueID", "uniqueid_c"}, {"DeptID", "DeptName"}, {"StaffID", "staffcode_c"},
            //    { "FundMasterID", "psplanmasterid_c"}, {"TransType", "transactioncode_c"}, {"TransDate", "transactiondate_d"}, {"TransTransfer", "transfer"},
            //    {"TransAdjustment", "ajd"}, {"TransCredit", "credit"}, {"TransCharge", "charge"} };

            Dictionary<string, KeyValuePair<string, Type>> columnMaps = new Dictionary<string, KeyValuePair<string, Type>> { { "uniqueid_c", new KeyValuePair<string, Type>("UniqueID", typeof(int)) },
            { "", new KeyValuePair<string, Type>("", typeof(int)) }, { "", new KeyValuePair<string, Type>("", typeof(int)) }, { "", new KeyValuePair<string, Type>("", typeof(int)) },
            { "", new KeyValuePair<string, Type>("", typeof(int)) }, { "", new KeyValuePair<string, Type>("", typeof(int)) }, { "", new KeyValuePair<string, Type>("", typeof(int)) },
            { "", new KeyValuePair<string, Type>("", typeof(int)) }, { "", new KeyValuePair<string, Type>("", typeof(int)) }, { "", new KeyValuePair<string, Type>("", typeof(int)) }};

            if (departments == null) LoadDeptDictionary();

            rowsCopied = Process(inputStream, "Transactions", columnMaps.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), progress, (csvreader, dataTable) =>
            {
                //Parse through csv fields and store them
                //Add range to the data table
                var row = dataTable.NewRow();

                foreach (KeyValuePair<string, KeyValuePair<string, Type>> item in columnMaps)
                {
                    if (item.Value.Key == "DeptID")
                        row[item.Value.Key] = departments[csvreader.GetField(item.Key)];
                    else
                        row[item.Value.Key] = Convert.ChangeType(csvreader.GetField(item.Key), item.Value.Value);
                }

                dataTable.Rows.Add(row);
            });

            return rowsCopied;
        }

        public long ProcessDepartments(Stream inputStream, IProgress<long> progress)
        {
            long rowsCopied;
            Dictionary<string, KeyValuePair<string, Type>> columnMaps = new Dictionary<string, KeyValuePair<string, Type>>
            { {"DeptName", new KeyValuePair<string, Type>("DeptName", typeof(string)) } };

            rowsCopied = Process(inputStream, "Departments", columnMaps.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), progress, (csvreader, dataTable) =>
            {
                var deptName = csvreader.GetField("DeptName");
                var row = dataTable.NewRow();
                row["DeptName"] = deptName;

                dataTable.Rows.Add(row);
            });

            return rowsCopied;
        }

        public void ProcessStaff(Stream inputStream, IProgress<long> progress)
        {
            
        }

        private void LoadDeptDictionary()
        {
            using (var context = new transcendenceEntities())
            {
                departments = context.Departments.ToDictionary(d => d.DeptName, d => d.DeptID);
            }
        }

        private long Process(Stream inputStream, string tableName, Dictionary<string, Type> columnMaps, IProgress<long> progress, ProcessDelegate processFile)
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
                        bulkCopy.NotifyAfter = notifyAfter;
                        bulkCopy.SqlRowsCopied += (sender, e) =>
                        {
                            progress.Report(e.RowsCopied);
                            rowsCopied = e.RowsCopied;
                            e.Abort = IsCanceled;
                        };

                        List<DataColumn> dataColumns = new List<DataColumn>();
                        DataTable dataTable = new DataTable(tableName);

                        foreach (KeyValuePair<string, Type> item in columnMaps)
                        {
                            dataColumns.Add(new DataColumn(item.Key, item.Value));
                            bulkCopy.ColumnMappings.Add(item.Key, item.Key);
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