using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinePilot.Tests;

/// <summary>Boots the real app in memory and exercises pages over HTTP.</summary>
public class WebTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/", "Daily output")]
    [InlineData("/WorkOrders", "Work orders")]
    [InlineData("/WorkOrders?status=InProgress", "In progress")]
    [InlineData("/WorkOrders/Create", "New work order")]
    [InlineData("/Machines", "Machines")]
    [InlineData("/Machines/Details/1", "Downtime log")]
    [InlineData("/Inventory?low=true", "Low stock")]
    public async Task Pages_render(string url, string expectedText)
    {
        var response = await factory.CreateClient().GetAsync(url);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedText, html);
        Assert.Contains("Designed &amp; Built by", html);
    }

    [Fact]
    public async Task Unknown_work_order_returns_404()
    {
        var response = await factory.CreateClient().GetAsync("/WorkOrders/Details/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var response = await factory.CreateClient().PostAsync("/Inventory/Receive/1",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Quantity"] = "10" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_work_order_with_a_valid_token_redirects_to_its_page()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var form = await client.GetStringAsync("/WorkOrders/Create");
        var token = Regex.Match(form, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/WorkOrders/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ProductId"] = "1",
            ["LineId"] = "2",
            ["QuantityPlanned"] = "750",
            ["Priority"] = "High",
            ["DueDate"] = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd"),
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/WorkOrders/Details/", response.Headers.Location?.OriginalString);
    }
}
