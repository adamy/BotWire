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

namespace BotWire.Core.Exceptions;

/// <summary>
/// Thrown when BotWire is misconfigured in a way that cannot be recovered from at runtime,
/// e.g. a RAG document set that exceeds the Phase 1 token budget.
/// </summary>
public sealed class BotWireConfigurationException : Exception
{
    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">A description of the configuration problem.</param>
    public BotWireConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with the given message and inner exception.</summary>
    /// <param name="message">A description of the configuration problem.</param>
    /// <param name="innerException">The underlying cause.</param>
    public BotWireConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
