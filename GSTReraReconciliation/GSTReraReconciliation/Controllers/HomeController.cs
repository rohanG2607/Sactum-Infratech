using System.Web.Mvc;

namespace GSTReraReconciliation.Controllers
{
    /// <summary>
    /// Home controller serving the landing page with system overview.
    /// </summary>
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "GST vs RERA Bank Statement Reconciliation System";
            return View();
        }
    }
}
