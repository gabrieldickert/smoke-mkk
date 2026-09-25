# Product placeholder art

`make_products.py` draws the 13 brand-free product illustrations in
`frontend/public/products/` (file names from docs/PLAN.md §3). They stand in until the
owner supplies real packshots. A real photo simply replaces a file under the same name,
with no code change.

## Re-run

Needs Python 3 with Pillow (tested with Pillow 11.3). This is a dev-only tool: it is not
an app dependency and the Docker build does not use it.

```sh
python frontend/scripts/product-art/make_products.py            # writes the 13 .webp files
python frontend/scripts/product-art/make_products.py --sheet out.png   # plus a contact sheet
```

The contact sheet shows all 13 on the site's `--card` colour, at 200 px and at the
panel's 56 px size. Use it to check legibility after any change.

## Rules the script enforces

- 400×400 WebP with alpha, fully transparent background, no baked-in shadow.
- One height band: every product stands on y = 360; the tallest is about 320 px.
- One outline weight (`LINE`), one corner radius (`RADIUS`), light from the top-left.
- Labels use Space Grotesk from `public/fonts/` (OFL), in generic German words only.
  No logos, brand names, trademarked shapes (e.g. no bear-shaped gummies) or imitated
  lettering.
- To add a product, add a line to `PRODUCTS` that reuses one of the shared shapes.
