// Types mirror docs/PLAN.md §5 verbatim. Do not add fields here; the contract is the PM's.

export type Category = "Vape" | "Tobacco" | "Accessory" | "Drink" | "Snack";

export interface Location {   // one map marker (B5)
  id: number;
  slug: string;
  name: string;
  street: string;
  postalCode: string;
  city: string;
  lat: number;
  lng: number;
  googleMapsUrl: string | null;
  machines: Machine[];   // active machines only, never empty; sorted by label, then id
}

export interface Machine {    // one vending machine; the id the inventory routes take
  id: number;
  label: string;         // "" when it is the only machine at its location, else e.g. "Grün", "Driving Range"
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

export const fetchLocations = (signal?: AbortSignal) =>
  getJson<Location[]>("/api/locations", signal);

export const fetchInventory = (machineId: number, signal?: AbortSignal) =>
  getJson<Inventory>(`/api/machines/${machineId}/inventory`, signal);

// docs/PLAN.md §6 "location display name": the name without its leading "SMOKE " (the header
// carries the brand), wherever a location is named in the UI.
export const placeName = (l: Pick<Location, "name">) => l.name.replace(/^SMOKE\s+/i, "");
