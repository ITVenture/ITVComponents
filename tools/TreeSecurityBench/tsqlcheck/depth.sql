SET NOCOUNT ON;
-- Gegenstueck zur PostgreSQL-Messung: bei welcher Kettenlaenge bricht SQL Server ab?
DECLARE @laengen TABLE (laenge int);
INSERT INTO @laengen VALUES (98),(99),(100),(101),(102);

DECLARE @len int, @n int, @msg nvarchar(400);
DECLARE c CURSOR FOR SELECT laenge FROM @laengen ORDER BY laenge;
OPEN c;
FETCH NEXT FROM c INTO @len;
WHILE @@FETCH_STATUS = 0
BEGIN
    DELETE FROM TenantUserRoles;
    DELETE FROM TenantUsers;
    DELETE FROM SecurityRoles;
    DELETE FROM RoleRoles;
    DELETE FROM Tenants;

    ;WITH n AS (SELECT 1 AS i UNION ALL SELECT i+1 FROM n WHERE i < @len)
    INSERT INTO Tenants (TenantId, TenantName, ParentTenantId)
    SELECT i, 'T' + CAST(i AS nvarchar(10)), CASE WHEN i = 1 THEN NULL ELSE i - 1 END
    FROM n OPTION (MAXRECURSION 0);

    BEGIN TRY
        SELECT @n = MAX(ParentLevel) FROM UpwardsTenantTree;
        SET @msg = 'OK, tiefste Ebene ' + CAST(@n AS nvarchar(10));
    END TRY
    BEGIN CATCH
        SET @msg = 'ABBRUCH: Msg ' + CAST(ERROR_NUMBER() AS nvarchar(10)) + ' - ' + ERROR_MESSAGE();
    END CATCH

    PRINT CAST(@len AS nvarchar(10)) + '|' + @msg;
    FETCH NEXT FROM c INTO @len;
END
CLOSE c;
DEALLOCATE c;
