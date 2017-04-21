CREATE TABLE [dbo].[Staff] (
    [StaffID]   INT          NOT NULL,
    [DeptID]    INT          NOT NULL,
    [StaffName] VARCHAR (35) NOT NULL,
    PRIMARY KEY CLUSTERED ([StaffID] ASC),
    CONSTRAINT [FK_DeptID] FOREIGN KEY ([DeptID]) REFERENCES [dbo].[Departments] ([DeptID]) ON DELETE CASCADE ON UPDATE CASCADE
);

