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

using System.Text;
using BotWire.AspNetCore;
using BotWire.Channels.Email;

// Render non-ASCII (e.g. 中文) log output correctly instead of "??" in the console.
Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

var apiKey  = Environment.GetEnvironmentVariable("BOTWIRE_TEST_API_KEY")
    ?? throw new InvalidOperationException("BOTWIRE_TEST_API_KEY environment variable is not set.");
var model   = Environment.GetEnvironmentVariable("BOTWIRE_TEST_MODEL")   ?? "gpt-4o-mini";
var baseUrl = Environment.GetEnvironmentVariable("BOTWIRE_TEST_BASE_URL");

builder.Services.AddBotWire(opts =>
{
    // opts.LicenseKey = "XXXX-XXXX-XXXX-XXXX"; // Commercial license key (required for closed-source use)
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
})
.AddJsonAuditLog(Path.Combine(AppContext.BaseDirectory, "logs", "audit"));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.MapBotWire();
app.Run();
