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

namespace BotWire.Core.Enums;

/// <summary>Priority level suggested by the AI for a support ticket.</summary>
public enum TicketPriority
{
    /// <summary>Non-urgent; can be addressed in normal queue order.</summary>
    Low,

    /// <summary>Standard priority for most support requests.</summary>
    Medium,

    /// <summary>Requires prompt attention ahead of normal queue.</summary>
    High,

    /// <summary>Requires immediate attention; customer is severely impacted.</summary>
    Urgent,
}
