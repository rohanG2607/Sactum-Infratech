using System.Data.Entity;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using GSTReraReconciliation.App_Start;
using GSTReraReconciliation.Data;
using GSTReraReconciliation.Migrations;

namespace GSTReraReconciliation
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Register dependency resolver for constructor injection
            DependencyResolver.SetResolver(new SimpleDependencyResolver());

            // Ensure App_Data directories exist
            string appDataPath = HttpContext.Current.Server.MapPath("~/App_Data");
            Directory.CreateDirectory(appDataPath);
            Directory.CreateDirectory(Path.Combine(appDataPath, "Uploads"));

            // EF6: Auto-create/migrate database on startup using Code First Migrations.
            // This will create the LocalDB .mdf file in App_Data if it doesn't exist.
            Database.SetInitializer(
                new MigrateDatabaseToLatestVersion<ReconciliationDbContext, Configuration>());

            // Force database creation on startup (triggers migration)
            using (var db = new ReconciliationDbContext())
            {
                db.Database.Initialize(force: false);
            }
        }
    }
}
