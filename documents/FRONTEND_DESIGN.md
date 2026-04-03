# FRONTEND DESIGN DOCUMENT
## CrossMarket Price Analyzer — React JavaScript SPA

**Version:** 1.0 | **Date:** April 3, 2026 | **Status:** Draft

---

## Table of Contents
1. [Design Principles](#1-design-principles)
2. [Tech Stack](#2-tech-stack)
3. [Project Structure](#3-project-structure)
4. [Page Specifications](#4-page-specifications)
5. [Component Library](#5-component-library)
6. [State Management](#6-state-management)
7. [API Integration](#7-api-integration)
8. [Routing & Navigation](#8-routing--navigation)
9. [Key UI/UX Decisions](#9-key-uiux-decisions)
10. [Project Checklist](#10-project-checklist)

---

## 1. Design Principles

| Principle | Application |
|---|---|
| **Mobile-first responsive** | Dashboard usable on tablet/desktop; responsive grid adapts |
| **Data density** | Financial dashboard style — maximize data visibility |
| **Progressive disclosure** | Summary → Drill-down → Detail (opportunity feed → comparison → deep stats) |
| **Accessibility (WCAG 2.1 AA)** | Semantic HTML, keyboard nav, ARIA labels, sufficient contrast |
| **Performance** | <3s load on 3G; React Query caching; lazy-loaded routes |
| **Consistency** | Single Tailwind token set; shared component library |

---

## 2. Tech Stack

| Concern | Choice | Rationale |
|---|---|---|
| **Framework** | React 18 (JavaScript) | Component-based; large ecosystem; team familiarity |
| **Build** | Vite 5 | Fast HMR; optimized builds; ESM-native |
| **Language** | JavaScript (ES2022+) | Team preference; no build-time transpilation overhead |
| **Styling** | Tailwind CSS v3 | Utility-first; consistent design system; tree-shakes well |
| **UI Components** | shadcn/ui (React) | Accessible, customizable, copy-paste ownership |
| **State (Server)** | TanStack Query (React Query v5) | Caching, background refetch, optimistic updates |
| **State (Client)** | Zustand | Minimal boilerplate; great for UI state |
| **Routing** | React Router v6 | Standard for React SPA routing |
| **Charts** | Recharts | Composable, React-native, responsive |
| **HTTP** | Axios + Axios interceptors | Auto-refresh tokens; request/response logging |
| **Forms** | React Hook Form + Zod | Performance; schema validation; easy integration |
| **i18n** | react-i18next | Multi-language (EN, VI for v1) |
| **Testing** | Vitest + React Testing Library | Unit tests; fast |
| **E2E** | Playwright | Cross-browser; reliable selectors |

---

## 3. Project Structure

```
cma-webapp/
│
├── public/
│   ├── favicon.svg
│   └── robots.txt
│
├── src/
│   ├── main.jsx                    # App entry point
│   ├── App.jsx                     # Root component + router
│   ├── index.css                   # Tailwind directives + custom tokens
│   │
│   ├── api/                        # API layer (Axios clients)
│   │   ├── axiosClient.js          # Base Axios instance
│   │   ├── productApi.js          # ProductService endpoints
│   │   ├── matchingApi.js         # MatchingService endpoints
│   │   ├── scoringApi.js          # ScoringService endpoints
│   │   └── alertApi.js             # NotificationService endpoints
│   │
│   ├── components/                 # Shared / reusable UI components
│   │   ├── ui/                     # shadcn/ui base components
│   │   │   ├── button.jsx
│   │   │   ├── card.jsx
│   │   │   ├── input.jsx
│   │   │   ├── select.jsx
│   │   │   ├── dialog.jsx
│   │   │   ├── table.jsx
│   │   │   ├── badge.jsx
│   │   │   ├── slider.jsx
│   │   │   ├── tooltip.jsx
│   │   │   ├── skeleton.jsx
│   │   │   └── toast.jsx           # (Radix UI based)
│   │   │
│   │   ├── layout/                 # Layout components
│   │   │   ├── Header.jsx
│   │   │   ├── Sidebar.jsx
│   │   │   ├── Footer.jsx
│   │   │   └── PageContainer.jsx
│   │   │
│   │   ├── shared/                 # Business-shared components
│   │   │   ├── OpportunityCard.jsx
│   │   │   ├── ProductTile.jsx
│   │   │   ├── ScoreGauge.jsx
│   │   │   ├── PriceDisplay.jsx    # Formats USD/VND with currency symbol
│   │   │   ├── ConfidenceBadge.jsx # Low/Medium/High match confidence
│   │   │   ├── StatusBadge.jsx     # Pending/Confirmed/Rejected
│   │   │   ├── MetricCard.jsx      # Key KPI card (margin%, demand, etc.)
│   │   │   ├── QuickLookupForm.jsx # URL input + submit
│   │   │   └── ExportButton.jsx    # CSV/Excel/PDF export trigger
│   │   │
│   │   └── charts/                 # Chart wrappers
│   │       ├── PriceLineChart.jsx  # Historical price trends
│   │       ├── MarginBarChart.jsx  # Margin comparison across products
│   │       └── ScoreRadarChart.jsx  # Multi-factor scoring breakdown
│   │
│   ├── pages/                      # Route-level page components
│   │   ├── DashboardPage.jsx       # Opportunity feed (default route)
│   │   ├── ComparisonPage.jsx      # Side-by-side product comparison
│   │   ├── CategoryPage.jsx        # Category explorer
│   │   ├── PriceHistoryPage.jsx    # Historical price charts
│   │   ├── QuickLookupPage.jsx     # URL-based quick lookup
│   │   ├── AlertsPage.jsx          # Notification center
│   │   └── SettingsPage.jsx       # User preferences & scoring weights
│   │
│   ├── hooks/                      # Custom React hooks
│   │   ├── useOpportunities.js     # React Query hook for opportunities
│   │   ├── useProduct.js           # React Query hook for single product
│   │   ├── usePriceHistory.js      # React Query hook for price history
│   │   ├── useAlerts.js            # React Query hook for alerts
│   │   ├── useScoringWeights.js    # Zustand-backed scoring weight state
│   │   └── useWebSocket.js         # Real-time opportunity updates
│   │
│   ├── store/                      # Zustand global stores
│   │   ├── uiStore.js              # Sidebar open, theme, loading states
│   │   ├── scoringStore.js         # Scoring factor weights (persisted)
│   │   └── filterStore.js          # Active dashboard filters
│   │
│   ├── lib/                        # Utility helpers
│   │   ├── currency.js             # formatUSD(), formatVND(), convertPrice()
│   │   ├── scoring.js              # calculateCompositeScore(), scoreColor()
│   │   ├── fuzzy.js               # highlightMatch() for fuzzy string display
│   │   ├── export.js               # exportToCSV(), exportToExcel()
│   │   └── constants.js           # SOURCE_LABELS, STATUS_COLORS, etc.
│   │
│   ├── i18n/                       # Internationalization
│   │   ├── index.js
│   │   ├── en.json                 # English translations
│   │   └── vi.json                 # Vietnamese translations
│   │
│   └── styles/
│       └── globals.css             # CSS variables, fonts, scrollbar styling
│
├── tests/
│   ├── unit/                       # Vitest unit tests
│   └── e2e/                        # Playwright E2E tests
│
├── tailwind.config.js
├── vite.config.js
├── jsconfig.json                   # Path aliases (@/ → src/)
└── package.json
```

---

## 4. Page Specifications

### 4.1 Dashboard Page (`/` — default)

**Purpose:** Show ranked opportunity feed — the primary view.

**Layout:**
```
┌──────────────────────────────────────────────────────────────┐
│  Header: Logo | Search | Quick Lookup | Notifications | User │
├─────────────┬────────────────────────────────────────────────┤
│  Sidebar    │  Filters Bar: Category | MinMargin | Demand   │
│             │                         | Competition | Source │
│  - Dashboard│────────────────────────────────────────────────│
│  - Compare  │  Stats Row: Total Opportunities | Avg Margin  │
│  - Category │              | Avg Demand | High Opportunities │
│  - History  │────────────────────────────────────────────────│
│  - Alerts   │  Opportunity Feed (infinite scroll / paginated)│
│  - Settings │  ┌──────────────────────────────────────────┐  │
│             │  │ OpportunityCard × N                       │  │
│             │  │ [Product Name] [Score: 87] [Margin: 22%]  │  │
│             │  │ [US $45] → [VN ₫1,450,000] [Diff: +31%]  │  │
│             │  └──────────────────────────────────────────┘  │
│             │  [Load More]                                   │
└─────────────┴────────────────────────────────────────────────┘
```

**Features:**
- Opportunity cards sorted by composite score (default desc)
- Inline quick stats on each card
- Click card → navigates to ComparisonPage
- Sticky filter bar with URL query param sync
- Real-time WebSocket updates (new high-value opportunity → toast notification)
- Auto-refresh via React Query every 60s

---

### 4.2 Product Comparison Page (`/compare/:matchId`)

**Purpose:** Deep-dive on a specific US ↔ Vietnam product pair.

**Layout:**
```
┌──────────────────────────────────────────────────────────────┐
│ Header (same)                                                │
├──────────────────────────────────────────────────────────────┤
│  ← Back to Dashboard                                         │
│  [Product Name]        [Composite Score: 87/100]             │
│                                                              │
│  ┌────────────────────┐  ┌─────────────────────────────────┐ │
│  │ U.S. PRODUCT       │  │ VIETNAM LISTING                │ │
│  │ Source: Amazon     │  │ Source: Shopee                 │ │
│  │ Price: $45.99 USD  │  │ Price: ₫1,450,000 VND           │ │
│  │ Seller: Amazon     │  │ Seller: PerfumeHCM            │ │
│  │ Rating: 4.5★ (2,300)│  │ Seller: 4.8★ (5,000 reviews)  │ │
│  │ Unit: 1 item       │  │ Unit: 1 item                   │ │
│  │ [View Original →]  │  │ [View Listing →]              │ │
│  └────────────────────┘  └─────────────────────────────────┘ │
│                                                              │
│  LANDED COST BREAKDOWN (collapsible)                        │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ US Purchase Price     $45.99    │ ₫1,180,000           ││
│  │ Shipping              $12.00    │ ₫308,000             ││
│  │ Import Duty (5%)       $2.30    │ ₫59,000              ││
│  │ VAT (10%)              $5.03   │ ₫129,000             ││
│  │ Handling               $3.00    │ ₫77,000              ││
│  │ ──────────────────────────────────────────────────────  ││
│  │ TOTAL LANDED COST     $68.32    │ ₫1,753,000           ││
│  └─────────────────────────────────────────────────────────┘│
│                                                              │
│  PROFITABILITY                                               │
│  Vietnam Retail: ₫1,450,000  │  Landed: ₫1,753,000          │
│  Price Difference: -₫303,000 ❌ (Below break-even — review)│
│  Profit Margin: —             │  ROI: —                       │
│                                                              │
│  SCORING BREAKDOWN                                          │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ Factor              Score   Weight   Weighted           ││
│  │ Profit Margin        0%      40%      0.0                ││
│  │ Market Demand       85      25%     21.3                ││
│  │ Competition         40      20%      8.0                ││
│  │ Price Stability     72      10%      7.2                ││
│  │ Match Confidence    100       5%      5.0                ││
│  │ ──────────────────────────────────────────────────────  ││
│  │ Composite Score: 41.5/100                              ││
│  └─────────────────────────────────────────────────────────┘│
│                                                              │
│  [Confirm Match ✓]  [Reject Match ✗]  [Export]               │
└──────────────────────────────────────────────────────────────┘
```

---

### 4.3 Category Explorer Page (`/categories`)

**Purpose:** Browse opportunities by HS code category.

**Layout:**
- Grid of category cards (Cosmetics, Food & Beverage, Electronics, etc.)
- Each card: category name, total opportunities, avg margin, top product
- Click → filtered opportunity list

---

### 4.4 Price History Page (`/history/:productId`)

**Purpose:** Line chart showing price trends over time.

**Features:**
- Recharts `<LineChart>` with USD and VND axes (dual Y-axis)
- Date range picker (7d / 30d / 90d / custom)
- Source filter (show Amazon only, Shopee only, or all)
- Data table below chart with raw snapshot values
- Export chart data as CSV

---

### 4.5 Quick Lookup Page (`/quick-lookup`)

**Purpose:** Paste a U.S. product URL → instant analysis.

**Flow:**
1. User pastes URL (Amazon, Walmart, cigarpage.com, etc.)
2. Frontend calls `POST /api/products/quick-lookup`
3. Backend scrapes the URL, finds matching Vietnam products
4. Returns `QuickLookupResultDto`
5. Frontend renders:
   - Scraped product details card
   - Estimated landed cost
   - Top 3 matching Vietnam listings with margin estimates
   - "Add to watchlist" button

---

### 4.6 Alerts Page (`/alerts`)

**Purpose:** Notification center.

**Features:**
- List of alerts with type icon, title, message, timestamp
- Filter: All / Unread / By type
- Mark as read / Delete
- Subscribe to product / category alerts

---

### 4.7 Settings Page (`/settings`)

**Purpose:** User preferences and scoring weight customization.

**Sections:**
1. **Scoring Weights:** Sliders for each factor (Profit Margin 0–100%, Market Demand 0–100%, etc.) — values saved to Zustand + backend
2. **Notifications:** Toggle email / Telegram / InApp alerts
3. **Filters:** Default filter presets (save your favorite filter combinations)
4. **Data:** Refresh rate, language (EN/VI)

---

## 5. Component Library

### Core UI Components (shadcn/ui)
All from shadcn/ui with Tailwind CSS styling:

| Component | Used In |
|---|---|
| `Button` | All CTAs, forms, navigation |
| `Card` | Opportunity cards, comparison panels |
| `Input` | Search, URL lookup |
| `Select` | Category filter, source filter |
| `Dialog` | Confirmation modals, detail popups |
| `Table` | Price history, match lists |
| `Badge` | Score badges, status chips |
| `Slider` | Margin filter, scoring weight sliders |
| `Tooltip` | Chart labels, info icons |
| `Skeleton` | Loading states |
| `Toast` | Success/error notifications |
| `Tabs` | Price history date range, category sections |

### Business Components

| Component | Description |
|---|---|
| `OpportunityCard` | Primary card in the feed; shows product name, scores, margin, prices |
| `ProductTile` | Compact product display (name, brand, source, price) |
| `ScoreGauge` | Circular gauge showing composite score 0–100 |
| `PriceDisplay` | Formatted currency display (USD $X.XX / VND ₫X,XXX,XXX) |
| `ConfidenceBadge` | Chip: 🟢 High / 🟡 Medium / 🔴 Low |
| `StatusBadge` | Chip: ⏳ Pending / ✅ Confirmed / ❌ Rejected |
| `MetricCard` | Small KPI card with label, value, and trend arrow |
| `QuickLookupForm` | URL input with loading state and error handling |
| `ExportButton` | Dropdown: CSV / Excel / PDF |
| `PriceLineChart` | Recharts wrapper for price history |
| `LandedCostTable` | Breakdown table for cost components |
| `ScoringBreakdownTable` | Factor-by-factor scoring table |
| `FilterBar` | Horizontal filter bar with chips and sliders |

---

## 6. State Management

### React Query (Server State)
```
useOpportunities(filters)    → GET /api/opportunities
useProduct(id)               → GET /api/products/{id}
usePriceHistory(productId)  → GET /api/products/{id}/prices
useMatches(filters)          → GET /api/matches
useScoreBreakdown(matchId)   → GET /api/scores/{matchId}
useAlerts()                  → GET /api/alerts
useExchangeRate()            → GET /api/exchange-rates/current

Mutations:
useConfirmMatch()            → POST /api/matches/{id}/confirm
useRejectMatch()             → POST /api/matches/{id}/reject
useCreateSubscription()       → POST /api/subscriptions
useQuickLookup()             → POST /api/products/quick-lookup
```

### Zustand (Client State)
```
uiStore:
  - sidebarOpen: boolean
  - theme: 'light' | 'dark'
  - isLoading: boolean
  - toasts: Toast[]

scoringStore (persisted):
  - weights: { profitMargin: 40, demand: 25, competition: 20, stability: 10, confidence: 5 }
  - setWeight(key, value)

filterStore (persisted to URL params):
  - category: string | null
  - minMargin: number
  - demandLevel: 'low' | 'medium' | 'high' | null
  - competitionLevel: 'low' | 'medium' | 'high' | null
  - source: string | null
  - dateRange: { from, to }
```

---

## 7. API Integration

### Axios Setup
```javascript
// src/api/axiosClient.js
const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' }
});

// Request interceptor: attach JWT
api.interceptors.request.use(config => {
  const token = localStorage.getItem('cma_access_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Response interceptor: handle 401 → refresh token
api.interceptors.response.use(
  response => response,
  async error => {
    if (error.response?.status === 401) {
      // Trigger token refresh or redirect to login
    }
    return Promise.reject(error);
  }
);
```

### React Query Configuration
```javascript
// src/lib/queryClient.js
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,         // 60s — don't refetch within 1 min
      gcTime: 5 * 60_000,        // 5 min cache
      retry: 2,
      refetchOnWindowFocus: true,
    }
  }
});
```

---

## 8. Routing & Navigation

```jsx
// App.jsx
<Routes>
  <Route path="/"             element={<DashboardPage />} />
  <Route path="/compare/:matchId" element={<ComparisonPage />} />
  <Route path="/categories"   element={<CategoryPage />} />
  <Route path="/categories/:categoryId" element={<CategoryPage />} />
  <Route path="/history/:productId" element={<PriceHistoryPage />} />
  <Route path="/quick-lookup" element={<QuickLookupPage />} />
  <Route path="/alerts"       element={<AlertsPage />} />
  <Route path="/settings"     element={<SettingsPage />} />
</Routes>
```

- Active nav item highlighted in sidebar
- URL query params synced with filterStore (shareable links)
- Lazy loading: `const DashboardPage = lazy(() => import('./pages/DashboardPage'))`

---

## 9. Key UI/UX Decisions

### Color System
```
--primary:     #2563EB  (blue — CTAs, active states)
--success:     #16A34A  (green — positive margin, confirmed)
--warning:     #D97706  (amber — medium confidence)
--danger:      #DC2626  (red — negative margin, rejected, high competition)
--background:  #F9FAFB  (light gray page background)
--surface:     #FFFFFF  (card surfaces)
--text-primary:#111827
--text-muted:  #6B7280
```

### Scoring Color Scale
```
Score 0–30  → Red    (#DC2626) — Low opportunity
Score 31–60 → Amber  (#D97706) — Medium opportunity
Score 61–80 → Blue   (#2563EB) — Good opportunity
Score 81–100→ Green  (#16A34A) — Excellent opportunity
```

### Responsive Breakpoints
```
sm:  640px   — Mobile landscape
md:  768px   — Tablet portrait
lg: 1024px   — Tablet landscape / small desktop
xl: 1280px   — Standard desktop
2xl:1536px   — Large desktop
```

---

## 10. Project Checklist

### Foundation
- [ ] Initialize Vite + React 18 project
- [ ] Configure Tailwind CSS with design tokens
- [ ] Install shadcn/ui and scaffold base components (Button, Card, Input, etc.)
- [ ] Set up path aliases (@/ → src/) in jsconfig.json + vite.config.js
- [ ] Configure i18next with en.json and vi.json
- [ ] Set up Zustand stores (uiStore, scoringStore, filterStore)
- [ ] Configure Axios client with interceptors
- [ ] Set up React Query client with defaults
- [ ] Add custom ESLint + Prettier config
- [ ] Configure Vitest + React Testing Library

### API Integration
- [ ] Implement productApi.js (all ProductService endpoints)
- [ ] Implement matchingApi.js (all MatchingService endpoints)
- [ ] Implement scoringApi.js (all ScoringService endpoints)
- [ ] Implement alertApi.js (all NotificationService endpoints)
- [ ] Create custom hooks for all API calls (useOpportunities, useProduct, etc.)
- [ ] Implement WebSocket hook for real-time opportunity updates
- [ ] Add React Query devtools in development

### Layout & Navigation
- [ ] Build Header component with logo, search, notifications bell, user menu
- [ ] Build Sidebar with nav links and active state
- [ ] Build PageContainer (wraps content with consistent padding)
- [ ] Implement React Router with lazy-loaded routes
- [ ] Sync filter state with URL query params

### Shared Components
- [ ] Build OpportunityCard with all states (loading, empty, error, data)
- [ ] Build PriceDisplay with USD/VND formatting and currency symbol
- [ ] Build ScoreGauge circular progress component
- [ ] Build ConfidenceBadge and StatusBadge chips
- [ ] Build MetricCard for KPI display
- [ ] Build FilterBar with all dashboard filters
- [ ] Build ExportButton (CSV / Excel / PDF)
- [ ] Build QuickLookupForm with loading and error states
- [ ] Build PriceLineChart with Recharts
- [ ] Build LandedCostTable component
- [ ] Build ScoringBreakdownTable component
- [ ] Implement Toast notification system

### Pages
- [ ] Build DashboardPage with opportunity feed (React Query + infinite scroll)
- [ ] Build ComparisonPage with all panels (US product, VN product, cost breakdown, scoring)
- [ ] Build CategoryPage with category grid and filtered feed
- [ ] Build PriceHistoryPage with line chart and date picker
- [ ] Build QuickLookupPage with URL input and result display
- [ ] Build AlertsPage with notification list and filters
- [ ] Build SettingsPage with scoring weight sliders and notification toggles

### Polish & Performance
- [ ] Add Skeleton loading states to all pages
- [ ] Add ErrorBoundary component with fallback UI
- [ ] Implement empty state illustrations/messages for all lists
- [ ] Add responsive layout tests (mobile, tablet, desktop)
- [ ] Performance: lazy-load all page components
- [ ] Performance: optimize OpportunityCard renders (React.memo)
- [ ] Accessibility: audit with axe-core; fix all WCAG 2.1 AA violations
- [ ] Internationalization: translate all static strings (EN + VI)

### Testing
- [ ] Unit tests for currency formatting helpers
- [ ] Unit tests for scoring calculation logic
- [ ] Unit tests for Zustand store actions
- [ ] Component tests for OpportunityCard (RTL)
- [ ] E2E tests: Dashboard loads and shows opportunity feed
- [ ] E2E tests: Quick Lookup URL submission flow
- [ ] E2E tests: Filter + navigation flow
