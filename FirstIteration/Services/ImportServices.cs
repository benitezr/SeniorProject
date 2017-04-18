﻿using System;
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
using System.Web.Mvc;

namespace FirstIteration.Services
{
    public class ImportServices
    {
        public bool IsCanceled { get; set; }
        private delegate void ProcessDelegate(CsvReader cr, DataTable dt);
        private Dictionary<string, int> _departments;

        public ImportServices()
        {
            using (var context = new transcendenceEntities())
            {
                _departments = context.Departments.ToDictionary(d => d.DeptName.Replace("\r\n", "").Trim(), d => d.DeptID);
            }
        }

        public ActionResult Import(HttpPostedFile file, string targetTable)
        {
            if (file.ContentLength > 0 && Path.GetExtension(file.FileName).ToUpper().Contains("CSV"))
            {
                string report = "";
                try
                {
                    switch (targetTable)
                    {
                        case "Transactions":
                            report = ProcessTransactions(file.InputStream);
                            break;
                        case "Departments":
                            report = ProcessDepartments(file.InputStream);
                            break;
                        case "Staff":
                            report = ProcessStaff(file.InputStream);
                            break;
                    }
                }
                catch (SqlException ex)
                {
                    string message = ex.Message.Contains("duplicate") ? "Cannot insert duplicate record" : "SQL exception detected.";
                    return new HttpStatusCodeResult(500, message);
                }
                catch (DuplicateNameException ex)
                {
                    return new HttpStatusCodeResult(500, ex.Message);
                }
                catch (Exception ex)
                {
                    return new HttpStatusCodeResult(500, "Csv data import failed.");
                }
                return new ContentResult { Content = report };
            }
            return new HttpStatusCodeResult(400, "File not found or incorrect file format.");
        }

        private string ProcessTransactions(Stream inputStream)
        {
            string rowsCopied;
            //Mapping columns programmatically
            //Dictionary<string, Type> columns = PropertiesToDictionary(typeof(Transaction), p => !p.GetGetMethod().IsVirtual && p.Name != "TransAmount");

            //Map csv columns to sql table columns and data types (done manually for now)
            Dictionary<string, KeyValuePair<string, Type>> columnMaps = new Dictionary<string, KeyValuePair<string, Type>> { { "uniqueid_c", new KeyValuePair<string, Type>("UniqueID", typeof(int)) },
            { "DeptName", new KeyValuePair<string, Type>("DeptID", typeof(int)) }, { "staffcode_c", new KeyValuePair<string, Type>("StaffID", typeof(int)) }, { "psplanmasterid_c", new KeyValuePair<string, Type>("FundMasterID", typeof(int)) },
            { "transactioncode_c", new KeyValuePair<string, Type>("TransType", typeof(string)) }, { "transactiondate_d", new KeyValuePair<string, Type>("TransDate", typeof(DateTime)) }, { "tranfer", new KeyValuePair<string, Type>("TransTransfer", typeof(decimal)) },
            { "adj", new KeyValuePair<string, Type>("TransAdjustment", typeof(decimal)) }, { "credit", new KeyValuePair<string, Type>("TransCredit", typeof(decimal)) }, { "charge", new KeyValuePair<string, Type>("TransCharge", typeof(decimal)) }};

            rowsCopied = Process(inputStream, "Transactions", columnMaps.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), (csvreader, dataTable) =>
            {
                var row = dataTable.NewRow();

                foreach (KeyValuePair<string, KeyValuePair<string, Type>> item in columnMaps)
                {
                    if (item.Value.Key == "DeptID")
                        row[item.Value.Key] = _departments[csvreader.GetField(item.Key)];
                    else
                        row[item.Value.Key] = Cast(csvreader.GetField(item.Key), item.Value.Value);
                }

                dataTable.Rows.Add(row);
            });

            return rowsCopied;
        }

        private string ProcessDepartments(Stream inputStream)
        {
            string rowsCopied;
            Dictionary<string, KeyValuePair<string, Type>> columnMaps = new Dictionary<string, KeyValuePair<string, Type>>
            { {"DeptName", new KeyValuePair<string, Type>("DeptName", typeof(string)) } };

            rowsCopied = Process(inputStream, "Departments", columnMaps.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), (csvreader, dataTable) =>
            {
                var row = dataTable.NewRow();
                string deptName = csvreader.GetField("DeptName");

                if (_departments.ContainsKey(deptName))
                    throw new DuplicateNameException(string.Format("Department name ({0}) already exists.", deptName));
                
                row["DeptName"] = deptName;                                     
                dataTable.Rows.Add(row);
            });

            return rowsCopied;
        }

        private string ProcessStaff(Stream inputStream)
        {
            string rowsCopied;

            Dictionary<string, KeyValuePair<string, Type>> columnMaps = new Dictionary<string, KeyValuePair<string, Type>> { { "staffcode_c", new KeyValuePair<string, Type>("StaffID", typeof(int)) },
            { "DeptName", new KeyValuePair<string, Type>("DeptID", typeof(int)) }, { "StaffName", new KeyValuePair<string, Type>("StaffName", typeof(string)) } };

            rowsCopied = Process(inputStream, "Staff", columnMaps.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), (csvreader, dataTable) => 
            {
                var row = dataTable.NewRow();

                foreach (KeyValuePair<string, KeyValuePair<string, Type>> item in columnMaps)
                {
                    if (item.Value.Key == "DeptID")
                        row[item.Value.Key] = _departments[csvreader.GetField(item.Key)];
                    else
                        row[item.Value.Key] = Cast(csvreader.GetField(item.Key), item.Value.Value);
                }

                dataTable.Rows.Add(row);
            });

            return rowsCopied;
        }

        private Dictionary<string, Type> PropertiesToDictionary(Type type, Func<System.Reflection.PropertyInfo, bool> predicate)
        {
            return type.GetProperties().Where(predicate).ToDictionary(p => p.Name, p => p.PropertyType);
        }

        private dynamic Cast(string str, Type type)
        {
            return Convert.ChangeType(str, type);
        }

        private string Process(Stream inputStream, string tableName, Dictionary<string, Type> columnMaps, ProcessDelegate processFile)
        {
            int readCount = 0, notifyCount = 0, notifyAfter = 10, batchSize = 500;
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
            return rowsCopied.ToString();
        }//function closed
    }
}