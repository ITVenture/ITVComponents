namespace ITVComponents.EFRepo.Helpers
{
    /// <summary>
    /// Schema placeholder for hand-written migration SQL. Migrations are generated without a schema so that the same
    /// migration applies to dbo and to any schema configured per installation. Raw SQL inside a migration refers to the
    /// target schema with this placeholder; the schema-aware migrations sql-generator replaces it when the migration is
    /// applied (with dbo, when no schema is configured).
    /// </summary>
    public static class MigrationSchema
    {
        /// <summary>
        /// The placeholder text. Use it in brackets, e.g. <c>[__SCHEMA__].[Tenants]</c>
        /// </summary>
        public const string Placeholder = "__SCHEMA__";

        /// <summary>
        /// The schema that is used when nothing is configured
        /// </summary>
        public const string DefaultSchema = "dbo";
    }
}
