using BotWire.AspNetCore;
using BotWire.Channels.Email;

var builder = WebApplication.CreateBuilder(args);

var apiKey  = Environment.GetEnvironmentVariable("BOTWIRE_TEST_API_KEY")
    ?? throw new InvalidOperationException("BOTWIRE_TEST_API_KEY environment variable is not set.");
var model   = Environment.GetEnvironmentVariable("BOTWIRE_TEST_MODEL")   ?? "gpt-4o-mini";
var baseUrl = Environment.GetEnvironmentVariable("BOTWIRE_TEST_BASE_URL");

builder.Services.AddBotWire(opts =>
{
    opts.TopicDescription = "Customer support for Acme Store";
    opts.Documents        = [Path.Combine(AppContext.BaseDirectory, "docs", "faq.md")];
    opts.ChatProvider     = new OpenAIProviderOptions
    {
        ApiKey  = apiKey,
        Model   = model,
        BaseUrl = baseUrl,
    };
    opts.Email = new EmailOptions
    {
        SmtpHost    = "localhost",
        Port        = 1025,
        UseSsl      = false,
        FromAddress = "support@acme.example",
        ToAddress   = "support-team@acme.example",
    };
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.MapBotWire();
app.Run();
