CREATE TABLE [dbo].[Funding_Sources] (
    [FundMasterID] CHAR (10) NOT NULL,
    [FundCategory] CHAR (10) NOT NULL,
    [FundCodeName] CHAR (50) NOT NULL,
    CONSTRAINT [PK_Funding_Sources] PRIMARY KEY CLUSTERED ([FundMasterID] ASC)
);

