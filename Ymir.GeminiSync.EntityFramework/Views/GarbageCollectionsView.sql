CREATE VIEW [dbo].[GarbageBinCollectionsView]
AS
SELECT DISTINCT al.CustomerId, al.AgreementLineId, al.AgreementId, a.ExternalAgreementId, al.UnitId, al.PlaceNr, f.Name AS FractionName, u.ShortName, u.HasLock, al.RegDate, al.FromDate, al.ToDate, al.LastChanged
FROM            dbo.AgreementLine AS al INNER JOIN
                         dbo.Agreement AS a ON a.GPSLSCustomerId = al.CustomerId AND a.AgreementId = al.AgreementId INNER JOIN
                         dbo.Unit AS u ON u.UnitId = al.UnitId AND u.CustomerId = al.CustomerId INNER JOIN
                         dbo.Place AS p ON p.PlaceNr = al.PlaceNr AND p.GPSLSCustomerId = al.CustomerId AND p.AgreementId = al.AgreementId INNER JOIN
                         dbo.Unit_Fraction AS uf ON uf.CustomerId = u.CustomerId AND uf.UnitId = u.UnitId INNER JOIN
                         dbo.Fraction AS f ON f.CustomerId = uf.CustomerId AND f.FractionId = uf.FractionId
WHERE        (al.Status <> 99) AND (GETDATE() < al.ToDate) AND (p.Status <> 99) OR
                         (al.Status <> 99) AND (p.Status <> 99) AND (al.ToDate = '1900-01-01 00:00:00.000')
GO