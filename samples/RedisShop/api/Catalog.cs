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

namespace RedisShop.Api;

/// <summary>A single catalog product. Kept in sync with the FAQ doc the bot answers from.</summary>
public sealed record Product(
    string Id,
    string Name,
    string Category,
    decimal Price,
    string Description,
    bool InStock);

/// <summary>Hardcoded sample catalog for the Acme Store shopfront testbed.</summary>
public static class Catalog
{
    public static readonly IReadOnlyList<Product> Products =
    [
        new("acme-tote",     "Acme Canvas Tote Bag",       "Bags",
            24.99m,  "Heavy-duty 16oz canvas tote with reinforced handles and inner pocket.", InStock: true),
        new("acme-bottle",   "Acme Insulated Water Bottle","Drinkware",
            19.99m,  "Double-walled stainless steel, 24oz, keeps drinks cold 24h / hot 12h.", InStock: true),
        new("acme-hoodie",   "Acme Classic Hoodie",        "Apparel",
            49.00m,  "Brushed-fleece pullover hoodie, unisex, available in five colours.",    InStock: true),
        new("acme-mug",      "Acme Ceramic Mug",           "Drinkware",
            12.50m,  "11oz glazed ceramic mug, dishwasher and microwave safe.",               InStock: false),
        new("acme-cap",      "Acme Embroidered Cap",       "Apparel",
            18.00m,  "Six-panel cotton twill cap with adjustable strap and embroidered logo.",InStock: true),
        new("acme-notebook", "Acme Dot-Grid Notebook",     "Stationery",
            14.00m,  "A5 hardcover dot-grid notebook, 192 pages, lay-flat binding.",          InStock: true),
    ];
}
