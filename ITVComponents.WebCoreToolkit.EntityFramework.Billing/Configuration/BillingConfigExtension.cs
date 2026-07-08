using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ITVComponents.EFRepo.DataSync;
using ITVComponents.EFRepo.DataSync.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Configuration
{
    /// <summary>
    /// Contributes the billing catalog (plans/add-ons/links/prices/features) to the system-configuration
    /// export and diff. Describe reads the catalog into <see cref="BillingConfigMarkup"/>; Compare emits standard
    /// <see cref="Change"/> objects that the generic apply engine persists. Cross-references are resolved by name
    /// (ids differ per system). Provider ids and subscriptions are not touched.
    /// </summary>
    public class BillingConfigExtension : IConfigExtension
    {
        public string SectionKey => BillingConfigMarkup.SectionName;

        public ConfigExtensionMarkup Describe(DbContext db)
        {
            if (db is not IBillingContext ctx)
            {
                return null;
            }

            var plans = ctx.Plans
                .Include(p => p.Prices)
                .Include(p => p.Features)
                .Include(p => p.PlanAddOns).ThenInclude(pa => pa.Prices)
                .Include(p => p.PlanAddOns).ThenInclude(pa => pa.AddOn)
                .AsNoTracking().ToList();
            var addOns = ctx.AddOns.Include(a => a.Features).AsNoTracking().ToList();

            return new BillingConfigMarkup
            {
                SectionKey = BillingConfigMarkup.SectionName,
                AddOns = addOns.OrderBy(a => a.Name).Select(a => new BillingAddOnMarkup
                {
                    Name = a.Name,
                    Description = a.Description,
                    IsActive = a.IsActive,
                    Features = a.Features.Select(f => f.FeatureKey).OrderBy(k => k).ToArray()
                }).ToArray(),
                Plans = plans.OrderBy(p => p.Name).Select(p => new BillingPlanMarkup
                {
                    Name = p.Name,
                    Description = p.Description,
                    BillingInterval = p.BillingInterval,
                    TrialDays = p.TrialDays,
                    SeatCount = p.SeatCount,
                    IsActive = p.IsActive,
                    Prices = p.Prices.OrderBy(pr => pr.Currency).Select(ToPriceMarkup).ToArray(),
                    Features = p.Features.Select(f => f.FeatureKey).OrderBy(k => k).ToArray(),
                    AddOns = p.PlanAddOns.Where(pa => pa.AddOn != null).OrderBy(pa => pa.AddOn.Name).Select(pa => new BillingPlanAddOnMarkup
                    {
                        AddOnName = pa.AddOn.Name,
                        Prices = pa.Prices.OrderBy(pr => pr.Currency).Select(ToPriceMarkup).ToArray()
                    }).ToArray()
                }).ToArray()
            };
        }

        public IEnumerable<Change> Compare(DbContext db, ConfigExtensionMarkup current, ConfigExtensionMarkup uploaded, IConfigChangeContext changes)
        {
            var cur = current as BillingConfigMarkup ?? new BillingConfigMarkup();
            var up = uploaded as BillingConfigMarkup ?? new BillingConfigMarkup();
            var result = new List<Change>();

            // Parents first (inserts are applied in emission order): add-ons, then plans, then the plan/add-on links.
            CompareAddOns(cur.AddOns ?? Array.Empty<BillingAddOnMarkup>(), up.AddOns ?? Array.Empty<BillingAddOnMarkup>(), changes, result);
            ComparePlans(cur.Plans ?? Array.Empty<BillingPlanMarkup>(), up.Plans ?? Array.Empty<BillingPlanMarkup>(), changes, result);
            ComparePlanAddOns(cur.Plans ?? Array.Empty<BillingPlanMarkup>(), up.Plans ?? Array.Empty<BillingPlanMarkup>(), changes, result);
            return result;
        }

        // -- add-ons ---------------------------------------------------------------------------------------

        private static void CompareAddOns(BillingAddOnMarkup[] cur, BillingAddOnMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            foreach (var (curA, upA) in JoinBy(cur, up, a => a.Name))
            {
                if (curA != null && upA == null)
                {
                    // Features cascade (AddOn -> AddOnFeature) on delete; links (Restrict) are removed via the plans.
                    result.Add(new Change { ChangeType = ChangeType.Delete, EntityName = "AddOns", Apply = true, DeletePriority = 2, Key = Key(("Name", curA.Name)) });
                }
                else if (curA == null && upA != null)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "AddOns", Apply = true };
                    c.Details.Add(ch.MakeDetail("Name", upA.Name));
                    c.Details.Add(ch.MakeDetail("Description", upA.Description ?? string.Empty));
                    c.Details.Add(ch.MakeDetail("IsActive", upA.IsActive.ToString(), "Entity.IsActive=(NewValueRaw==\"True\")"));
                    result.Add(c);
                    CompareAddOnFeatures(upA.Name, Array.Empty<string>(), upA.Features ?? Array.Empty<string>(), ch, result);
                }
                else if (curA != null)
                {
                    var c = new Change { ChangeType = ChangeType.Update, EntityName = "AddOns", Apply = true, Key = Key(("Name", curA.Name)) };
                    if (TextChanged(upA.Description, curA.Description))
                    {
                        c.Details.Add(ch.MakeDetail("Description", upA.Description ?? string.Empty, currentValue: curA.Description));
                    }

                    if (upA.IsActive != curA.IsActive)
                    {
                        c.Details.Add(ch.MakeDetail("IsActive", upA.IsActive.ToString(), "Entity.IsActive=(NewValueRaw==\"True\")", curA.IsActive.ToString()));
                    }

                    if (c.Details.Count != 0)
                    {
                        result.Add(c);
                    }

                    CompareAddOnFeatures(curA.Name, curA.Features ?? Array.Empty<string>(), upA.Features ?? Array.Empty<string>(), ch, result);
                }
            }
        }

        private static void CompareAddOnFeatures(string addOnName, string[] cur, string[] up, IConfigChangeContext ch, List<Change> result)
        {
            foreach (var (value, inCur, inUp) in JoinKeys(cur, up))
            {
                if (inCur && !inUp)
                {
                    result.Add(new Change
                    {
                        ChangeType = ChangeType.Delete, EntityName = "AddOnFeatures", Apply = true, DeletePriority = 0,
                        Key = Key(("FeatureKey", value), ("AddOn", addOnName)),
                        KeyExpression = new Dictionary<string, string> { { "AddOn", ch.MakeLinqQuery("AddOns", "Name") } }
                    });
                }
                else if (!inCur && inUp)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "AddOnFeatures", Apply = true };
                    c.Details.Add(ch.MakeDetail("FeatureKey", value));
                    c.Details.Add(ch.MakeDetail("AddOn", addOnName, ch.MakeLinqAssign("AddOn", "AddOns", "Name")));
                    result.Add(c);
                }
            }
        }

        // -- plans -----------------------------------------------------------------------------------------

        private static void ComparePlans(BillingPlanMarkup[] cur, BillingPlanMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            foreach (var (curP, upP) in JoinBy(cur, up, p => p.Name))
            {
                if (curP != null && upP == null)
                {
                    // Prices/features/links cascade (Plan -> *) on delete.
                    result.Add(new Change { ChangeType = ChangeType.Delete, EntityName = "Plans", Apply = true, DeletePriority = 2, Key = Key(("Name", curP.Name)) });
                }
                else if (curP == null && upP != null)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "Plans", Apply = true };
                    c.Details.Add(ch.MakeDetail("Name", upP.Name));
                    c.Details.Add(ch.MakeDetail("Description", upP.Description ?? string.Empty));
                    c.Details.Add(ch.MakeDetail("BillingInterval", upP.BillingInterval.ToString()));
                    c.Details.Add(ch.MakeDetail("IsActive", upP.IsActive.ToString(), "Entity.IsActive=(NewValueRaw==\"True\")"));
                    if (upP.TrialDays.HasValue)
                    {
                        c.Details.Add(ch.MakeDetail("TrialDays", upP.TrialDays.Value.ToString(CultureInfo.InvariantCulture)));
                    }

                    if (upP.SeatCount.HasValue)
                    {
                        c.Details.Add(ch.MakeDetail("SeatCount", upP.SeatCount.Value.ToString(CultureInfo.InvariantCulture)));
                    }

                    result.Add(c);
                    ComparePlanPrices(upP.Name, Array.Empty<BillingPriceMarkup>(), upP.Prices ?? Array.Empty<BillingPriceMarkup>(), ch, result);
                    ComparePlanFeatures(upP.Name, Array.Empty<string>(), upP.Features ?? Array.Empty<string>(), ch, result);
                }
                else if (curP != null)
                {
                    var c = new Change { ChangeType = ChangeType.Update, EntityName = "Plans", Apply = true, Key = Key(("Name", curP.Name)) };
                    if (TextChanged(upP.Description, curP.Description))
                    {
                        c.Details.Add(ch.MakeDetail("Description", upP.Description ?? string.Empty, currentValue: curP.Description));
                    }

                    if (upP.BillingInterval != curP.BillingInterval)
                    {
                        c.Details.Add(ch.MakeDetail("BillingInterval", upP.BillingInterval.ToString(), currentValue: curP.BillingInterval.ToString()));
                    }

                    if (upP.IsActive != curP.IsActive)
                    {
                        c.Details.Add(ch.MakeDetail("IsActive", upP.IsActive.ToString(), "Entity.IsActive=(NewValueRaw==\"True\")", curP.IsActive.ToString()));
                    }

                    AddNullableIntUpdate(c, ch, "TrialDays", upP.TrialDays, curP.TrialDays);
                    AddNullableIntUpdate(c, ch, "SeatCount", upP.SeatCount, curP.SeatCount);

                    if (c.Details.Count != 0)
                    {
                        result.Add(c);
                    }

                    ComparePlanPrices(curP.Name, curP.Prices ?? Array.Empty<BillingPriceMarkup>(), upP.Prices ?? Array.Empty<BillingPriceMarkup>(), ch, result);
                    ComparePlanFeatures(curP.Name, curP.Features ?? Array.Empty<string>(), upP.Features ?? Array.Empty<string>(), ch, result);
                }
            }
        }

        private static void ComparePlanFeatures(string planName, string[] cur, string[] up, IConfigChangeContext ch, List<Change> result)
        {
            foreach (var (value, inCur, inUp) in JoinKeys(cur, up))
            {
                if (inCur && !inUp)
                {
                    result.Add(new Change
                    {
                        ChangeType = ChangeType.Delete, EntityName = "PlanFeatures", Apply = true, DeletePriority = 0,
                        Key = Key(("FeatureKey", value), ("Plan", planName)),
                        KeyExpression = new Dictionary<string, string> { { "Plan", ch.MakeLinqQuery("Plans", "Name") } }
                    });
                }
                else if (!inCur && inUp)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "PlanFeatures", Apply = true };
                    c.Details.Add(ch.MakeDetail("FeatureKey", value));
                    c.Details.Add(ch.MakeDetail("Plan", planName, ch.MakeLinqAssign("Plan", "Plans", "Name")));
                    result.Add(c);
                }
            }
        }

        private static void ComparePlanPrices(string planName, BillingPriceMarkup[] cur, BillingPriceMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            foreach (var (currency, curPr, upPr) in JoinPrices(cur, up))
            {
                if (curPr != null && upPr == null)
                {
                    result.Add(new Change
                    {
                        ChangeType = ChangeType.Delete, EntityName = "PlanPrices", Apply = true, DeletePriority = 0,
                        Key = Key(("Currency", currency), ("Plan", planName)),
                        KeyExpression = new Dictionary<string, string> { { "Plan", ch.MakeLinqQuery("Plans", "Name") } }
                    });
                }
                else if (curPr == null && upPr != null)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "PlanPrices", Apply = true };
                    c.Details.Add(ch.MakeDetail("Currency", upPr.Currency));
                    c.Details.Add(ch.MakeDetail("Amount", Money(upPr.Amount)));
                    c.Details.Add(ch.MakeDetail("Plan", planName, ch.MakeLinqAssign("Plan", "Plans", "Name")));
                    result.Add(c);
                }
                else if (curPr != null && upPr.Amount != curPr.Amount)
                {
                    var c = new Change
                    {
                        ChangeType = ChangeType.Update, EntityName = "PlanPrices", Apply = true,
                        Key = Key(("Currency", currency), ("Plan", planName)),
                        KeyExpression = new Dictionary<string, string> { { "Plan", ch.MakeLinqQuery("Plans", "Name") } }
                    };
                    c.Details.Add(ch.MakeDetail("Amount", Money(upPr.Amount), currentValue: Money(curPr.Amount)));
                    result.Add(c);
                }
            }
        }

        // -- plan/add-on links + their prices --------------------------------------------------------------

        private static void ComparePlanAddOns(BillingPlanMarkup[] cur, BillingPlanMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            var curByPlan = (cur ?? Array.Empty<BillingPlanMarkup>()).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            // Only process plans that survive (present in the upload). Links of a deleted plan cascade with the plan.
            foreach (var upP in up ?? Array.Empty<BillingPlanMarkup>())
            {
                curByPlan.TryGetValue(upP.Name, out var curP);
                var curLinks = curP?.AddOns ?? Array.Empty<BillingPlanAddOnMarkup>();
                var upLinks = upP.AddOns ?? Array.Empty<BillingPlanAddOnMarkup>();

                foreach (var (curL, upL) in JoinBy(curLinks, upLinks, l => l.AddOnName))
                {
                    if (curL != null && upL == null)
                    {
                        // Link delete; its prices cascade (PlanAddOn -> PlanAddOnPrice).
                        result.Add(new Change
                        {
                            ChangeType = ChangeType.Delete, EntityName = "PlanAddOns", Apply = true, DeletePriority = 1,
                            Key = Key(("Plan", upP.Name), ("AddOn", curL.AddOnName)),
                            KeyExpression = new Dictionary<string, string>
                            {
                                { "Plan", ch.MakeLinqQuery("Plans", "Name") },
                                { "AddOn", ch.MakeLinqQuery("AddOns", "Name") }
                            }
                        });
                    }
                    else if (curL == null && upL != null)
                    {
                        var c = new Change { ChangeType = ChangeType.Insert, EntityName = "PlanAddOns", Apply = true };
                        c.Details.Add(ch.MakeDetail("Plan", upP.Name, ch.MakeLinqAssign("Plan", "Plans", "Name")));
                        c.Details.Add(ch.MakeDetail("AddOn", upL.AddOnName, ch.MakeLinqAssign("AddOn", "AddOns", "Name")));
                        result.Add(c);
                        ComparePlanAddOnPrices(upP.Name, upL.AddOnName, Array.Empty<BillingPriceMarkup>(), upL.Prices ?? Array.Empty<BillingPriceMarkup>(), ch, result);
                    }
                    else if (curL != null)
                    {
                        ComparePlanAddOnPrices(upP.Name, upL.AddOnName, curL.Prices ?? Array.Empty<BillingPriceMarkup>(), upL.Prices ?? Array.Empty<BillingPriceMarkup>(), ch, result);
                    }
                }
            }
        }

        private static void ComparePlanAddOnPrices(string planName, string addOnName, BillingPriceMarkup[] cur, BillingPriceMarkup[] up, IConfigChangeContext ch, List<Change> result)
        {
            // The parent PlanAddOn has no single natural key, so it is resolved by (Plan.Name, AddOn.Name): the
            // add-on name is baked into the lookup's additional-where, the plan name travels as the filter value.
            var addOnWhere = $"n.AddOn.Name == \"{addOnName}\"";
            foreach (var (currency, curPr, upPr) in JoinPrices(cur, up))
            {
                if (curPr != null && upPr == null)
                {
                    result.Add(new Change
                    {
                        ChangeType = ChangeType.Delete, EntityName = "PlanAddOnPrices", Apply = true, DeletePriority = 0,
                        Key = Key(("Currency", currency), ("PlanAddOn", planName)),
                        KeyExpression = new Dictionary<string, string> { { "PlanAddOn", ch.MakeLinqQuery("PlanAddOns", "Plan.Name", addOnWhere) } }
                    });
                }
                else if (curPr == null && upPr != null)
                {
                    var c = new Change { ChangeType = ChangeType.Insert, EntityName = "PlanAddOnPrices", Apply = true };
                    c.Details.Add(ch.MakeDetail("Currency", upPr.Currency));
                    c.Details.Add(ch.MakeDetail("Amount", Money(upPr.Amount)));
                    c.Details.Add(ch.MakeDetail("PlanAddOn", planName, ch.MakeLinqAssign("PlanAddOn", "PlanAddOns", "Plan.Name", addOnWhere)));
                    result.Add(c);
                }
                else if (curPr != null && upPr.Amount != curPr.Amount)
                {
                    var c = new Change
                    {
                        ChangeType = ChangeType.Update, EntityName = "PlanAddOnPrices", Apply = true,
                        Key = Key(("Currency", currency), ("PlanAddOn", planName)),
                        KeyExpression = new Dictionary<string, string> { { "PlanAddOn", ch.MakeLinqQuery("PlanAddOns", "Plan.Name", addOnWhere) } }
                    };
                    c.Details.Add(ch.MakeDetail("Amount", Money(upPr.Amount), currentValue: Money(curPr.Amount)));
                    result.Add(c);
                }
            }
        }

        // -- helpers ---------------------------------------------------------------------------------------

        private static BillingPriceMarkup ToPriceMarkup(PlanPrice p) => new() { Currency = p.Currency, Amount = p.Amount };

        private static BillingPriceMarkup ToPriceMarkup(PlanAddOnPrice p) => new() { Currency = p.Currency, Amount = p.Amount };

        private static string Money(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);

        private static bool TextChanged(string up, string cur)
            => (up != cur && !string.IsNullOrWhiteSpace(up) && !string.IsNullOrWhiteSpace(cur))
               || (string.IsNullOrWhiteSpace(up) != string.IsNullOrWhiteSpace(cur));

        private static void AddNullableIntUpdate(Change c, IConfigChangeContext ch, string prop, int? up, int? cur)
        {
            if (up == cur)
            {
                return;
            }

            if (up.HasValue)
            {
                c.Details.Add(ch.MakeDetail(prop, up.Value.ToString(CultureInfo.InvariantCulture), currentValue: cur?.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                c.Details.Add(ch.MakeDetail(prop, null, $"Entity.{prop}=null", cur?.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static Dictionary<string, string> Key(params (string Name, string Value)[] parts)
            => parts.ToDictionary(p => p.Name, p => p.Value);

        private static IEnumerable<(T Cur, T Up)> JoinBy<T>(IEnumerable<T> cur, IEnumerable<T> up, Func<T, string> key) where T : class
        {
            var curBy = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in cur ?? Enumerable.Empty<T>())
            {
                curBy[key(x)] = x;
            }

            var upBy = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in up ?? Enumerable.Empty<T>())
            {
                upBy[key(x)] = x;
            }

            var all = new HashSet<string>(curBy.Keys, StringComparer.OrdinalIgnoreCase);
            all.UnionWith(upBy.Keys);
            foreach (var k in all)
            {
                yield return (curBy.TryGetValue(k, out var cv) ? cv : null, upBy.TryGetValue(k, out var uv) ? uv : null);
            }
        }

        private static IEnumerable<(string Value, bool InCur, bool InUp)> JoinKeys(IEnumerable<string> cur, IEnumerable<string> up)
        {
            var curBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in cur ?? Enumerable.Empty<string>())
            {
                curBy[x] = x;
            }

            var upBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in up ?? Enumerable.Empty<string>())
            {
                upBy[x] = x;
            }

            var all = new HashSet<string>(curBy.Keys, StringComparer.OrdinalIgnoreCase);
            all.UnionWith(upBy.Keys);
            foreach (var k in all)
            {
                yield return (upBy.TryGetValue(k, out var uv) ? uv : curBy[k], curBy.ContainsKey(k), upBy.ContainsKey(k));
            }
        }

        private static IEnumerable<(string Currency, BillingPriceMarkup Cur, BillingPriceMarkup Up)> JoinPrices(BillingPriceMarkup[] cur, BillingPriceMarkup[] up)
            => JoinBy(cur, up, p => p.Currency).Select(t => ((t.Up ?? t.Cur).Currency, t.Cur, t.Up));
    }
}
