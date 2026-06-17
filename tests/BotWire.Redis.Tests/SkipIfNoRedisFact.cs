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

using StackExchange.Redis;

namespace BotWire.Redis.Tests;

/// <summary>
/// Skips the test when Redis is not reachable at <c>localhost:6379</c>.
/// Run Redis via Docker Desktop before running these tests locally.
/// </summary>
public sealed class SkipIfNoRedisFact : FactAttribute
{
    internal const string ConnectionString = "localhost:6379,connectTimeout=1000,syncTimeout=1000";

    public SkipIfNoRedisFact()
    {
        try
        {
            using var mux = ConnectionMultiplexer.Connect(
                ConfigurationOptions.Parse(ConnectionString + ",abortConnect=false"));
            if (!mux.IsConnected)
                Skip = "Redis not reachable at localhost:6379";
        }
        catch
        {
            Skip = "Redis not reachable at localhost:6379";
        }
    }
}
