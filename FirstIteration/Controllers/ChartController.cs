using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FirstIteration.Models;
using DotNet.Highcharts.Options;
using DotNet.Highcharts.Helpers;
using System.Collections;
using System.Data;
using FirstIteration.Services;
using System.IO;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace FirstIteration.Controllers
{
    public class ChartController : Controller
    {
        private static LineChartServices LineService = new LineChartServices();
        private static StaffListServices StaffService = new StaffListServices();
        private static FundingSourceServices FundService = new FundingSourceServices();
        private static DepartmentServices DeptService = new DepartmentServices();
        private static BarChartServices BarService = new BarChartServices();
        private static PieChartServices PieService = new PieChartServices();
        private readonly ImportServices ImportService = new ImportServices();

        public ActionResult Dashboard()
        {
            ViewBag.Department = DeptService.GetAllDepartments();            
            return View();
        }

        public PartialViewResult _FundingSourceDropDowns()
        {
            ViewBag.Department = DeptService.GetAllDepartments();
            return PartialView();
        }

        public PartialViewResult _EmployeeDropDowns()
        {
            ViewBag.Department = DeptService.GetAllDepartments();
            return PartialView();
        }

        public ActionResult _BarChart()
        {
            return View();
        }

        public ActionResult _LineChart()
        {
            return View();
        }

        public ActionResult _PieChart()
        {
            return View();
        }

        [HttpGet]
        public ActionResult UploadModal(string id)
        {
            ViewBag.UploadType = id;
            return PartialView();
        }

        [HttpPost]
        public ActionResult UploadCsv()
        {
            var file = System.Web.HttpContext.Current.Request.Files["CsvUpload"];
            if (file.ContentLength > 0 && Path.GetExtension(file.FileName).ToUpper().Contains("CSV"))
            {
                string report = "", tableUpload = System.Web.HttpContext.Current.Request.Form["UploadType"];
                try
                {
                    switch (tableUpload)
                    {
                        //case "Transactions":
                        //    ImportService.ProcessTransactions(file.InputStream, progress);
                        //    break;
                        case "Departments":
                            report = ImportService.ProcessDepartments(file.InputStream);
                            break;
                        case "Staff":
                            report = ImportService.ProcessStaff(file.InputStream);
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
                return Content(report);
            }
            return new HttpStatusCodeResult(400, "File not found or incorrect file format.");
        }

        public JsonResult StaffList(int Id)
        {
            var staffList = StaffService.GetStaffList(Id);
            var list = staffList.Select(m => new { value = m.StaffID, text = m.StaffName });
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public JsonResult FundingCategoryList(int Id)
        {
            var fundCategoryList = FundService.GetFundingCategoryList(Id);
            return Json(fundCategoryList, JsonRequestBehavior.AllowGet);
        }

        public JsonResult FundingCodeNameList(int Id, string text)
        {
            var fundCodeNameList = FundService.GetFundCodeNameList(Id, text);
            return Json(fundCodeNameList, JsonRequestBehavior.AllowGet);
        }

        public JsonResult LineData(int? Id, string Source, int? employee)
        {
            if(Id == null)
            {
                var allData = LineService.GetAllTransactions().Select(m => new { name = m.Key, data = m.Value });
                return Json(allData, JsonRequestBehavior.AllowGet);
            }else
            {
                if (employee == null)
                {
                    var list = LineService.GetTransactionsByDeptID(Id, Source).Select(m => new { name = m.Key, data = m.Value });
                    return Json(list, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    var employeeData = LineService.GetEmployeeTransactions(Id, employee).Select(m => new { name = m.Key, data = m.Value });
                    return Json(employeeData, JsonRequestBehavior.AllowGet);
                }
                
            }
            
        }

        public JsonResult BarData(int? Id, string Source, int? employee)
        {
            if (Id == null)
            {
                var allData = BarService.GetAllTransactions().Select(m => new { name = m.Key, data = m.Value });
                return Json(allData, JsonRequestBehavior.AllowGet);
            }
            else
            {
                if (employee == null)
                {
                    var list = BarService.GetTransactionsByDeptID(Id, Source).Select(m => new { name = m.Key, data = m.Value });
                    return Json(list, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    var employeeData = BarService.GetEmployeeTransactions(Id, employee).Select(m => new { name = m.Key, data = m.Value });
                    return Json(employeeData, JsonRequestBehavior.AllowGet);
                }
                
            }
        }

        public JsonResult PieData(int? Id, string Source, int? employee)
        {
            if (Id == null)
            {
                var allData = PieService.GetAllTransactions();

                var test = new { name = "Clay Revenue", data = allData.Select(j => new { name = j.Key, y = j.Value.Single() } ) };

                return Json(test, JsonRequestBehavior.AllowGet);
            }
            else
            {
                if(employee == null)
                {
                    var allData = PieService.GetTransactionsByDeptID(Id, Source);
                    var test = new { name = "Clay Revenue", data = allData.Select(j => new { name = j.Key, y = j.Value.Single() }) };
                    return Json(test, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    var employeeData = PieService.GetEmployeeTransactions(Id, employee);
                    var test = new { name = "Clay Revenue", data = employeeData.Select(j => new { name = j.Key, y = j.Value.Single() }) };
                    return Json(test, JsonRequestBehavior.AllowGet);
                }
                
            }
        }
    }
}