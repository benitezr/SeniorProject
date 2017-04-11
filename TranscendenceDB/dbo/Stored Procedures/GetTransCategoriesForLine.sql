CREATE procedure [dbo].[GetTransCategoriesForLine]
(
	@deptid as int = null,
	@fundcategory as varchar(5) = '%'
)
as
begin
	select datepart(month, TransDate) as [TransMonth], FundCategory, Sum(TransAmount) as [TransAmount]
	from Funding_Sources, Transactions
	where (@deptid is null or DeptID = @deptid) and
	Transactions.FundMasterID = Funding_Sources.FundMasterID and
	FundCategory like @fundcategory
	group by FundCategory, datepart(year, TransDate), datepart(month, TransDate)
	order by datepart(month, TransDate) asc
end
