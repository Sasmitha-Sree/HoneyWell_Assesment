from collections import defaultdict

def summarize_orders(events):
    # Deduplicate by event_id
    seen = set()
    unique_events = []

    for event in events:
        if event["event_id"] not in seen:
            seen.add(event["event_id"])
            unique_events.append(event)

    # Group by order_id
    orders = defaultdict(list)
    for event in unique_events:
        orders[event["order_id"]].append(event)

    summaries = []

    for order_id in sorted(orders):
        # Process in (timestamp, event_id) order
        order_events = sorted(
            orders[order_id],
            key=lambda e: (e["timestamp"], e["event_id"])
        )

        status = "PENDING"
        items = defaultdict(int)

        for event in order_events:
            etype = event["type"]

            if etype == "CREATED":
                continue

            elif etype == "ITEM_ADDED":
                if status not in ("PAID", "CANCELLED"):
                    items[event["sku"]] += event["qty"]

            elif etype == "ITEM_REMOVED":
                if status not in ("PAID", "CANCELLED"):
                    sku = event["sku"]
                    qty = event["qty"]
                    items[sku] = max(0, items[sku] - qty)
                    if items[sku] == 0:
                        del items[sku]

            elif etype == "PAID":
                if status != "CANCELLED":
                    status = "PAID"

            elif etype == "CANCELLED":
                status = "CANCELLED"

        summaries.append({
            "order_id": order_id,
            "status": status,
            "items": dict(items)
        })

    return summaries