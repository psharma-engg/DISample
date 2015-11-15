using System.Web.Mvc;

namespace AspNetIdentityDependencyInjectionSample.Controllers
{
    [Authorize]
    public class WebApiTestController : Controller
    {
        // GET: WebApiTest
        public ActionResult Index()
        {
            return View();
        }
    }
}