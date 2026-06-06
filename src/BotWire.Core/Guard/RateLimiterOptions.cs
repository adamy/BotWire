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

using System.ComponentModel.DataAnnotations;

namespace BotWire.Core.Guard;

/// <summary>Configuration for per-IP sliding-window rate limiting.</summary>
public sealed class RateLimiterOptions
{
    /// <summary>Maximum number of requests allowed per IP address within any rolling 60-second window. Defaults to 20.</summary>
    [Range(1, int.MaxValue)]
    public int MaxRequestsPerIpPerMinute { get; set; } = 20;
}
