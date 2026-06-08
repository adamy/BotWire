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

namespace BotWire.AspNetCore.IntegrationTests;

/// <summary>Skips the test when <c>BOTWIRE_TEST_API_KEY</c> is not set in the environment.</summary>
public sealed class SkipIfNoApiKeyFact : FactAttribute
{
    public SkipIfNoApiKeyFact()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BOTWIRE_TEST_API_KEY")))
            Skip = "BOTWIRE_TEST_API_KEY not set";
    }
}

/// <summary>
/// Skips the test when <c>BOTWIRE_TEST_API_KEY</c> or <c>MAILPIT_ENABLED</c> is not set.
/// Apply alongside <c>[Trait("Category", "RequiresMailpit")]</c>.
/// </summary>
public sealed class SkipIfNoMailpitFact : FactAttribute
{
    public SkipIfNoMailpitFact()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BOTWIRE_TEST_API_KEY")))
            Skip = "BOTWIRE_TEST_API_KEY not set";
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAILPIT_ENABLED")))
            Skip = "MAILPIT_ENABLED not set";
    }
}
