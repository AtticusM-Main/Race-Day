using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests.Infrastructure;

public class WebTestFactory : WebApplicationFactory<Program>
{
    public FakeRaceDayApi Api { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Api:BaseUrl", "http://api.invalid");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRaceDayApi>();
            services.AddSingleton<IRaceDayApi>(Api);
        });
    }

    public HttpClient NewBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });
}

public static class BrowserExtensions
{
    private static readonly Regex TokenPattern = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>Loads a page that contains a form and returns the antiforgery token the browser would post back.</summary>
    public static async Task<string> GetTokenAsync(this HttpClient browser, string url)
    {
        var html = await browser.GetStringAsync(url);
        var match = TokenPattern.Match(html);
        Assert.True(match.Success, $"No antiforgery token found on {url}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient browser, string formPage, string postUrl, Dictionary<string, string> fields)
    {
        var token = await browser.GetTokenAsync(formPage);
        fields["__RequestVerificationToken"] = token;
        return await browser.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }

    public static async Task LoginAsync(this HttpClient browser, string email)
    {
        var response = await browser.PostFormAsync("/Account/Login", "/Account/Login", new()
        {
            ["Email"] = email,
            ["Password"] = FakeRaceDayApi.Password
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
