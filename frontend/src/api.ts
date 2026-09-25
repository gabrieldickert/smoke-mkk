// Types mirror docs/PLAN.md §5 verbatim. Do not add fields here; the contract is the PM's.

export type Category = "Vape" | "Tobacco" | "Accessory" | "Drink" | "Snack";

export interface Machine {
  id: number;
  slug: string;
  name: string;
  street: string;        // "" when unknown (#7)
  postalCode: string;
  city: string;
  lat: number;
  lng: number;
  googleMapsUrl: string | null;
}

export interface InventoryItem {
  productId: number;
  name: string;
  category: Category;
  imageUrl: string | null;   // site-relative, e.g. "/products/red-bull-250.webp"; file may not exist yet
  quantity: number;      // >= 0
  priceCents: number;    // >= 0
}

export interface Inventory {
  machineId: number;
  updatedAt: string | null;   // max(updated_at) over items; null when no items
  items: InventoryItem[];     // sorted by category (Vape, Tobacco, Accessory, Drink, Snack), then name
}

export interface InventoryWrite {   // PUT body element
  productId: number;
  quantity: number;
  priceCents: number;
}

async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(url, { signal, headers: { Accept: "application/json" } });
  if (!res.ok) throw new Error(`${res.status} ${url}`);
  return res.json() as Promise<T>;
}

export const fetchMachines = (signal?: AbortSignal) =>
  getJson<Machine[]>("/api/machines", signal);

export const fetchInventory = (machineId: number, signal?: AbortSignal) =>
  getJson<Inventory>(`/api/machines/${machineId}/inventory`, signal);
