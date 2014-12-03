SELECT
(SELECT COUNT(*) FROM [MovieScheduleStats].[dbo].[Showtime])AS [ShowtimeCounts],
(SELECT COUNT(*) FROM [MovieScheduleStats].[dbo].[Cinema])AS [CinemaCounts],
(SELECT COUNT(*) FROM [MovieScheduleStats].[dbo].[Source] WHERE CinemaId IS NOT NULL) AS [CinemaSourceCounts],
(SELECT COUNT(*) FROM [MovieScheduleStats].[dbo].[City])AS [CityCounts],
(SELECT COUNT(*) FROM [MovieScheduleStats].[dbo].[Source] WHERE CityId IS NOT NULL AND CinemaId IS NULL) AS [CitySourceCounts],
(SELECT COUNT(*) FROM [MovieScheduleStats].[dbo].[Movie])AS [MovieCounts],
(SELECT COUNT(*) FROM [MovieScheduleStats].[dbo].[Distributor])AS [DistributorCounts]

SELECT
(SELECT MAX(ID) FROM [MovieScheduleStats].[dbo].[Showtime]) AS ShotimeId,
(SELECT MAX(ID) FROM [MovieScheduleStats].[dbo].[Cinema]) AS CinemaId,
(SELECT MAX(ID) FROM [MovieScheduleStats].[dbo].[City]) AS CityId,
(SELECT MAX(ID) FROM [MovieScheduleStats].[dbo].[Source]) AS SourceId

SELECT * 
FROM Showtime sht
INNER JOIN Cinema cnm
ON sht.CinemaId = cnm.Id
INNER JOIN Movie mv
ON sht.MovieId = mv.Id
INNER JOIN City ct
ON cnm.CityId = ct.Id
WHERE 1=1
--AND SessionsFormat = 'FourDX' 
AND CinemaId < 854 
AND TargetSite = 'afisha.ru'


SELECT cnm.Name, ct.Name 
FROM Cinema cnm
INNER JOIN City ct
ON cnm.CityId = ct.Id
WHERE 1=1
--AND SessionsFormat = 'FourDX' 
AND cnm.Id > 853
ORDER BY cnm.CityId,cnm.Name