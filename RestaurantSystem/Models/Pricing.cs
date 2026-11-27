using Microsoft.Extensions.Configuration;
using System;

namespace RestaurantSystem.Models
{
    /// <summary>
    /// Single source of truth for order totals (Workflow.md §2.4).
    ///
    /// Cart, checkout, payment, confirmation, receipt PDFs and the reports all call this, so a
    /// total can never disagree between two screens. Values are read from configuration
    /// ("RestaurantSettings" in appsettings.json) so they can be changed without a code edit.
    ///
    /// Server-side validation matters here (Workflow.md §8.10): posted totals are never trusted,
    /// the controller always recalculates with prices freshly read from the database.
    /// </summary>
    public class PricingSettings
    {
        // Percentages are stored as whole numbers, e.g. 6 means 6%.
        public decimal TaxPercent { get; set; } = 6m;
        public decimal ServiceChargePercent { get; set; } = 10m;
        public decimal DeliveryFee { get; set; } = 5m;

        /// <summary>Service charge is not applied to takeaway/delivery in most restaurants; our
        /// rule: dine-in only, which matches how the receipt presents it.</summary>
        public bool ServiceChargeAppliesTo(string? orderType) => orderType == OrderType.DineIn;

        public static PricingSettings FromConfiguration(IConfiguration config)
        {
            var section = config.GetSection("RestaurantSettings");
            var s = new PricingSettings();
            if (section.Exists())
            {
                section.Bind(s);
            }
            return s;
        }
    }

    /// <summary>The money breakdown for one order.</summary>
    public readonly record struct OrderTotals(
        decimal Subtotal,
        decimal ServiceCharge,
        decimal Tax,
        decimal DeliveryFee,
        decimal Discount,
        decimal Total)
    {
        public static OrderTotals Empty => new(0m, 0m, 0m, 0m, 0m, 0m);
    }

    public static class Pricing
    {
        /// <summary>
        /// builds the full breakdown for a cart.
        ///  subtotal     = sum of line totals (unit price + add-on prices) × quantity
        ///  service      = only for dine-in
        ///  tax          = on (subtotal + service)
        ///  deliveryFee  = only for delivery
        ///  total        = subtotal + service + tax + delivery - discount
        /// </summary>
        public static OrderTotals Calculate(
            decimal subtotal,
            string? orderType,
            PricingSettings settings,
            decimal discount = 0m)
        {
            subtotal = Math.Max(0m, subtotal);
            discount = Math.Max(0m, discount);

            decimal service = settings.ServiceChargeAppliesTo(orderType)
                ? Round(subtotal * settings.ServiceChargePercent / 100m)
                : 0m;

            decimal tax = Round((subtotal + service) * settings.TaxPercent / 100m);

            decimal delivery = orderType == OrderType.Delivery ? settings.DeliveryFee : 0m;

            decimal total = Round(subtotal + service + tax + delivery - discount);
            if (total < 0m) total = 0m;

            return new OrderTotals(subtotal, service, tax, delivery, discount, total);
        }

        private static decimal Round(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
