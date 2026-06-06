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

using BotWire.Core.Ticket;

namespace BotWire.Core.Tests.Ticket;

public class TicketIdGeneratorTests
{
    [Fact]
    public void Next_ReturnsCorrectFormat()
    {
        var id = TicketIdGenerator.Next("TKT");
        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");

        Assert.Matches($@"^TKT-{today}-\d{{4,}}$", id);
    }

    [Fact]
    public void Next_CustomPrefix_AppearsInId()
    {
        var id = TicketIdGenerator.Next("ACME");
        Assert.StartsWith("ACME-", id);
    }

    [Fact]
    public void Next_SequenceIncrementsAcrossCalls()
    {
        var id1 = TicketIdGenerator.Next("TKT");
        var id2 = TicketIdGenerator.Next("TKT");

        var seq1 = long.Parse(id1.Split('-')[^1]);
        var seq2 = long.Parse(id2.Split('-')[^1]);

        Assert.True(seq2 > seq1);
    }

    [Fact]
    public void Next_SequenceIsFourDigitsPadded()
    {
        var id = TicketIdGenerator.Next("TKT");
        var seq = id.Split('-')[^1];

        Assert.True(seq.Length >= 4);
        Assert.True(long.TryParse(seq, out _));
    }
}
