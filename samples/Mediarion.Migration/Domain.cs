using System;
using System.Collections.Generic;

namespace MigrationSample
{
    /// <summary>
    /// The domain both versions of the layer sit on top of. It knows nothing about either
    /// library, which is the point: what is being compared is the wiring, not the work.
    /// </summary>
    public sealed class Basket
    {
        public string Customer { get; set; } = string.Empty;

        public List<BasketLine> Lines { get; } = new List<BasketLine>();
    }

    public sealed class BasketLine
    {
        public string Sku { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
    }

    public sealed class Receipt
    {
        public string Reference { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public int Lines { get; set; }
    }

    /// <summary>
    /// Everything the layer does that can be seen from outside, in the order it happened.
    /// </summary>
    /// <remarks>
    /// The two versions write into one of these each, and the run is a pass only when the two
    /// read the same line for line. An assertion on the response alone would miss a behaviour
    /// that stopped running or a notification handler that never fired.
    /// </remarks>
    public sealed class Trace
    {
        public List<string> Entries { get; } = new List<string>();

        public void Add(string entry) => Entries.Add(entry);

        public IReadOnlyList<string> Lines => Entries;
    }

    public sealed class Prices
    {
        private readonly int decimals;

        public Prices()
            : this(2)
        {
        }

        public Prices(int decimals)
        {
            this.decimals = decimals;
        }

        public decimal Of(BasketLine line) =>
            Math.Round(line.UnitPrice * line.Quantity, decimals);
    }

    public static class Reference
    {
        /// <remarks>
        /// Fixed rather than random, because two runs have to be comparable.
        /// </remarks>
        public static string For(Basket basket) =>
            "ORD-" + basket.Customer.ToUpperInvariant() + "-" + basket.Lines.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
    }
}
