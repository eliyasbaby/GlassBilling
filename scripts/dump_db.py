import sqlite3
import os

paths = [
    r"C:\Users\dyocense\AppData\Local\GlassBilling\GlassBilling\Data\glassbilling.db3",
    r"C:\Users\dyocense\AppData\Local\Packages\com.glassbilling.app_b5y0bwpcra2b8\LocalState\glassbilling.db3",
    r"C:\Users\dyocense\AppData\Local\User Name\com.glassbilling.app\Data\glassbilling.db3"
]

bill_id = 49
item_id = 57

for db in paths:
    if not os.path.exists(db):
        print(f"DB not found: {db}")
        continue

    conn = sqlite3.connect(db)
    cur = conn.cursor()
    print(f"\nDB: {db}")

    try:
        bills = cur.execute("SELECT COUNT(*) FROM BillItems WHERE BillId=?", (bill_id,)).fetchone()[0]
    except Exception as e:
        print(f"  Error querying BillItems: {e}")
        conn.close()
        continue

    print(f"  BillItems for BillId={bill_id}: {bills}")

    rows = cur.execute("SELECT COUNT(*) FROM MeasurementRows WHERE BillItemId=?", (item_id,)).fetchone()[0]
    print(f"  MeasurementRows for BillItemId={item_id}: {rows}")

    extras = cur.execute("SELECT COUNT(*) FROM ExtraCharges WHERE BillId=?", (bill_id,)).fetchone()[0]
    print(f"  ExtraCharges for BillId={bill_id}: {extras}")

    # show sample BillItems
    for r in cur.execute("SELECT Id,BillId,ThicknessName,GlassDescription,MeasurementUnit,PricePerSqFt,TotalChargeAreaSqFt,TotalAmount FROM BillItems WHERE BillId=?", (bill_id,)):
        print("   ", r)

    for r in cur.execute("SELECT Id,BillItemId,Length,Width,ChargeAreaSqFt,RowNumber FROM MeasurementRows WHERE BillItemId=?", (item_id,)):
        print("   ", r)

    conn.close()