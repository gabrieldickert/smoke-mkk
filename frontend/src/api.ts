// Types mirror docs/PLAN.md §5 verbatim. Do not add fields here; the contract is the PM's.

export type Category = "Vape" | "Tobacco" | "Accessory" | "Drink" | "Snack" | "Other";   // Other since B7: no keyword matched

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
  pictureUrl: string | null;   // absolute URL of a photo of this machine (B6); null for most machines
}

export interface InventoryItem {   // one Vendon stock item on this machine (B7); no images
  productId: number;     // Vendon stock_id
  name: string;          // Vendon name without the "SM:" prefix
  category: Category;    // keyword rule, §4.2
  quantity: number;      // >= 0, summed over the machine's slots
  priceCents: number;    // >= 0, lowest slot price
}

export interface SearchHit {   // B9: a machine whose current stock matches a product search
  machineId: number;
  products: string[];    // matching in-stock product names, sorted, at most 5
}

export interface Inventory {
  machineId: number;
  updatedAt: string | null;   // when the API fetched it from Vendon; null when no items
  items: InventoryItem[];     // sorted by category (Vape, Tobacco, Accessory, Drink, Snack, Other), then name
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

export const fetchSearch = (q: string, signal?: AbortSignal) =>
  getJson<SearchHit[]>(`/api/search?q=${encodeURIComponent(q)}`, signal);

// docs/PLAN.md §6 "location display name": the name without its leading "SMOKE " (the header
// carries the brand), wherever a location is named in the UI.
export const placeName = (l: Pick<Location, "name">) => l.name.replace(/^SMOKE\s+/i, "");
