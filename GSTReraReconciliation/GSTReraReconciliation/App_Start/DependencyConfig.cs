using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using GSTReraReconciliation.Data;
using GSTReraReconciliation.Data.Repositories;
using GSTReraReconciliation.Models;
using GSTReraReconciliation.Services;

namespace GSTReraReconciliation.App_Start
{
    /// <summary>
    /// Simple MVC dependency resolver that provides constructor injection for controllers.
    /// Avoids the need for an external IoC container (Unity, Autofac, etc.) for this small project.
    /// Registers: DbContext, GenericRepository&lt;T&gt;, ExcelImportService, ComparisonService, ExportService.
    /// </summary>
    public class SimpleDependencyResolver : IDependencyResolver
    {
        public object GetService(Type serviceType)
        {
            // --- Controllers ---
            if (serviceType == typeof(Controllers.HomeController))
            {
                return new Controllers.HomeController();
            }

            if (serviceType == typeof(Controllers.UploadController))
            {
                var db = CreateDbContext();
                var sessionRepo = new GenericRepository<UploadSession>(db);
                var reraRepo = new GenericRepository<RERARecord>(db);
                var gstRepo = new GenericRepository<GSTRecord>(db);
                var resultRepo = new GenericRepository<ComparisonResult>(db);
                var nameExtractor = new NameExtractionService();
                var transactionFilter = new TransactionFilterService();
                var importService = new ExcelImportService(reraRepo, gstRepo, nameExtractor, transactionFilter);
                var comparisonService = new ComparisonService(gstRepo, reraRepo, resultRepo);
                return new Controllers.UploadController(importService, comparisonService, sessionRepo);
            }

            if (serviceType == typeof(Controllers.DashboardController))
            {
                var db = CreateDbContext();
                var sessionRepo = new GenericRepository<UploadSession>(db);
                var resultRepo = new GenericRepository<ComparisonResult>(db);
                var reraRepo = new GenericRepository<RERARecord>(db);
                var gstRepo = new GenericRepository<GSTRecord>(db);
                return new Controllers.DashboardController(sessionRepo, resultRepo, reraRepo, gstRepo);
            }

            if (serviceType == typeof(Controllers.ReportsController))
            {
                var db = CreateDbContext();
                var resultRepo = new GenericRepository<ComparisonResult>(db);
                var sessionRepo = new GenericRepository<UploadSession>(db);
                var exportService = new ExportService();
                return new Controllers.ReportsController(resultRepo, sessionRepo, exportService);
            }

            // --- Fall back to default MVC behavior for framework types ---
            try
            {
                if (serviceType.IsInterface || serviceType.IsAbstract)
                    return null;
                return Activator.CreateInstance(serviceType);
            }
            catch
            {
                return null;
            }
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return Enumerable.Empty<object>();
        }

        private static ReconciliationDbContext CreateDbContext()
        {
            return new ReconciliationDbContext();
        }
    }
}
