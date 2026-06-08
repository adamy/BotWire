// BotWire
// Copyright (C) 2026  Object IT Limited
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BotWire.AspNetCore.IntegrationTests;

/// <summary>
/// In-memory ASP.NET Core host for integration tests.
/// Creates a real <see cref="WebApplication"/> backed by <see cref="TestServer"/>;
/// no network I/O except when an LLM API key is supplied.
/// </summary>
internal sealed class BotWireTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private BotWireTestHost(WebApplication app)
    {
        _app   = app;
        Client = app.GetTestClient();
    }

    /// <summary>Pre-configured <see cref="HttpClient"/> backed by the in-memory server.</summary>
    public HttpClient Client { get; }

    /// <summary>
    /// Builds and starts a test host.
    /// </summary>
    /// <param name="maxRpm">IP rate-limit cap. Defaults to 1000 to avoid interference between tests.</param>
    /// <param name="documents">Fixture file names to load as knowledge-base documents. Defaults to <c>["support-faq.md"]</c>.</param>
    /// <param name="extra">Optional callback to apply additional <see cref="BotWireOptions"/> overrides.</param>
    public static async Task<BotWireTestHost> CreateAsync(
        int maxRpm = 1000,
        string[]? documents = null,
        Action<BotWireOptions>? extra = null)
    {
        var apiKey  = Environment.GetEnvironmentVariable("BOTWIRE_TEST_API_KEY") ?? "sk-test";
        var model   = Environment.GetEnvironmentVariable("BOTWIRE_TEST_MODEL")   ?? "gpt-4o-mini";
        var baseUrl = Environment.GetEnvironmentVariable("BOTWIRE_TEST_BASE_URL");  // null = standard OpenAI

        var docs = (documents ?? ["support-faq.md"]).Select(FixtureFile).ToList();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddBotWire(opts =>
        {
            opts.TopicDescription          = "Online store customer support";
            opts.Documents                 = docs;
            opts.MaxRequestsPerIpPerMinute = maxRpm;
            opts.ChatProvider              = new OpenAIProviderOptions
            {
                ApiKey  = apiKey,
                Model   = model,
                BaseUrl = baseUrl,
            };
            extra?.Invoke(opts);
        });

        var app = builder.Build();
        app.UseCors();
        app.MapBotWire();
        await app.StartAsync();
        return new BotWireTestHost(app);
    }

    private static string FixtureFile(string name)
        => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
