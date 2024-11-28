using System;
using BobsBookstoreClassic.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        private string GetConfigurationValue(string key)
        {
            var types = new[] { Type.GetType("Bookstore.Domain.BookstoreConfiguration"), Type.GetType("Bookstore.Data.BookstoreConfiguration") };
            foreach (var type in types)
            {
                if (type != null)
                {
                    var method = type.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        var result = method.Invoke(null, new object[] { key });
                        if (result != null)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            return null;
        }
        public ActionResult Login(string redirectUri = null)
        {
            if(string.IsNullOrWhiteSpace(redirectUri)) return RedirectToAction("Index", "Home");

            return Redirect(redirectUri);
        }

        public ActionResult LogOut()
        {
            var authType = GetConfigurationValue("Services/Authentication");
            return authType == "aws" ? CognitoSignOut() : LocalSignOut();
        }

        private ActionResult LocalSignOut()
        {
            if (Request.Cookies.ContainsKey("LocalAuthentication"))
            {
                Response.Cookies.Append("LocalAuthentication", "", new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(-1)
                });
            }

            return RedirectToAction("Index", "Home");
        }

        private ActionResult CognitoSignOut()
        {
            if (Request.Cookies.ContainsKey(".AspNet.Cookies"))
            {
                Response.Cookies.Append(".AspNet.Cookies", "", new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(-1)
                });
            }

            var domain = GetConfigurationValue("Authentication/Cognito/CognitoDomain");
            var clientId = GetConfigurationValue("Authentication/Cognito/LocalClientId");
            var logoutUri = $"{Request.Scheme}://{Request.Host}/";

            return Redirect($"{domain}/logout?client_id={clientId}&logout_uri={logoutUri}");
        }
    }
}