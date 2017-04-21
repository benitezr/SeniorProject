CREATE procedure [dbo].[GetTransCategoriesForLine]
(
	@deptid as int = null,
	@fundcategory as varchar(5) = '%'
)
as
begin
	select MonthList.Months as [Month], isnull(F.FundCategory, C.FundCategory) as [Category], isnull(F.Amount, 0) as [Amount]
	from (
		values (1), (2), (3), (4), (5), 
				(6), (7), (8), (9), (10), (11), (12)
	) [MonthList](Months)
	Left Join (
		select datepart(month, TransDate) as [Month],FundCategory, Sum(TransAmount) as [Amount]
		from Transactions, Funding_Sources
		where (@deptid is null or DeptID = @deptid) and Transactions.FundMasterID = Funding_Sources.FundMasterID
		group by FundCategory, datepart(month, TransDate)
	) F on F.[Month] = [Months]
	Left Join (
		select FundCategory
		from Transactions, Funding_Sources
		where (@deptid is null or DeptID = @deptid) and Transactions.FundMasterID = Funding_Sources.FundMasterID
		group by FundCategory
	) C on F.Amount is null and F.FundCategory is null
	order by [Month]
end
