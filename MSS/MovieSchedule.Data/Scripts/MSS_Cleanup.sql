DELETE FROM [MovieScheduleStats].[dbo].[Showtime]

DELETE FROM [MovieScheduleStats].[dbo].[DistributorMovie]
DELETE FROM [MovieScheduleStats].[dbo].[Movie]
DELETE FROM [MovieScheduleStats].[dbo].[Distributor]

DELETE FROM [MovieScheduleStats].[dbo].[Source]
DELETE FROM [MovieScheduleStats].[dbo].[Cinema]
DELETE FROM [MovieScheduleStats].[dbo].[City]


DBCC CHECKIDENT ('[dbo].[Showtime]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Source]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Cinema]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[City]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Movie]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Distributor]', RESEED, 0);

DBCC SHRINKDATABASE (N'MovieScheduleStats', 0);
GO
