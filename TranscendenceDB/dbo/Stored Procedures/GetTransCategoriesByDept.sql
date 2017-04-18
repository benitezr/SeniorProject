CREATE procedure [dbo].[GetTransCategoriesByDept]
(
	@deptid as int = null
)
as
begin
	select C.FundCategory as [Category], isnull(F.Amount, 0) as [Amount]
	from (
		values (1), (2), (3), (4), (5), 
				(6), (7), (8), (9), (10), (11), (12)
	) [MonthList](Months)
	Left Join (
		select FundCategory
		from Transactions, Funding_Sources
		where (@deptid is null or DeptID = @deptid) and Transactions.FundMasterID = Funding_Sources.FundMasterID
		group by FundCategory
	) C on C.FundCategory is not null
	Left Join (
		select datepart(month, TransDate) as [Month],FundCategory, Sum(TransAmount) as [Amount]
		from Transactions, Funding_Sources
		where (@deptid is null or DeptID = @deptid) and Transactions.FundMasterID = Funding_Sources.FundMasterID
		group by FundCategory, datepart(month, TransDate)
	) F on F.[Month] = [Months] and F.FundCategory = C.FundCategory
	order by Months
end