# Campaign recommendations

`GET /api/campaign-recommendations` (Admin) ranks merchant categories that are worth
defining a campaign over, and hands each one back with a draft the operator can open the
campaign form on.

## The signals

For every merchant category with card activity in the lookback window:

| Signal | How it is measured |
|---|---|
| **Spend** | Net card spend over the window (refund rows are negative, so a plain sum nets them), normalised against the busiest category. |
| **Trend** | Spend in the recent half of the window against the half before it. `0.42` means it grew 42%. |
| **Season** | Average `SEASONAL_PATTERN` weight over the months the suggested campaign would run. Above `1.00` is a stronger-than-usual stretch. |
| **Coverage gap** | Whether any open or upcoming campaign already singles out a merchant in the category. A campaign with no merchant criteria is horizontal, not category coverage, so it does not count. |

```
score = SpendWeight  * normalisedSpend
      + TrendWeight   * clamp(trend, -1, 3)
      + SeasonWeight  * (seasonalWeight - 1)
score *= CoverageGapBoost   if nothing covers the category
```

The weights, the window lengths, the minimum spend and the suggested-reward rate all live in
the `Recommendation` section of `appsettings.json` (`RecommendationOptions`). Tuning them is
what "training" means for this heuristic. The scoring is deliberately isolated in
`CampaignRecommendationService` so a trained model can replace it without the controller or
the screen changing.

## SEASONAL_PATTERN

A calendar prior — category × month × weight — seeded in `SeasonalPatternConfiguration`.
`1.00` is an ordinary month; a category or month with no row is treated as `1.00`. The seeded
values follow ordinary Turkish retail seasonality (back-to-school in August–September, fuel
and travel across the summer, electronics in November, apparel at the collection changes,
weddings in late spring) rather than anything measured from this system's own transactions
yet — that is the job of the eventual trained model.

## The draft

Each suggestion carries a `draft`: a campaign name, a start/end date over the horizon, the
active merchants in the category, and a suggested `RewardPoint` sized from the category's
average ticket. The **Kampanya Önerileri** screen passes it to `/campaigns/new` as router
state, where `CampaignForm` prefills those fields and leaves the rest to the operator.
