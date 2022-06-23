using Microsoft.VisualStudio.TestTools.UnitTesting;

using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

using SeleniumExtras.WaitHelpers;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MobileShadowDOM
{
    public enum Device { iPhone, iPad, Android, Windows };

    public class ParallelTestDriver
    {
        public IWebDriver Driver { get; private set; } = null;

        public ParallelTestDriver(string initURL,
            Device device, Dictionary<string, object> browserstackOptions, NameValueCollection bCaps)
        {
            bool flag;
            DriverOptions capabilities;
            Action<string, string, DriverOptions> act;

            switch (device)
            {
                case Device.Android:
                    capabilities = new AppiumOptions();
                    if (bCaps != null && bCaps.Count > 0)
                        browserstackOptions.Add("chrome", bCaps.ToDictionary());
                    act = InitMobileAndroid;
                    ((AppiumOptions)capabilities).AddAdditionalAppiumOption("bstack:options", browserstackOptions);
                    break;
                case Device.iPad:
                case Device.iPhone:
                    capabilities = new AppiumOptions();
                    if (bCaps != null && bCaps.Count > 0)
                        browserstackOptions.Add("safari", bCaps.ToDictionary());
                    act = InitMobileIOS;
                    ((AppiumOptions)capabilities).AddAdditionalAppiumOption("bstack:options", browserstackOptions);
                    break;
                case Device.Windows:
                    capabilities = new ChromeOptions();
                    capabilities.BrowserVersion = "latest";
                    act = InitWindows;
                    ((ChromeOptions)capabilities).AddAdditionalOption("bstack:options", browserstackOptions);
                    break;
                default:
                    throw new ArgumentException("Device type is not supported", nameof(device));
            }
            do
            {
                string url = "https://www.jeunesseglobal.com/";
                flag = false;
                try
                {
                    act.Invoke(initURL, url, capabilities);
                }
                catch (WebDriverException ex) when (ex.Message.StartsWith("All parallel tests are currently in use, including the queued tests") ||
                                                    ex.Message.StartsWith("The HTTP request to the remote WebDriver server"))
                {
                    flag = true;
                    Thread.Sleep(10000);
                }
            } while (flag);
        }

        public void InitMobileIOS(string initURL, string url, DriverOptions capability)
        {
            Driver = new IOSDriver(new Uri(initURL), capability, TimeSpan.FromSeconds(120));
            Driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(120);
            Driver.Navigate().GoToUrl(url);
        }
        public void InitMobileAndroid(string initURL, string url, DriverOptions capability)
        {
            Driver = new AndroidDriver(new Uri(initURL), capability, TimeSpan.FromSeconds(120));
            Driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(120);
            Driver.Navigate().GoToUrl(url);
        }
        public void InitWindows(string initURL, string url, DriverOptions capability)
        {
            Driver = new RemoteWebDriver(new Uri(initURL), capability.ToCapabilities(), TimeSpan.FromSeconds(120));
            Driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(120);
            Driver.Navigate().GoToUrl(url);
        }
        public void Quit()
        {
            if (Driver != null)
            {
                Driver.Quit();
                Driver = null;
            }
        }
    }

    public abstract partial class Disposed : IDisposable
    {
        public abstract string Url { get; }

        public IWebDriver Driver { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing) { }

        public void WaitForAjax(int sec = 20)
        {
            bool? jQueryDefined = (bool?)(Driver as IJavaScriptExecutor).ExecuteScript("return !!window.jQuery");
            if (jQueryDefined == true)
            {
                CustomWebDriverWait(driver =>
                {
                    try
                    {
                        return (bool)((IJavaScriptExecutor)driver).ExecuteScript("return jQuery.active == 0");
                    }
                    catch (WebDriverException)
                    {
                        return false;
                    }
                }, sec);
            }
        }
        public void WaitForReadyState(int seconds = 60)
        {
            CustomWebDriverWait(driver =>
            {
                try
                {
                    return ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState")
                    .ToString().Equals("complete");
                }
                catch (WebDriverException)
                {
                    return false;
                }
            }, seconds);
        }
        public void WaitForReadyWithoutAngular()
        {
            WaitForReadyState();
            WaitForAjax();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T CustomWebDriverWait<T>(Func<IWebDriver, T> func, int sec = 20)
        {
            return CustomWebDriverWait(func, TimeSpan.FromSeconds(sec));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T CustomWebDriverWait<T>(Func<IWebDriver, T> func, TimeSpan timeSpan)
        {
            T result = default;
            try
            {
                WebDriverWait wait = new WebDriverWait(Driver, timeSpan);
                wait.IgnoreExceptionTypes(new[] { typeof(StaleElementReferenceException) });
                result = wait.Until(func);
            }
            catch (WebDriverTimeoutException) { }
            return result;
        }

        public void WaitForPartialUrl(string urlEnding, int sec = 20)
        {
            WaitForReadyState(220);

            WebDriverWait wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(sec));
            wait.Until(WebDriver =>
            {
                try
                {
                    return WebDriver.Url.Contains(urlEnding);
                }
                catch
                {
                    return false;
                }
            });
        }

        public bool WaitForInvisibilityOfElements(IWebElement element, int sec = 20)
        {
            return CustomWebDriverWait(driver =>
            {
                try
                {
                    return !element.Displayed;
                }
                catch (NoSuchElementException)
                {
                    // Returns true because the element is not present in DOM. The
                    // try block checks if the element is present but is invisible.
                    return true;
                }
                catch (StaleElementReferenceException)
                {
                    // Returns true because stale element reference implies that element
                    // is no longer visible.
                    return true;
                }
                catch (NoSuchWindowException)
                {
                    return true;
                }
                catch (System.Reflection.TargetInvocationException)
                {
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }, sec);
        }

        public IWebElement WaitForElementIsVisible(By locator, int sec = 20)
        {
            return CustomWebDriverWait((driver) =>
            {
                try
                {
                    IWebElement element = driver.FindElement(locator);
                    if (element.Displayed && element.Enabled && element.CheckIfNotDisabled())
                    {
                        return element;
                    }

                    return null;
                }
                catch (StaleElementReferenceException)
                {
                    return null;
                }
            },
            sec);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IWebElement CheckIfElementExists(By element, int sec = 1)
        {
            return CheckIfElementExists(element, TimeSpan.FromSeconds(sec));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IWebElement CheckIfElementExists(By element, TimeSpan span)
        {
            return CustomWebDriverWait(ExpectedConditions.ElementToBeClickable(element), span);
        }

        public void WaitUntilAttributeAppear(By locator, string attributeName, int sec = 20)
        {
            WebDriverWait wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(sec));
            wait.Until((d) =>
            {
                IWebElement element = d.FindElement(locator);
                if (element != null)
                {
                    Dictionary<string, object> attrs = GetAllAttributesFromElement(element);
                    return attrs.Any(a => a.Key == attributeName);
                }
                return false;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IWebElement ScrollToElementJS(IWebElement element, string scrollIntoView = "false")
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)Driver;
            js.ExecuteScript(string.Format("arguments[0].scrollIntoView({0});", scrollIntoView), element);
            return element;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IWebElement JavaScriptClick(IWebElement element)
        {
            if (element == null)
                return null;
            try
            {
                (Driver as IJavaScriptExecutor)
                      .ExecuteScript("arguments[0].click();", element);
                return element;
            }
            catch
            {
                return null;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WaitForElementToClickAndDisappear(By locator, int sec = 20)
        {
            IWebElement el = WaitForElementIsVisible(locator, sec);
            Assert.IsNotNull(el, "Element is null. Locator: " + locator.Criteria);
            ScrollToElementJS(el, "{ block: \"center\" }");
            el.Click();
            Assert.IsTrue(WaitForInvisibilityOfElements(el, sec), "Element should disappeared, but exists. Locator: " + locator.Criteria);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void HoverElement(IWebElement element)
        {
            if (element == null)
                return;
            string javaScript = "var evObj = document.createEvent('MouseEvents');" +
                "evObj.initMouseEvent(\"mouseover\",true, false, window, 0, 0, 0, 0, 0, false, false, false, false, 0, null);" +
                "arguments[0].dispatchEvent(evObj);";

            IJavaScriptExecutor executor = Driver as IJavaScriptExecutor;
            executor.ExecuteScript(javaScript, element);
        }

        public IWebElement GetShadowRootElement(By shadowRootLocator, By elementLocator)
        {
            ISearchContext srElement = GetShadowRoot(shadowRootLocator);
            return srElement.FindElement(elementLocator);
        }
        public ISearchContext GetShadowRoot(By selector)
        {
            IWebElement shadowHost = Driver.FindElement(selector);
            return shadowHost.GetShadowRoot();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMobileBrowser(IWebDriver Driver)
        {
            return IsIOS(Driver) || IsAndroid(Driver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIOS(IWebDriver Driver)
        {
            return Driver.GetType() == typeof(IOSDriver);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAndroid(IWebDriver Driver)
        {
            return Driver.GetType() == typeof(AndroidDriver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMobileBrowser()
        {
            return IsMobileBrowser(Driver);
        }

        public Dictionary<string, object> GetAllAttributesFromElement(IWebElement element)
        {
            IJavaScriptExecutor javascriptDriver = (IJavaScriptExecutor)Driver;
            Dictionary<string, object> attributes = javascriptDriver.ExecuteScript("var items = {}; for (index = 0; index < arguments[0].attributes.length; ++index) { items[arguments[0].attributes[index].name] = arguments[0].attributes[index].value }; return items;", element) as Dictionary<string, object>;
            return attributes;
        }

    }

    public class JfrontHeader : Disposed
    {
        public class JfrontHeaderElements
        {
            public By LinkWellness { get; } = By.CssSelector(".nav-menu-header [data-name='WellnessMenuItem']");

            public By LinkMarketsMobile { get; } = By.CssSelector("#menu-footer-menu > li:nth-child(2) > a");

            public By DropDownMenu { get; } = By.CssSelector(".fa.fa-bars.fa-2x");

            public By CloseMessage { get; } = By.CssSelector("#toast-container > div > button");

            public By DropDownWellness { get; } = By.XPath("//*[contains(@class, 'mobile-slide-menu')]/li/div[boolean(.//span[@data-name='WellnessMenuItem'])]");

            public By LinkMobileReserve { get; } = By.CssSelector(".mobile-slide-menu [data-name='ReserveBrand']");
        }

        private readonly JfrontHeaderElements elements = new JfrontHeaderElements();

        public override string Url => "/zh-HK";

        public void ClickCloseMessage()
        {
            WaitForReadyState();
            IWebElement el = CheckIfElementExists(elements.CloseMessage);
            if (el != null)
            {
                el.Click();
                WaitForInvisibilityOfElements(el);
            }
        }
        public void ClickMarketsMobile()
        {
            JavaScriptClick(
                WaitForElementIsVisible(elements.LinkMarketsMobile));
        }

        public void HoverWellness()
        {
            HoverElement(WaitForElementIsVisible(elements.LinkWellness, 5));
        }

        public void ClickMenu()
        {
            WaitForElementIsVisible(elements.DropDownMenu).Click();
        }
        public void ClickMobileWellness()
        {
            WaitForElementIsVisible(elements.DropDownWellness).Click();
        }

        public void ClickLinkReserve()
        {
            HoverWellness();
            WaitForElementToClickAndDisappear(elements.LinkMobileReserve);

        }
    }

    public class JfrontCountries : Disposed
    {
        public class JfrontCountriesElements
        {
            public By ContainerCountries { get; } = By.CssSelector("div[class*='market-region']");

            public By LinkCanadaFlag { get; } = By.XPath("//*[@cms-translation='CANADA Select']/..");
            public By ImageCanadaFlag { get; } = By.CssSelector("img[jn-no-image*='CA.png']");
        }

        private readonly JfrontCountriesElements elements = new JfrontCountriesElements();

        public override string Url => "/countries";

        public void WaitToLoadCountries()
        {
            WaitForElementIsVisible(elements.ContainerCountries);
        }

        public void ClickCanada()
        {
            SelectCountry(elements.LinkCanadaFlag, elements.ImageCanadaFlag);
        }
        private void SelectCountry(By LinkCountry, By LinkCountryFlag)
        {
            IWebElement el;
            if (!IsMobileBrowser())
            {
                el = WaitForElementIsVisible(LinkCountry);
            }
            else
            {
                el = WaitForElementIsVisible(LinkCountryFlag);
            }
            ScrollToElementJS(el, "{ block: \"center\" }");
            WaitForReadyWithoutAngular();
            el.Click();
            WaitForInvisibilityOfElements(el);
        }
    }

    public class ProductsReserve : Disposed
    {
        public class ProductsReserveElements
        {
            public By ButtonBuyNow { get; } = By.CssSelector("[data-name='buyNow']");

            public By ButtonAddToCart { get; } = By.CssSelector("[cms-translation='addToCart']");
        }

        private readonly ProductsReserveElements elements = new ProductsReserveElements();

        public override string Url => "/reserve";

        public void ClickButtonBuyNow()
        {
            WaitForElementIsVisible(elements.ButtonBuyNow).Click();
            WaitForReadyState();
        }

        public void ClickButtonAddToCart()
        {
            JavaScriptClick(WaitForElementIsVisible(elements.ButtonAddToCart));
        }
    }

    public class ShoppingCart : Disposed
    {
        public class ShoppingCartElements
        {
            public By ShadowSideCartEl { get; } = By.CssSelector("side-cart");
        }
        private readonly ShoppingCartElements elements = new ShoppingCartElements();

        public override string Url => "";

        public void WaitForOpenShadowSideCart()
        {
            WaitUntilAttributeAppear(elements.ShadowSideCartEl, "open");
        }

        public void ClickShadowCartProceedToCheckout()
        {
            WaitForOpenShadowSideCart();
            GetShadowRootElement(elements.ShadowSideCartEl, By.CssSelector("a[class='btn-cart checkout']")).Click();
        }
    }

    public static class IWebElementExtentions
    {
        public static bool CheckIfNotDisabled(this IWebElement element)
        {
            return element.GetAttribute("disabled") == null;
        }

        public static Dictionary<string, object> ToDictionary(this NameValueCollection col)
        {
            var dict = new Dictionary<string, object>();
            if (col != null)
            {
                foreach (string key in col.AllKeys)
                {
                    dict.Add(key, col[key]);
                }
            }

            return dict;
        }
    }
}
