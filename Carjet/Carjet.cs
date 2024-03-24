using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json;

partial class Carjet
{
    private readonly HttpClient client;

    private readonly HttpClientHandler clientHandler;

    private CookieContainer cc = new();
    private readonly string dataDirectory = "data";

    [GeneratedRegex("window.location.replace\\('([^']*)'\\)")]
    private static partial Regex WindowLocationReplaceValueRegex();

    [GeneratedRegex("<.*?>")]
    private static partial Regex HTMLTagsRegex();

    public Carjet(
        string dataDirectory,
        string? proxyHost = null,
        int? proxyPort = null,
        string? proxyUsername = null,
        string? proxyPassword = null
    )
    {
        clientHandler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip,

            CookieContainer = cc
        };

        this.dataDirectory = dataDirectory;
        // Creating the data directory if it doesn't exist
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        if (proxyHost != null && proxyPort != null)
        {
            var proxyURI = new Uri(string.Format("http://{0}:{1}", proxyHost, proxyPort));

            if (proxyUsername != null && proxyPassword != null)
            {
                var credentials = new NetworkCredential(proxyUsername, proxyPassword);

                clientHandler.Proxy = new WebProxy(proxyURI, false, null, credentials)
                {
                    UseDefaultCredentials = false
                };
            }
            else
            {
                clientHandler.Proxy = new WebProxy(proxyURI, false);
            }
        }

        client = new HttpClient(clientHandler);

        // Set the default headers
        client.DefaultRequestHeaders.Add("host", "www.carjet.com");
        client.DefaultRequestHeaders.Add("connection", "keep-alive");
        client.DefaultRequestHeaders.Add("cache-control", "max-age=0");
        client.DefaultRequestHeaders.Add(
            "sec-ch-ua",
            "\"Google Chrome\";v=\"123\", \"Not:A-Brand\";v=\"8\", \"Chromium\";v=\"123\""
        );
        client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"macOS\"");
        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
        client.DefaultRequestHeaders.Add("origin", "https://www.carjet.com");
        client.DefaultRequestHeaders.Add(
            "user-agent",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36"
        );
        client.DefaultRequestHeaders.Add(
            "accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"
        );
        client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
        client.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
        client.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
        client.DefaultRequestHeaders.Add("sec-fetch-dest", "document");
        client.DefaultRequestHeaders.Add("accept-language", "en-GB,en-US;q=0.9,en;q=0.8");
    }

    public void FetchListingDetail(
        List<KeyValuePair<string, string>> hiddenFormInputs,
        string viewDealButtonURL,
        string id,
        string listingDetailDirectory
    )
    {

        // Creating new key value pair for the hidden form inputs
        // WHere i will change the frmDetailCode to the id
        var hiddenFormInputsLocal = new List<KeyValuePair<string, string>>();

        foreach (var input in hiddenFormInputs)
        {
            if (input.Key == "frmDetailCode")
            {
                hiddenFormInputsLocal.Add(new KeyValuePair<string, string>("frmDetailCode", id));
            }
            else
            {
                hiddenFormInputsLocal.Add(input);
            }
        }

        // Adding the hidden form inputs to the form data
        var postBody = new FormUrlEncodedContent(hiddenFormInputsLocal);

        int errorCount = 0;
        while (errorCount < 5)
        {
            try
            {
                var response = client.PostAsync(viewDealButtonURL, postBody).Result;

                response.EnsureSuccessStatusCode();

                Console.WriteLine("Fetched listing detail for " + id);

                var html = response.Content.ReadAsStringAsync().Result;

                // Writing the response to a file for debugging
                File.WriteAllText(Path.Combine(listingDetailDirectory, id + ".html"), html);

                HtmlDocument doc = new();

                doc.LoadHtml(html);

                // Finding different sections, First section: div containing class summaryBox
                var summaryBox = doc.DocumentNode.SelectSingleNode(".//div[contains(@class, 'summaryBox')]") ?? throw new Exception("Summary box not found");
                var reviewLogos = doc.DocumentNode.SelectSingleNode(".//div[contains(@class, 'reviewLogos')]") ?? throw new Exception("Review logos not found");
                var benefitsContainer = doc.DocumentNode.SelectSingleNode(".//div[contains(@class, 'benefits-container')]") ?? throw new Exception("Benefits container not found");
                // Extracting car image and brand logo
                var carImage = summaryBox.SelectSingleNode(".//img[contains(@class, 'summaryCAR')]")
                    ?.GetAttributeValue("src", null)
                    ?.Replace("/cdn/", "https://www.carjet.com/cdn/");

                var brandLogo = summaryBox.SelectSingleNode(".//img[contains(@class, 'summaryPRV')]")
                    ?.GetAttributeValue("src", null)
                    ?.Replace("/cdn/", "https://www.carjet.com/cdn/");

                // Extracting car name from the h3 with class carName
                var carName = summaryBox.SelectSingleNode(".//h3[contains(@class, 'carName')]")?.InnerText.Trim().Replace("or similar", "");

                var reviews = reviewLogos
                    .SelectNodes(".//div[@class='review-logo']")
                    ?.Select(div => new
                    {
                        platform = div.SelectSingleNode(".//img[contains(@class, 'logo')]")?.GetAttributeValue("title", null),
                        reviewCount = div.SelectSingleNode(".//div[@class='review-logo-nfo']/span[1]/strong")?.InnerText.Trim(),
                        reviewScore = div.SelectSingleNode(".//div[@class='review-logo-nfo']/span[2]/strong")?.InnerText.Trim()
                    })
                    .ToList();

                // Extracting car info from the ul with class cl--info-serv
                var carInfos = summaryBox.SelectSingleNode(".//ul[contains(@class, 'cl--info-serv')]")
                    ?.SelectNodes(".//li")
                    ?.Select(li => WebUtility.HtmlDecode(li.GetAttributeValue("title", "")))
                    ?.Select(info => HTMLTagsRegex().Replace(info, string.Empty))
                    .ToList();

                // Extracting the prices

                // selecting the li with Id detailsInsuranceABF -> span with class price
                var priceWithoutInsurance = summaryBox.SelectSingleNode(".//li[contains(@id, 'detailsInsuranceABF')]//span[contains(@class, 'price')]")?.InnerText;

                // selecting the li with Id detailsInsuranceBBF -> span with class price
                var priceWithInsurance = summaryBox.SelectSingleNode(".//li[contains(@id, 'detailsInsuranceBBF')]//span[contains(@class, 'price')]")?.InnerText;

                var payOnArrival = summaryBox.SelectSingleNode(".//span[contains(@id, 'precioDestinoValueBF')]")?.InnerText.Trim();

                var onlinePrepayment = summaryBox.SelectSingleNode(".//span[contains(@id, 'onlinePrecioValueBF')]")?.InnerText.Trim();

                var benefits = benefitsContainer.SelectSingleNode(".//ul[contains(@class, 'interest')]")
                    ?.SelectNodes(".//li")
                    ?.Select(li => WebUtility.HtmlDecode(li.GetAttributeValue("title", "")))
                    ?.Select(info => HTMLTagsRegex().Replace(info, string.Empty))
                    .ToList();

                var comment = doc.DocumentNode.SelectSingleNode(".//span[contains(@class, 'cuadro-text')]")?.InnerText;

                var tips = doc.DocumentNode.SelectNodes(".//ul[contains(@class, 'tips-list')]//li")
                    ?.Select(li => li.InnerText.Trim())
                    .ToList();

                // Storing all these values in a dictionary and then to a JSON file
                var data = new Dictionary<string, object?>
                {
                    { "carImage", carImage },
                    { "brandLogo", brandLogo },
                    { "carName", carName },
                    { "carInfos", carInfos },
                    { "priceWithoutInsurance", priceWithoutInsurance },
                    { "priceWithInsurance", priceWithInsurance },
                    { "comment", comment },
                    { "payOnArrival", payOnArrival },
                    { "onlinePrepayment", onlinePrepayment },
                    { "benefits", benefits },
                    { "reviews", reviews },
                    { "tips", tips }
                };

                File.WriteAllText(
                    Path.Combine(listingDetailDirectory, id + ".json"),
                    JsonConvert.SerializeObject(data, Formatting.Indented)
                );

                return;
            }
            catch (Exception e)
            {
                
                errorCount++;
                Console.WriteLine("Error fetching listing detail: " + e.Message);
            }
        }
    }

    public void FetchListings(
        string locationCode,
        string startDateISO,
        int startHour,
        int startMinute,
        string endDateISO,
        int endHour,
        int endMinute
    )
    {

        string currentDataDirectory = Path.Combine(dataDirectory, locationCode, startDateISO + "_" + startHour + "_" + startMinute + "_" + endDateISO + "_" + endHour + "_" + endMinute);
        string currentListingDetailDirectory = Path.Combine(currentDataDirectory, "listing_detail");

        if (!Directory.Exists(currentListingDetailDirectory))
        {
            Directory.CreateDirectory(currentListingDetailDirectory);
        }

        int errorCount = 0;
        while (errorCount < 5)
        {
            try
            {
                // First we have to fetch initial cookies with a GET request
                // Before doing this, clear all the cookies
                cc = new CookieContainer();

                clientHandler.CookieContainer = cc;

                var response = client.GetAsync("https://www.carjet.com/").Result;
                response.EnsureSuccessStatusCode();

                Console.WriteLine("Fetched initial cookies");

                // Converting ISO date format to Carjet date format
                var startDate = DateTime.Parse(startDateISO);
                var endDate = DateTime.Parse(endDateISO);

                var postBody = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        { "chkOneWay", "SI" },
                        { "fechaRecogida", startDate.ToString("ddd, dd/MM/yyyy") },
                        { "fechaDevolucion", endDate.ToString("ddd, dd/MM/yyyy") },
                        { "chkAge", "SI" },
                        { "edad", "35" },
                        { "send", "" },
                        { "booster", "0" },
                        { "child_seat", "0" },
                        { "pais", "TR" },
                        { "destino", locationCode },
                        { "destino_final", "" },
                        { "horarecogida", startHour.ToString() },
                        { "minutosrecogida", startMinute.ToString() },
                        { "horadevolucion", endHour.ToString() },
                        { "minutosdevolucion", endMinute.ToString() },
                        { "ecruos_muidem", "(direct) / (none)" },
                    }
                );

                response = client.PostAsync("https://www.carjet.com/do/list/en", postBody).Result;

                response.EnsureSuccessStatusCode();

                Console.WriteLine("Fetched listings");

                var html = response.Content.ReadAsStringAsync().Result;

                // If window.location.replace is not found, then we have the incorrect response
                if (html.Contains("window.location.replace"))
                {
                    // Extract the URL from the response
                    string url = WindowLocationReplaceValueRegex().Match(html).Groups[1].Value;

                    url = "https://www.carjet.com" + url;

                    response = client.GetAsync(url).Result;

                    response.EnsureSuccessStatusCode();

                    html = response.Content.ReadAsStringAsync().Result;
                }

                if (!html.Contains("function submitNext(action, value) {"))
                {
                    // Write the response to a file for debugging
                    File.WriteAllText("error.html", html);
                    throw new Exception("Invalid response for listing fetch");
                }

                // Writing the response to a file for debugging
                File.WriteAllText(Path.Combine(dataDirectory, "listings.html"), html);

                HtmlDocument doc = new();

                doc.LoadHtml(html);

                // Fetch all the articles tag inside the section with class "newcarlist"

                var articles = doc.DocumentNode.SelectNodes(
                    "//section[contains(@class, 'newcarlist')]/article"
                );

                Console.WriteLine("Found " + (articles?.Count ?? 0) + " listings");

                if (articles == null || articles.Count == 0)
                {
                    throw new Exception("No listings found");
                }

                var hiddenFormInputs = doc
                    .DocumentNode.SelectNodes("//input[@type='hidden']")
                    .Select(input => new KeyValuePair<string, string>(
                        input.GetAttributeValue("name", ""),
                        input.GetAttributeValue("value", "")
                    ))
                    .ToList();

                // Writing it in console

                var listings = new List<Dictionary<string, object?>>();

                if (articles != null)
                {
                    foreach (var article in articles)
                    {
                        // Extract the car image (img with class name "cl--car-img)
                        var image = article
                            .SelectSingleNode(".//img[contains(@class, 'car-img')]")
                            ?.GetAttributeValue("src", null)
                            ?.Replace("/cdn/", "https://www.carjet.com/cdn/")
                            ?.Trim();

                        // Extract the car name (inside the div with class "cl--name", find h2 tag)
                        var name = article
                            .SelectSingleNode(".//div[@class='cl--name']//h2")
                            ?.InnerText?.Trim();

                        // Extract the car type (inside the div with class "cl--name", span with class cl--name-type)
                        var type = article
                            .SelectSingleNode(
                                ".//div[@class='cl--name']//span[@class='cl--name-type']"
                            )
                            ?.InnerText?.Trim();

                        // Extract car info (inside the div with class "cl--info") -> al the li tags find title attribute
                        var infos = article
                            .SelectNodes(".//ul[@class='cl--info-serv']/li")
                            ?.Select(li => WebUtility.HtmlDecode(li.GetAttributeValue("title", "")))
                            ?.Select(info => HTMLTagsRegex().Replace(info, string.Empty))
                            .ToList();

                        // Extract car interests (inside the div with class "cl--interest") -> all the li tags find title attribute
                        var interests = article
                            .SelectNodes(".//ul[@class='cl--interest']/li")
                            ?.Select(li => WebUtility.HtmlDecode(li.GetAttributeValue("title", "")))
                            ?.Select(info => HTMLTagsRegex().Replace(info, string.Empty))
                            .ToList();

                        // Extract the price (inside the span with class "price pr-euros")
                        var price = article
                            .SelectSingleNode(".//span[@class='price pr-euros']")
                            ?.InnerText;

                        // Extract the old price (inside the span with class "price old-price old-price-euros")
                        var oldPrice = article
                            .SelectSingleNode(".//span[@class='price old-price old-price-euros']")
                            ?.InnerText;

                        // Extract the price per day (inside the em with class "price-day-euros")
                        var pricePerDay = article
                            .SelectSingleNode(".//em[@class='price-day-euros']")
                            ?.InnerText;

                        // Extract the rent platform (img tag inside the span tag with class "cl--car-rent-logo"), get the alt attribute and img src
                        var rentPlatform = article
                            .SelectSingleNode(".//span[@class='cl--car-rent-logo']//img")
                            ?.GetAttributeValue("alt", null);

                        // Extract the rent platform image
                        var rentPlatformImage = article
                            .SelectSingleNode(".//span[@class='cl--car-rent-logo']//img")
                            ?.GetAttributeValue("src", null)
                            ?.Replace("/cdn/", "https://www.carjet.com/cdn/");

                        // Find the final View Deal button inside the div with class "cl--action-container" -> input tag with type button
                        // We need to extract onclick attribute
                        var viewDealButtonOnClickValue = (article
                            .SelectSingleNode(
                                ".//div[@class='cl--action-container']//input[@type='button']"
                            )
                            ?.GetAttributeValue("onclick", null)) ?? throw new Exception("View Deal button not found");

                        // The format for onClick value is submitNext('/do/detail/en?s=b3819659-d297-4f4b-a68e-29971d303bf8&b=f08fede9-423c-4e2e-b50d-f33df3950f61','QUxNfEVMMDN8ZmZ8fGZhbHNlfElMSU1JVEFET3w3OS4yMHww');
                        // The first parameter is the URL, the second parameter is the base64 encoded string, we will consider the 2nd parameter as id
                        var viewDealButtonOnClickValueMatch = Regex.Match(
                            viewDealButtonOnClickValue,
                            "submitNext\\('([^']*)','([^']*)'\\)"
                        );

                        var id = viewDealButtonOnClickValueMatch.Groups[2].Value;
                        var viewDealButtonURL = viewDealButtonOnClickValueMatch.Groups[1].Value.Replace("/do/detail/en", "https://www.carjet.com/do/detail/en");

                        // Storing all these values in a dictionary and then to a JSON file
                        var data = new Dictionary<string, object?>
                        {
                            { "image", image },
                            { "name", name },
                            { "type", type },
                            { "infos", infos },
                            { "interests", interests },
                            { "price", price },
                            { "oldPrice", oldPrice },
                            { "pricePerDay", pricePerDay },
                            { "rentPlatform", rentPlatform },
                            { "rentPlatformImage", rentPlatformImage },
                            { "id", id }
                        };

                        listings.Add(data);

                        Console.WriteLine("Fetched listing " + id);

                        FetchListingDetail(hiddenFormInputs, viewDealButtonURL, id, currentListingDetailDirectory);
                    }
                }

                File.WriteAllText(
                    Path.Combine(currentDataDirectory, "listings.json"),
                    JsonConvert.SerializeObject(listings, Formatting.Indented)
                );

                return;
            }
            catch (Exception e)
            {
                errorCount++;
                Console.WriteLine("Error fetching listings: " + e.Message);
            }
        }

        throw new Exception("Failed to fetch listings");
    }
}
