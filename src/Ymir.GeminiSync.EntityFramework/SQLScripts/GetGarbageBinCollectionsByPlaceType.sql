ALTER PROCEDURE [dbo].[GetGarbageBinCollectionsByPlaceType]
    @CustomerId INT,
    @PlaceTypeDescription NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH MatchingPlaces AS
    (
        SELECT
            ppt.CustomerId,
            ppt.PlaceNr
        FROM dbo.PlaceType AS pt
        INNER JOIN dbo.Place_PlaceType AS ppt
            ON ppt.CustomerId = pt.CustomerId
           AND ppt.PlaceTypeId = pt.PlaceTypeId
        WHERE pt.CustomerId = @CustomerId
          AND pt.Description = @PlaceTypeDescription
        GROUP BY
            ppt.CustomerId,
            ppt.PlaceNr
    ),
    EligibleAgreementLines AS
    (
        SELECT
            al.CustomerId,
            al.AgreementLineId,
            al.AgreementId,
            al.UnitId,
            al.PlaceNr,
            al.Termin,
            al.RegDate,
            al.FromDate,
            al.ToDate,
            al.LastChanged
        FROM MatchingPlaces AS mp
        INNER JOIN dbo.AgreementLine AS al
            ON al.CustomerId = mp.CustomerId
           AND al.PlaceNr = mp.PlaceNr
        WHERE al.Status <> 99
          AND EXISTS
          (
              SELECT 1
              FROM dbo.Place AS p
              WHERE p.GPSLSCustomerId = al.CustomerId
                AND p.AgreementId = al.AgreementId
                AND p.PlaceNr = al.PlaceNr
                AND p.Status <> 99
          )
    )
    SELECT DISTINCT
        al.CustomerId,
        al.AgreementLineId,
        al.AgreementId,
        a.ExternalAgreementId,
        al.UnitId,
        al.PlaceNr,
        al.Termin,
        f.Name AS FractionName,
        u.ShortName,
        u.HasLock,
        al.RegDate,
        al.FromDate,
        al.ToDate,
        al.LastChanged
    FROM EligibleAgreementLines AS al
    INNER JOIN dbo.Agreement AS a
        ON a.GPSLSCustomerId = al.CustomerId
       AND a.AgreementId = al.AgreementId
    INNER JOIN dbo.Unit AS u
        ON u.CustomerId = al.CustomerId
       AND u.UnitId = al.UnitId
    INNER JOIN dbo.Unit_Fraction AS uf
        ON uf.CustomerId = u.CustomerId
       AND uf.UnitId = u.UnitId
    INNER JOIN dbo.Fraction AS f
        ON f.CustomerId = uf.CustomerId
       AND f.FractionId = uf.FractionId
       AND f.Name COLLATE Latin1_General_100_CI_AS LIKE N'%restavfall%'
    OPTION (RECOMPILE);
END;