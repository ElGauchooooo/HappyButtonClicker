using System.Linq;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace HappyButtonClicker
{
    public class ClickerBot
    {
        private Quiz _quiz = new Quiz();
        private Quiz _decisions = new Quiz();

        public async Task<ClickResult> StartClicking(string rootUrl, string followUpUrl = null)
        {
            try
            {
                await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = false,
                    DefaultViewport = null
                });

                var page = await browser.NewPageAsync();
                if (!await page.NavigateAndWait(rootUrl))
                    return ClickResult.NetworkIssue;

                if (string.IsNullOrWhiteSpace(followUpUrl))
                {
                    try
                    {
                        await page.WaitForSelectorAsync(".btn.btn-primary.btn-pill.btn-lg", new WaitForSelectorOptions()
                        {
                            Visible = true,
                            Timeout = 5000
                        });

                        await page.WaitForTimeoutAsync(1000);
                        var pillButton = await page.QuerySelectorAsync(".btn.btn-primary.btn-pill.btn-lg");
                        await pillButton.ClickAsync();
                    }
                    catch (System.Exception)
                    {
                        return ClickResult.Success; 
                    }
                }
                else
                {
                    await page.WaitForTimeoutAsync(1000);
                    if (!await page.NavigateAndWait(followUpUrl))
                        return ClickResult.NetworkIssue;
                }

                await page.WaitForSelectorAsync("iframe", new WaitForSelectorOptions
                {
                    Visible = true,
                    Timeout = 5000
                });

                var iframe = await page.QuerySelectorAsync("iframe");
                var contentFrame = await iframe.ContentFrameAsync();

                var iterationsWithoutElement = 0;

                while (true)
                {
                    ElementHandle elementToClick = null;
                    while (elementToClick == null)
                    {
                        iterationsWithoutElement++;
                        if (iterationsWithoutElement > 15)
                            return ClickResult.NoButtonToClick;

                        elementToClick = await GetNextElementToClick(contentFrame);
                    }

                    iterationsWithoutElement = 0;

                    try
                    {
                        await elementToClick.ClickAsync();
                    }
                    catch (System.Exception e)
                    {
                        var x = 10;
                        // we were slightly to fast, element just disappeared or did not appear completely (yet). 
                    }
                }
            }
            catch (System.Exception e)
            {
                return ClickResult.UserAbort;
            }
        }


        /// <summary>
        /// Just some random element being returned from the page. Do not ask me about the order...
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        private async Task<ElementHandle> GetNextElementToClick(Frame frame)
        {
            var timeout = 1000;
            await frame.WaitForTimeoutAsync(timeout);

            await UpdateWrongQuizAnswers(frame);

            var highImportanceButtons = await frame.QuerySelectorAllAsync(".question-example-overlay:not([hidden='hidden']) .example-buttons .button:not(.is-disabled)");
            foreach (var buttonHandle in highImportanceButtons)
            {
                var boundingBox = await buttonHandle.BoundingBoxAsync();
                var isVisible = boundingBox != null && boundingBox.Width > 0 && boundingBox.Height > 0;
                if (!isVisible)
                    continue;

                var buttonText = await frame.GetInnerText(buttonHandle);
                var isCandidate = new[] { "schließen" }.Any(x => buttonText.ToLower().Contains(x));
                if (isCandidate)
                    return buttonHandle;
            }

            var decisionTextHandle = await frame.QuerySelectorAllAsync(".decision-wrapper .text-column");
            if (decisionTextHandle.Any())
            {
                var decisionText = await frame.EvaluateFunctionAsync<string>("el => el.innerText", decisionTextHandle.First());
                var decisionHash = decisionText.GetHashCode();

                var choices = await frame.QuerySelectorAllAsync(".content-region .decision-choices li.choice button:not(.is-disabled)");

                Question decision = null;
                if (_decisions.Questions.All(x => x.Hash != decisionHash))
                {
                    decision = new Question { Hash = decisionHash };

                    foreach (var elementHandle in choices)
                    {
                        var choiceText = await frame.GetInnerText(elementHandle);
                        decision.Answers.Add(new Answer { Result = null, Text = choiceText });
                    }

                    _decisions.Questions.Add(decision);
                }
                else
                    decision = _decisions.Questions.Single(x => x.Hash == decisionHash);

                var bestAnswer = decision.GetBestAnswer();
                if (bestAnswer != null)
                {
                    foreach (var elementHandle in choices)
                    {
                        var boundingBox = await elementHandle.BoundingBoxAsync();
                        var isVisible = boundingBox != null && boundingBox.Width > 0 && boundingBox.Height > 0;
                        if (!isVisible)
                            continue;

                        var choiceText = await frame.EvaluateFunctionAsync<string>("e => e.innerText", elementHandle);
                        if (choiceText == bestAnswer)
                            return elementHandle;
                    }
                }
            }

            var quizChoices = await frame.QuerySelectorAllAsync(".content-region .question-choices li.choice button:not(.is-disabled)");
            if (quizChoices.Any())
            {
                var contentHash = await GetQuizHash(frame);

                var bestAnswer = _quiz.Questions.Single(x => x.Hash == contentHash).GetBestAnswer();
                if (bestAnswer != null)
                {
                    foreach (var elementHandle in quizChoices)
                    {
                        var boundingBox = await elementHandle.BoundingBoxAsync();
                        var isVisible = boundingBox != null && boundingBox.Width > 0 && boundingBox.Height > 0;
                        if (!isVisible)
                            continue;

                        var choiceText = await frame.GetInnerText(elementHandle);
                        if (choiceText == bestAnswer)
                            return elementHandle;
                    }
                }
            }

            var regularButtons = await frame.QuerySelectorAllAsync(".content-region button.action:not(.is-disabled)");
            var exampleButtons = await frame.QuerySelectorAllAsync(".example-button-container .button:not(.is-disabled)");
            var buttonHandles = regularButtons.Concat(exampleButtons).Concat(highImportanceButtons);
            foreach (var buttonHandle in buttonHandles)
            {
                var boundingBox = await buttonHandle.BoundingBoxAsync();
                var isVisible = boundingBox != null && boundingBox.Width > 0 && boundingBox.Height > 0;
                if (!isVisible)
                    continue;

                var buttonText = await frame.GetInnerText(buttonHandle);
                var isCandidate = new[] { "beginnen", "weiter", "mehr", "anzeigen", "schließen", "wiederholen" }.Any(x => buttonText.ToLower().Contains(x));
                if (isCandidate)
                    return buttonHandle;
            }

            var currentCalloutHandles = await frame.QuerySelectorAllAsync(".callout-trigger.is-current");
            if (!currentCalloutHandles.Any())
            {
                var calloutHandles = await frame.QuerySelectorAllAsync(".callout-trigger.is-unlocked:not(.is-current)");
                if (calloutHandles.Any())
                    return calloutHandles.First();
            }
            else
            {
                var next = await frame.QuerySelectorAllAsync(".callout-navigation.next a");
                if (next.Any())
                {
                    var boundingBox = await next.First().BoundingBoxAsync();
                    var isVisible = boundingBox != null && boundingBox.Width > 0 && boundingBox.Height > 0;
                    if (isVisible)
                        return next.First();
                }
            }

            var bannerHandles = await frame.QuerySelectorAllAsync(".banner-wrapper.is-unlocked");
            if (bannerHandles.Any())
                return bannerHandles.First();

            var flashCardHandles = await frame.QuerySelectorAllAsync(".content-region article.flash-card:not(.flash-card-flipped) .flash-card-side");
            if (flashCardHandles.Any())
                return flashCardHandles.First();

            return null;
        }

        private async Task<int> GetQuizHash(Frame frame)
        {
            var heading = await frame.TryGetTextContent(".content-heading") ?? "";
            var content = await frame.TryGetTextContent(".content_card") ?? "";
            return (heading + content).GetHashCode();
        }

        private async Task UpdateWrongQuizAnswers(Frame frame)
        {
            var quizChoices = await frame.QuerySelectorAllAsync(".content-region .question-choices li.choice button");

            if (quizChoices.Length > 0)
            {
                var contentHash = await GetQuizHash(frame);

                if (_quiz.Questions.All(x => x.Hash != contentHash))
                {
                    var question = new Question() { Hash = contentHash };
                    foreach (var elementHandle in quizChoices)
                    {
                        var choiceText = await frame.GetInnerText(elementHandle);
                        question.Answers.Add(new Answer { Result = null, Text = choiceText });
                    }
                    
                    _quiz.Questions.Add(question);
                    //seems to be a new quiz, abort for now.
                    return;
                }

                foreach (var elementHandle in quizChoices)
                {
                    var isWrong  = await frame.EvaluateFunctionAsync<bool>("el => el.querySelectorAll(\".icon.icon-incorrect\").length > 0", elementHandle);
                    var choiceText = await frame.EvaluateFunctionAsync<string>("e => e.innerText", elementHandle);

                    if (isWrong)
                        _quiz.Questions.Single(x => x.Hash == contentHash).Answers.Single(x => x.Text == choiceText).Result = false;
                }
            }
        }
    }
}