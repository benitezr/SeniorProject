CREATE TABLE [dbo].[StaffDept]
(
	[StaffID] INT NOT NULL, 
    [DeptID] INT NOT NULL,
	CONSTRAINT PK_StaffID_DeptID PRIMARY KEY (StaffID, DeptID),
	CONSTRAINT [FK_StaffID_Bridge] FOREIGN KEY ([StaffID]) REFERENCES [dbo].[Staff] ([StaffID]),
	CONSTRAINT [FK_DeptID_Bridge] FOREIGN KEY ([DeptID]) REFERENCES [dbo].[Departments] ([DeptID]) 
)