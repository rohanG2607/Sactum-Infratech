namespace GSTReraReconciliation.Migrations
{
    using System.Data.Entity.Migrations;

    /// <summary>
    /// EF6 Code First Migrations configuration.
    /// Automatic migrations are enabled for development convenience.
    /// Disable in production and use explicit migrations instead.
    /// </summary>
    internal sealed class Configuration : DbMigrationsConfiguration<Data.ReconciliationDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = false;
        }

        protected override void Seed(Data.ReconciliationDbContext context)
        {
            // No seed data — production system populated via file uploads.
        }
    }
}
