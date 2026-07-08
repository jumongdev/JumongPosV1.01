import json
data = json.load(open(r"C:\Users\ADMIN\Desktop\JumongPosV1.01\_wh_products.json"))
for p in data[:15]:
    print(f"ID={p['id']}: {p['name']} (stock={p['stockQty']})")
