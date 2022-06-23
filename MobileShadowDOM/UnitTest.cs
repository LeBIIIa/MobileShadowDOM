
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MobileShadowDOM;

using OpenQA.Selenium;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;

namespace MobileTestBrowserStack.TestCases
{
    [TestClass]
    public class Mobile_Dist_Enrollment_JFront
    {
        public TestContext TestContext { get; set; }

        #region protected properties
        protected string TestName { get; set; }
        protected ParallelTestDriver ParallelTestDriver { get; set; }
        protected IWebDriver Browser => ParallelTestDriver.Driver;
        #endregion

        //Change device
        protected virtual Device CurrentDevice => Device.iPhone;

        #region test
        [TestInitialize]
        public void Base_JeunesseTestInitialize()
        {
            TestName = TestContext.TestName;

            NameValueCollection bsCaps = ConfigurationManager.GetSection($"capabilities/{CurrentDevice}/BrowserStackCapabilities") as NameValueCollection;
            NameValueCollection bCaps = ConfigurationManager.GetSection($"capabilities/{CurrentDevice}/BrowserCapabilities") as NameValueCollection;

            Dictionary<string, object> browserstackOptions = new Dictionary<string, object>();

            string assemblyName = GetType().Assembly.GetName().Name;
            string currentUser = Environment.GetEnvironmentVariable("USERNAME");
            string username = ConfigurationManager.AppSettings.Get("user");
            string accesskey = ConfigurationManager.AppSettings.Get("key");

            browserstackOptions.Add("buildName", $"{currentUser} {DateTime.Now:d}"); //BuildName
            browserstackOptions.Add("projectName", assemblyName); //ProjectName
            browserstackOptions.Add("sessionName", TestName); //TestName
            browserstackOptions.Add("userName", username);
            browserstackOptions.Add("accessKey", accesskey);
            browserstackOptions.Add("seleniumVersion", "4.1.2");

            foreach (var a in bsCaps.AllKeys)
            {
                browserstackOptions.Add(a, bsCaps[a]);
            }

            string baseURL = "http://" + ConfigurationManager.AppSettings.Get("server");

            ParallelTestDriver = new ParallelTestDriver(baseURL, CurrentDevice, browserstackOptions, bCaps);
        }

        [TestCleanup]
        public void Base_JeunesseTestCleanup()
        {
            ParallelTestDriver?.Quit();
        }

        #endregion

        [TestMethod, TestCategory("Mobile")]
        public void Jfront_Dist_Enrollment_Canada()
        {
            using (JfrontHeader homepage = new JfrontHeader() { Driver = Browser })
            {
                homepage.ClickCloseMessage();
                homepage.ClickMarketsMobile();
                homepage.WaitForReadyState();
            }
            using (JfrontCountries market = new JfrontCountries() { Driver = Browser })
            {
                market.WaitToLoadCountries();
                market.ClickCanada();
                market.WaitForPartialUrl("/en-CA");
                market.WaitForReadyState();
            }
            using (JfrontHeader homepage = new JfrontHeader() { Driver = Browser })
            {
                homepage.ClickMenu();
                homepage.ClickMobileWellness();
                homepage.ClickLinkReserve();
                homepage.WaitForReadyState();
            }
            using (ProductsReserve reserve = new ProductsReserve() { Driver = Browser })
            {
                reserve.ClickButtonBuyNow();
                reserve.ClickButtonAddToCart();
                reserve.WaitForReadyState();
            }
            using (ShoppingCart cart = new ShoppingCart() { Driver = Browser })
            {
                cart.ClickShadowCartProceedToCheckout();
                cart.WaitForReadyState();
            }
        }
    }
}
