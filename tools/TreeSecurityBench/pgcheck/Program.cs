using System;
using System.IO;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.PostgreSql.SyntaxHelper;

// Zieht die Anweisungen heraus, die ConfigureViews erzeugt - damit gegen den echten Code
// getestet wird und nicht gegen eine Abschrift davon.
var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
// Explizites Schema: ohne den schema-bewussten Migrations-Generator bliebe sonst der Platzhalter stehen.
PostgreSqlColumnsSyntaxHelper.ConfigureViews(builder, "public");

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
    writer.WriteLine(";");
    writer.WriteLine();
}

Console.WriteLine($"{count} Anweisungen nach {Path.GetFullPath(target)} geschrieben.");
