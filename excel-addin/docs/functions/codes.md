# Financial convention codes

Worksheet functions use stable integer IDs rather than implementation enum ordinals. An ID never changes meaning after release; new values are appended.

## Calendars

| ID | Name | Meaning |
|---:|---|---|
| 0 | Brazil Settlement | Brazil settlement calendar with code-maintained holiday corrections. |
| 1 | US Settlement | United States settlement calendar. |
| 2 | Brazil + US | Joint calendar; both settlement markets must be open. |

## Business-day conventions

| ID | Name | Meaning |
|---:|---|---|
| 0 | Modified Following | Move forward unless that changes month, then backward. |
| 1 | Following | Move to the next business day. |
| 2 | Preceding | Move to the previous business day. |
| 3 | Modified Preceding | Move backward unless that changes month, then forward. |
| 4 | Unadjusted | Do not adjust the date. |
| 5 | Half-Month Modified Following | QuantLib half-month modified-following adjustment. |
| 6 | Nearest | Move to the nearest business day. |

## Time units

| ID | Name |
|---:|---|
| 0 | Months |
| 1 | Years |
| 2 | Weeks |
| 3 | Days |

## Day counters

| ID | Name |
|---:|---|
| 0 | Business/252 |
| 1 | Actual/365 Fixed |
| 2 | 30/360 Bond Basis |
| 3 | Actual/360 |
| 4 | Actual/365 No Leap |
| 5 | Actual/Actual ISDA |
| 6 | Actual/Actual AFB |
| 7 | 30/360 USA |
| 8 | 30/360 European |
| 9 | 30/360 Italian |
| 10 | 30/360 NASD |
| 11 | One Day |
| 12 | Simple |

The discovery worksheet functions introduced later in this plan expose these tables inside Excel.
