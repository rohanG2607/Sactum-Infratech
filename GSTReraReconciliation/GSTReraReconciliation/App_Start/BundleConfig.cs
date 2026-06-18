using System.Web.Optimization;

namespace GSTReraReconciliation
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            // jQuery
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                "~/Scripts/jquery-{version}.js"));

            // jQuery Validation
            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                "~/Scripts/jquery.validate*"));

            // Bootstrap JS
            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                "~/Scripts/bootstrap.bundle.js"));

            // Site-specific scripts
            bundles.Add(new ScriptBundle("~/bundles/site").Include(
                "~/Scripts/site.js"));

            // Bootstrap CSS
            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/bootstrap.css",
                "~/Content/Site.css"));
        }
    }
}
