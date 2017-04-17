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

        public long ProcessTransactions(Stream inputStream, IProgress<string> progress)
        {
            long rowsCopied;
            //Dictionary<string, string> columnMaps = new Dictionary<string, string> { {"UniqueID", "uniqueid_c"}, {"DeptID", "DeptName"}, {"StaffID", "staffcode_c"},
            //    { "FundMasterID", "psplanmasterid_c"}, {"TransType", "transactioncode_c"}, {"TransDate", "transactiondate_d"}, {"TransTransfer", "transfer"},
            //    {"TransAdjustment", "adj"}, {"TransCredit", "credit"}, {"TransCharge", "charge"} };

            //Map csv columns to sql table columns and data types
            Dictionary<string, KeyValuePair<string, Type>> columnMaps = new Dictionary<string, KeyValuePair<string, Type>> { { "uniqueid_c", new KeyValuePair<string, Type>("UniqueID", typeof(int)) },
            { "DeptName", new KeyValuePair<string, Type>("DeptID", typeof(int)) }, { "staffcode_c", new KeyValuePair<string, Type>("StaffID", typeof(int)) }, { "psplanmasterid_c", new KeyValuePair<string, Type>("FundMasterID", typeof(int)) },
            { "transactioncode_c", new KeyValuePair<string, Type>("TransType", typeof(string)) }, { "transactiondate_d", new KeyValuePair<string, Type>("TransDate", typeof(DateTime)) }, { "transfer", new KeyValuePair<string, Type>("TransTransfer", typeof(decimal)) },
            { "adj", new KeyValuePair<string, Type>("TransAdjustment", typeof(decimal)) }, { "credit", new KeyValuePair<string, Type>("TransCredit", typeof(decimal)) }, { "charge", new KeyValuePair<string, Type>("TransCharge", typeof(decimal)) }};

            if (departments == null) LoadDeptDictionary();

            rowsCopied = Process(inputStream, "Transactions", columnMaps.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), progress, (csvreader, dataTable) =>
            {
                var row = dataTable.NewRow();

                foreach (KeyValuePair<string, KeyValuePair<string, Type>> item in columnMaps)
                {
                    if (item.Value.Key == "DeptID")
                        row[item.Value.Key] = departments[csvreader.GetField(item.Key)];
                    else
                        row[item.Value.Key] = Cast(csvreader.GetField(item.Key), item.Value.Value);
                }

                dataTable.Rows.Add(row);
            });

            return rowsCopied;
        }

        public string ProcessDepartments(Stream inputStream, IProgress<string> progress)
        {
            long rowsCopied;
            Dictionary<string, KeyValuePair<string, Type>> columnMaps = new Dictionary<string, KeyValuePair<string, Type>>
            { {"DeptName", new KeyValuePair<string, Type>("DeptName", typeof(string)) } };

            rowsCopied = Process(inputStream, "Departments", columnMaps.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), progress, (csvreader, dataTable) =>
            {
                var row = dataTable.NewRow();
                row["DeptName"] = csvreader.GetField("DeptName");

                dataTable.Rows.Add(row);
            });

            return rowsCopied.ToString();
        }

        public void ProcessStaff(Stream inputStream, IProgress<string> progress)
        {
            
        }

        private void LoadDeptDictionary()
        {
            using (var context = new transcendenceEntities())
            {
                departments = context.Departments.ToDictionary(d => d.DeptName, d => d.DeptID);
            }
        }

        private dynamic Cast(string str, Type type)
        {
            return Convert.ChangeType(str, type);
        }

        private long Process(Stream inputStream, string tableName, Dictionary<string, Type> columnMaps, IProgress<string> progress, ProcessDelegate processFile)
        {
            int readCount = 0, notifyCount = 0;
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
                            progress.Report(e.RowsCopied.ToString());
                            rowsCopied = e.RowsCopied;
                            notifyCount++;
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
                                try
                                {
                                    //Parse through csv fields and store them
                                    //Add range to the data table
                                    processFile(csvreader, dataTable);

                                    readCount++;
                                    empty = !csvreader.Read();

                                    if (readCount == batchSize || empty)
                                    {
                                        bulkCopy.WriteToServer(dataTable);                                        

                                        if (empty && dataTable.Rows.Count > 0)
                                            rowsCopied += dataTable.Rows.Count - notifyAfter * notifyCount;

                                        dataTable.Rows.Clear();
                                        readCount = 0;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    transaction.Rollback();
                                    bulkCopy.Close();
                                    dataTable.Rows.Clear();
                                    conn.Close();
                                    throw;
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