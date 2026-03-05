SELECT l.[LogLineId]
      ,al.[AgreementLineId]
      ,l.[Time]
      ,l.[Message]
      ,al.[PlaceNr]
      ,al.[UnitId]
      ,u.[Name]
      ,u.[ShortName]
      ,pt.Description
FROM [dbo].[LogLine] AS l
INNER JOIN [dbo].[AgreementLine] al ON al.AgreementLineId = l.AgreementLineId AND  al.CustomerId = l.CustomerId AND al.PASystem = l.PASystem
INNER JOIN [dbo].[Unit] u ON u.UnitId = al.UnitId AND u.CustomerId = al.CustomerId
FULL OUTER JOIN [dbo].[Place_PlaceType] ppt ON ppt.CustomerId = al.CustomerId AND ppt.PASystem = al.PASystem AND ppt.PlaceNr = al.PlaceNr
FULL OUTER JOIN dbo.PlaceType AS pt ON pt.PlaceTypeId = ppt.PlaceTypeId AND pt.CustomerId = ppt.CustomerId
WHERE l.[CustomerId] = 270
ORDER BY PlaceNr, Time DESC
