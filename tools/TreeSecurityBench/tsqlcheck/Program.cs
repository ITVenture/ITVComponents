using System;
using System.IO;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.SyntaxHelper;

// Gegenstueck zum PostgreSQL-Auszieher: holt die Anweisungen aus dem ECHTEN T-SQL-Code, damit der
// Vergleich beide Seiten aus der Quelle nimmt und nicht aus einer Abschrift.
var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
SqlColumnsSyntaxHelper.ConfigureViews(builder);

var target = args.Length > 0 ? args[0] : "objects.sql";
using var writer = new StreamWriter(target, false, new System.Text.UTF8Encoding(false));
var count = 0;
foreach (var op in builder.Operations)
{
    if (op is not SqlOperation sql)
    {
        Console.Error.WriteLine($"unerwartete Operation: {op.GetType().Name}");
        continue;
    }

    count++;
    writer.WriteLine(sql.Sql);
    // CREATE VIEW/FUNCTION/PROCEDURE muss die erste Anweisung eines Stapels sein.
    writer.WriteLine("GO");
    writer.WriteLine();
}

Console.WriteLine($"{count} Anweisungen nach {Path.GetFullPath(target)} geschrieben.");
