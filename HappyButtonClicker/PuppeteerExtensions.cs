using System.Threading.Tasks;
using PuppeteerSharp;

namespace HappyButtonClicker
{
    public static class PuppeteerExtensions
    {
        public static async Task<string> TryGetTextContent(this Frame frame, string cssSelector)
        {
            ElementHandle element = null;
            try
            {
                element = await frame.WaitForSelectorAsync(cssSelector, new WaitForSelectorOptions
                {
                    Timeout = 1000
                });
            }
            catch (System.Exception)
            {
                return null;
            }

            return await frame.GetInnerText(element);
        }

        public static async Task<string> GetInnerText(this Frame frame, ElementHandle handle)
        {
            return await frame.EvaluateFunctionAsync<string>("el => el.textContent", handle);
        }

        public static async Task<bool> NavigateAndWait(this Page page, string url)
        {
            try
            {
                return (await page.GoToAsync(url, 15000, new[] { WaitUntilNavigation.Networkidle2 })).Ok;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
