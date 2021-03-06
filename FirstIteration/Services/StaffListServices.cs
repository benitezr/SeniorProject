﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FirstIteration.Models;
using System.Web.Mvc;

namespace FirstIteration.Services
{
    public class StaffListServices
    {
        /* returns all staff that match deptID */
        public List<Staff> GetStaffList(int id)
        {

            var allStaff = new List<Staff>();
            using (var context = new transcendenceEntities())
            {
                allStaff = (from s in context.Staffs
                             where s.DeptID == id
                             select s).ToList();
                
                
            }
            return allStaff;
        }
    }
}