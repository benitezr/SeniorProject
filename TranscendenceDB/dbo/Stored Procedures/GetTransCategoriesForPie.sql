CREATE PROCEDURE [dbo].[GetTransCategoriesForPie]
(
	@deptid int = null,
	@fundcategory varchar(10) = null
)
as
DECLARE @amtSum int
SET @amtSum = (select Sum(TransAmount) as [Amount]
			from Funding_Sources F, Transactions T
			where T.FundMasterID = F.FundMasterID and (@deptid is null or DeptID = @deptid) and 
			(@fundcategory is null or FundCategory like '%' + @fundcategory + '%'))

SELECT CASE WHEN @fundcategory is null THEN FundCategory ELSE FundCodeName END as [Category], (SUM(TransAmount) / @amtSum) * 100 as [Amount]
from Funding_Sources F, Transactions T
where T.FundMasterID = F.FundMasterID and (@deptid is null or DeptID = @deptid) and 
			(@fundcategory is null or FundCategory like '%' + @fundcategory + '%')
group by case when @fundcategory is null then FundCategory else FundCodeName end