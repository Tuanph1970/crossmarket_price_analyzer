# CrossMarket Price Analyzer — User Guide

**Version:** 1.0 (MVP) | **Last Updated:** April 2026

---

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Dashboard](#2-dashboard)
3. [Product Comparison](#3-product-comparison)
4. [Price History](#4-price-history)
5. [Quick Lookup](#5-quick-lookup)
6. [Alerts](#6-alerts)
7. [Settings](#7-settings)
8. [Glossary](#8-glossary)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Getting Started

### How to Access the Dashboard

Open your browser and navigate to the gateway URL. The default local address is:

```
http://localhost:8080
```

If you are accessing a deployed environment, use the address provided by your system administrator.

> **Login:** v1.0 does not require authentication. All users share the same view. User accounts and individual watchlists are planned for v2.0.

### Overview of What the Tool Shows

The CrossMarket Price Analyzer helps you find profitable cross-border trade opportunities — specifically, U.S. products that can be bought, shipped to Vietnam, and sold at a higher price than the local market.

On the main **Dashboard**, you will see a ranked list of trade opportunities. Each opportunity pairs a U.S. product with a matching Vietnamese listing and scores it based on profit potential.

The higher the score, the stronger the opportunity.

### Key Concepts

Before you begin, understand these three core concepts:

#### Cross-Border Opportunity

A **cross-border opportunity** exists when the same (or very similar) product is priced significantly higher in Vietnam than the total cost of buying and shipping it from the United States. The tool identifies these gaps automatically.

#### Landed Cost

**Landed Cost** is the total cost of bringing a U.S. product into Vietnam. It includes:

- The purchase price of the item in the U.S.
- International shipping
- Import duty (a percentage charged by Vietnamese customs, based on product category)
- VAT (Value Added Tax, 10% in Vietnam)
- Handling and miscellaneous fees

The formula:

```
Landed Cost = US Purchase Price + Shipping + Import Duty + VAT + Handling
```

The tool calculates this automatically. If the Vietnam retail price is higher than the landed cost, there is potential profit.

#### Composite Score

The **Composite Score** (0–100) is a single number that summarizes how good an opportunity is. It combines five factors:

| Factor | Default Weight | What It Measures |
|---|---|---|
| Profit Margin | 40% | How much profit is possible relative to the Vietnam retail price |
| Market Demand | 25% | How popular the product appears to be in Vietnam (sales volume, reviews) |
| Competition | 20% | How many sellers are already offering this product in Vietnam |
| Price Stability | 10% | How steady the U.S. and Vietnam prices have been over the past 30 days |
| Match Confidence | 5% | How confident the system is that the U.S. and Vietnam products are truly the same |

You can adjust these weights in **Settings** to match your own priorities.

---

## 2. Dashboard

The Dashboard is the main page of the application. It opens automatically when you visit the site.

### What You See on Load

When the Dashboard loads, it fetches the most recent opportunities from the server. You will see:

- A **summary statistics bar** at the top showing total opportunities, average margin, and the number of high-scoring items.
- An **opportunity feed** — a scrollable list of opportunity cards sorted by Composite Score (highest first) by default.
- A **filter bar** below the header for narrowing the results.

If no data has loaded yet, you will see a loading animation. If no opportunities match your current filters, you will see an empty state message.

### Understanding the Opportunity Card

Each card in the feed represents one U.S.–Vietnam product pairing. Here is what each part means:

```
┌──────────────────────────────────────────────────────────┐
│  [Perfume Gift Set — Eau de Parfum 100ml]               │
│                                                          │
│  US → VN  $45.99 USD → ₫1,450,000 VND                  │
│                                                          │
│  Score: 87  ████████████████████░░░░░░  [Excellent]     │
│  Margin: +22%                                           │
│                                                          │
│  Source: Amazon ✦ Shopee                                 │
└──────────────────────────────────────────────────────────┘
```

| Field | Description |
|---|---|
| **Product Name** | The matched product name. Click the card to open the full comparison. |
| **US → VN Prices** | Current U.S. retail price (USD) and the equivalent Vietnam retail price (VND). |
| **Composite Score Badge** | Color-coded score from 0–100. Green = Excellent (81–100), Blue = Good (61–80), Amber = Medium (31–60), Red = Low (0–30). |
| **Margin %** | Profit margin percentage. A positive number (e.g., +22%) means Vietnam price exceeds landed cost. |
| **Source** | Which platforms the products were scraped from (e.g., Amazon, Walmart, Shopee). |

### Filtering

Use the **Filter Bar** above the opportunity feed to narrow results:

| Filter | How to Use |
|---|---|
| **Source** | Filter by U.S. source marketplace: Amazon, Walmart, or cigarpage.com. |
| **Category** | Select a product category from the dropdown (e.g., Cosmetics, Electronics, Food & Beverage). |
| **Minimum Margin** | Slide to show only opportunities with margin at or above your threshold (e.g., show only 15%+ margin). |
| **Demand Level** | Filter by estimated demand in Vietnam: Low, Medium, or High. |
| **Competition Level** | Filter by how many sellers are active in Vietnam: Low, Medium, or High. |

> **Note on performance:** Applying many filters at once may result in fewer or zero results, especially if data coverage for a category is limited. Try relaxing your filters.

### Pagination and Navigation

The opportunity feed is paginated to keep page load times fast. At the bottom of the feed you will find:

- **[Load More]** button — appends the next page of results to the current list.
- **Result count** — shows how many opportunities are currently displayed out of the total available.

### Exporting to CSV

To export the currently filtered list of opportunities to a CSV file:

1. Click the **Export** button (located above or within the filter bar).
2. Choose **CSV** from the dropdown.
3. The browser will download a file named `opportunities-export.csv`.

The exported file contains: Product Name, US Price (USD), VN Price (VND), Composite Score, Margin %, Demand Score, Competition Score, US Source, VN Source, and Calculated At.

> Excel export is available in v1.5. PDF export is planned for v2.0.

---

## 3. Product Comparison

The Product Comparison page gives you a detailed, side-by-side view of a single U.S.–Vietnam product match.

### How to Open a Comparison

Click on **any opportunity card** on the Dashboard. The page will navigate to the Comparison view for that specific match.

You can also navigate directly using a comparison link shared by a colleague (URL format: `/compare/:matchId`).

### US Product Panel

The left panel shows details of the U.S. product:

| Field | Description |
|---|---|
| **Source** | Which U.S. marketplace this was scraped from (Amazon, Walmart, etc.). |
| **Price** | Current price in USD. |
| **Seller** | Name of the seller or retailer. |
| **Seller Rating** | Average customer rating out of 5 stars, plus the number of reviews. |
| **Unit** | Quantity per unit (e.g., "1 item", "Box of 20"). |
| **View Original** | A link to open the original product listing in a new tab. |

### Vietnam Product Panel

The right panel shows details of the matched Vietnam listing:

| Field | Description |
|---|---|
| **Source** | Which Vietnam marketplace (Shopee in v1.0; Lazada and Tiki in v1.5). |
| **Price** | Current price in VND. |
| **Seller** | Seller/shop name on the Vietnam platform. |
| **Seller Rating** | Average rating out of 5 stars, plus total reviews. |
| **Unit** | Quantity per unit. |
| **View Listing** | A link to open the original Vietnam listing. |

### Cost Breakdown Tab

Click the **Cost Breakdown** tab to see how the total landed cost was calculated:

| Cost Component | Description |
|---|---|
| **US Purchase Price** | The price you pay to buy the product in the U.S. |
| **Shipping** | Estimated international shipping cost (based on weight tier; FedEx/DHL API integration in v1.5). |
| **Import Duty** | Customs duty charged by Vietnam, calculated as a percentage of the product value based on its HS code category. |
| **VAT** | Vietnam's Value Added Tax (10% of the CIF value). |
| **Handling** | Customs brokerage, local delivery, and miscellaneous handling fees. |
| **Total Landed Cost** | **The sum of all the above.** This is your true cost to get one unit into Vietnam. |

All cost components are shown in both **USD** and **VND** side by side, using the current exchange rate.

> **Accuracy Note:** In v1.0, shipping and handling costs are estimates based on weight tier tables. FedEx/DHL API integration in v1.5 will provide real-time freight quotes.

### Score Breakdown Tab

Click the **Score Breakdown** tab to see how the Composite Score was calculated:

| Factor | Raw Score | Your Weight | Weighted Score |
|---|---|---|---|
| Profit Margin | (varies) | 40% | (calculated) |
| Market Demand | 0–100 | 25% | (calculated) |
| Competition | 0–100 | 20% | (calculated) |
| Price Stability | 0–100 | 10% | (calculated) |
| Match Confidence | 0–100 | 5% | (calculated) |
| **Composite Score** | | | **0–100** |

To change how these factors are weighted, go to **Settings**.

### Confirming or Rejecting a Match

If you believe the U.S. and Vietnam products are correctly matched, click **Confirm Match** (green checkmark).

If you believe they are incorrectly matched (different products, wrong variant, etc.), click **Reject Match** (red X).

Confirmed and rejected matches are excluded from the default Dashboard feed (which shows only Pending matches by default, unless you apply a status filter).

---

## 4. Price History

The Price History page shows how the prices of a product have changed over time.

### Accessing Price History

From the Dashboard, navigate to any product. In the product detail or comparison view, look for a **History** tab or link. You can also navigate directly via URL: `/history/:productId`.

### Understanding the Chart

The main element is a **line chart** with two trend lines:

- **Blue line** — U.S. price (shown in USD on the left Y-axis).
- **Orange line** — Vietnam price (shown in VND on the right Y-axis).

> Because the two currencies have very different scales, the chart uses a **dual Y-axis**. The lines are plotted independently — do not use the vertical distance between lines to judge price gaps; use the Cost Breakdown tab for that.

### Filtering by Date Range

Use the date range selector above the chart to filter the time window:

| Preset | Description |
|---|---|
| **7 Days** | Last 7 days of data. |
| **30 Days** | Last 30 days (default). |
| **90 Days** | Last 90 days. |
| **Custom** | Pick a specific start and end date. |

### Reading the Trend Lines

- **Upward slope** — price is increasing over time.
- **Downward slope** — price is decreasing.
- **Flat line** — price has remained stable.
- **Gaps or missing sections** — no data was scraped for those dates (common on weekends or holidays when scrapers did not run).

A **converging** U.S. and Vietnam line means the arbitrage opportunity is shrinking. A **diverging** line (Vietnam rising, U.S. falling) means the opportunity is growing.

### Exporting Price History

Click the **Export** button above the chart to download the raw price data as a CSV file. The file includes: Date, US Price (USD), VN Price (VND), Exchange Rate, and Source.

---

## 5. Quick Lookup

Quick Lookup lets you analyze any U.S. product by its URL — without waiting for it to appear in the scheduled scraping results.

### Entering a Product URL

1. Click **Quick Lookup** in the navigation sidebar.
2. Paste a product URL into the input field. Supported sources in v1.0:
   - `amazon.com`
   - `walmart.com`
   - `cigarpage.com`
3. Click **Analyze**.

### How It Works

Behind the scenes, the system:

1. **Scrapes** the URL to extract the product name, brand, price, and unit quantity.
2. **Searches** for matching Vietnam products in the database.
3. **Calculates** the landed cost and estimates the margin.
4. **Returns** the results to your screen within seconds.

> The first time you look up a URL, it may take up to 30 seconds while the scraper runs. Subsequent lookups of the same product will be faster (cached results).

### Interpreting Quick Lookup Results

The results page shows:

- **Product Details Card** — name, brand, price, source URL.
- **Matched Vietnam Listings** — up to 3 matching products from Shopee, with their prices in VND.
- **Estimated Landed Cost** — total cost to bring this product to Vietnam.
- **Estimated Margin** — profit margin range based on the Vietnam matches found.

If no Vietnam matches are found, the tool will still show the U.S. product and landed cost estimate, but the margin will show as "Insufficient Vietnam data — no matches found." This is normal for niche or very new products.

---

## 6. Alerts

> **Note:** Full alert functionality (email, Telegram, threshold-based notifications) is planned for v2.0. The Alerts page in v1.0 shows a list of system-generated notifications and supports basic read/unread state management.

### Viewing the Alerts List

Click **Alerts** in the navigation sidebar. You will see a chronological list of notifications.

Each alert shows:
- **Type icon** — distinguishes between New Opportunity, Price Drop, Match Confirmation, and Threshold alerts.
- **Title** — a brief description of the event.
- **Message** — more detail about what happened.
- **Timestamp** — when the alert was generated.
- **Read/Unread indicator** — bold title means unread.

### Filtering Alerts

Use the filter buttons above the list to show:
- **All** — every alert.
- **Unread** — only alerts you have not yet opened.
- **By Type** — filter to a specific alert category.

### Marking as Read / Unread

- Click on any alert to open it and automatically mark it as **read**.
- To mark an alert as unread again (e.g., to revisit it later), hover over the alert row and click the **Mark Unread** icon.

### Subscribing to Alerts

Alert subscriptions (to receive notifications when a specific product or category meets your criteria) are available in v2.0.

---

## 7. Settings

The Settings page lets you customize how opportunities are scored and how the interface behaves.

### Adjusting Scoring Weights

Scroll to the **Scoring Weights** section. You will see a slider for each of the five scoring factors:

| Factor | What It Measures | Default Weight |
|---|---|---|
| Profit Margin | Size of the profit margin | 40% |
| Market Demand | How popular the product is in Vietnam | 25% |
| Competition | How few sellers are already in Vietnam | 20% |
| Price Stability | How steady prices have been | 10% |
| Match Confidence | How confident the system is in the product match | 5% |

**To adjust a weight:**
1. Move the slider left (lower weight) or right (higher weight).
2. The percentage shown updates in real time.
3. Click **Save** to apply your changes.

> **Important:** The five weights must sum to 100%. The system will enforce this. If you set one weight very high, the others may auto-adjust downward to maintain the total.

### What Each Factor Means

| Factor | Higher Score = | Lower Score = |
|---|---|---|
| **Profit Margin** | Larger margin relative to Vietnam retail price | Narrower or negative margin |
| **Market Demand** | More sales volume and reviews on Vietnam platforms | Less demand signal available |
| **Competition** | Fewer active sellers for this product in Vietnam | Many sellers already offering it |
| **Price Stability** | Prices have not fluctuated much in 30 days | Prices are volatile or erratic |
| **Match Confidence** | U.S. and Vietnam products are very likely the same item | Match is uncertain (different variants, brands, etc.) |

### Strategy Examples

| Strategy | Recommended Weights |
|---|---|
| **Conservative (safe margins)** | Profit Margin: 60%, Demand: 20%, Competition: 10%, Stability: 5%, Confidence: 5% |
| **Growth (find demand)** | Profit Margin: 25%, Demand: 40%, Competition: 15%, Stability: 10%, Confidence: 10% |
| **Balanced** | Profit Margin: 40%, Demand: 25%, Competition: 20%, Stability: 10%, Confidence: 5% (default) |

### Saving and Resetting

- **Save** — persists your weights to the server. You will see a success toast notification.
- **Reset to Defaults** — restores all five weights to the system defaults (40/25/20/10/5).

---

## 8. Glossary

| Term | Definition |
|---|---|
| **Composite Score** | A single number from 0 to 100 that summarizes an opportunity's overall attractiveness, calculated by combining five weighted factors. Higher is better. |
| **Confidence Score** | A measure (Low / Medium / High) of how confident the system is that the U.S. and Vietnam products are truly the same item. Based on fuzzy string matching similarity. |
| **Cross-Border Opportunity** | A trade scenario where a product can be purchased in one country (U.S.) and sold in another (Vietnam) at a profit after accounting for all landed costs. |
| **HS Code** | Harmonized System Code — an international classification number used by customs authorities to determine duty rates and regulatory requirements for traded products. |
| **Landed Cost** | The total cost of getting one unit of a product from the U.S. seller to your warehouse in Vietnam, including purchase price, shipping, import duty, VAT, and handling fees. |
| **Profit Margin (%)** | `(Vietnam Retail Price − Landed Cost) ÷ Vietnam Retail Price × 100`. A positive margin means the product can be sold at a profit. |
| **ROI (%)** | `(Vietnam Retail Price − Landed Cost) ÷ Landed Cost × 100`. Return on Investment — how much profit you earn relative to your upfront investment. |
| **Price Stability Score** | A 0–100 score reflecting how much the U.S. and Vietnam prices have fluctuated over the trailing 30 days. Higher = more stable = less purchasing risk. |
| **CIF Value** | Cost, Insurance, and Freight — the product price plus shipping and insurance to the destination port. Used as the taxable base for import duty and VAT calculations in Vietnam. |
| **VAT** | Value Added Tax. In Vietnam, the standard rate is 10% applied to the CIF value of imported goods. |
| **Scraping** | The automated process of visiting a website and extracting product data (price, name, seller, etc.) using specialized software. |
| **Quick Lookup** | A feature that lets you paste any U.S. product URL and immediately receive an analysis without waiting for the scheduled scraping cycle. |
| **VND** | Vietnamese Dong — the official currency of Vietnam. All Vietnam prices are displayed in VND. The tool converts USD prices to VND using a live exchange rate. |
| **USD** | United States Dollar — the currency of the U.S. source products. |

---

## 9. Troubleshooting

### Scraping Failures: Product URL Not Working

**Symptoms:** Quick Lookup returns an error or "Scraping failed" for a URL that is valid and accessible.

**Common causes and solutions:**

| Cause | Solution |
|---|---|
| The product page requires JavaScript to load (common on Amazon and Walmart). | The system uses headless browsers to handle JavaScript pages. If it still fails, the page structure may have changed — report this to the team. |
| The product has been removed or is out of stock. | Try a different URL or check the product page manually in your browser. |
| IP blocking — the scraper was temporarily blocked by the target site. | Wait 15–30 minutes and try again. |
| The URL format is not supported in v1.0. | v1.0 supports: `amazon.com`, `walmart.com`, `cigarpage.com`. Other sources are planned for v1.5. |

**How to check:** Try opening the URL in your own browser. If you can see the product but the tool cannot, please report the URL to the development team with the exact error message.

### Exchange Rate Issues

**Symptoms:** Prices appear unusually high or low in VND, or a margin calculation seems off.

**Explanation:** The tool fetches the USD → VND exchange rate from a live forex API once per hour. The rate is cached in Redis. A small delay between a rate change and its reflection in the tool is expected.

**How to check:**
1. Go to the Comparison page for any opportunity.
2. Look for the exchange rate shown in the Cost Breakdown section, along with the timestamp ("Rate fetched at: [date/time]").
3. If the rate seems stale (more than 2 hours old), the exchange rate service may be temporarily unavailable. The system will use a **fallback rate of 25,000 VND per USD** until the live rate is restored.

**What to do:** If you believe the exchange rate is incorrect for a specific calculation, use the **Settings → Manual Cost Override** (planned for v1.5) to enter a custom rate.

### Data Freshness: How Often Prices Update

Prices are not real-time. Here is the expected data freshness schedule:

| Data Type | Update Frequency | Notes |
|---|---|---|
| U.S. Product Prices (Amazon, Walmart) | Daily | Once per day, overnight. |
| Vietnam Product Prices (Shopee) | Daily | Once per day. |
| Exchange Rate | Hourly | Cached for 1 hour. |
| Landed Cost | Every 6 hours | Recalculated automatically for all active matches. |
| Composite Scores | Every 6 hours | Updated whenever Landed Cost recalculates. |
| Quick Lookup | On-demand | Scrapes the URL at the moment you submit it. |

> **Weekend note:** Scrapers do not run on Saturdays and Sundays. Prices from Friday are shown through the weekend.

### No Opportunities Appearing in Results

**Symptoms:** The Dashboard shows zero opportunities after applying filters.

**Troubleshooting steps:**
1. Check whether your filters are too restrictive. Try resetting all filters to their defaults.
2. Confirm that the scrapers have run successfully. Check the **Scrape Jobs** status page (if available in your environment) to see if recent jobs completed.
3. If you are using the **Minimum Margin** filter, try lowering the threshold. Very few products will have margins above 30%.
4. If no products appear at all (not just filtered), there may be a data loading issue — refresh the page or contact your system administrator.

### CSV Export Contains Unexpected Data

**Symptoms:** The exported CSV has fewer rows than expected, or missing columns.

**Solutions:**
- The CSV only exports the **currently filtered and loaded** results — not all products in the database. Click **Load More** until all desired results are loaded before exporting.
- Very new products (just scraped today) may not yet have full scoring data. Their rows may show blank fields for some columns.

### Interface Language (i18n)

**To switch the interface language:**
1. Click your user initials or avatar in the **Header**.
2. Select **Language**.
3. Choose **English** or **Tiếng Việt**.

All static UI labels, buttons, and messages will update immediately. Product data (product names, prices, seller names) is not translated — it reflects the original marketplace language.
