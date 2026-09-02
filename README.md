# Landing de StockHex

Esta rama **sólo** contiene el sitio publicado en
<https://waka-code.github.io/StockHex/>.

Vive aparte a propósito: `git clone` y la descarga en ZIP sacan únicamente la rama
por defecto, así que quien se lleva el proyecto **no se lleva la landing**. En `main`
no hay ni un archivo de esta página.

- `index.html` — la página entera, en un solo archivo. Las capturas van incrustadas
  como WebP en `data:` URI: no hay carpeta de recursos que se pueda romper al mover
  el sitio, y lo que se revisa es exactamente lo que se sirve.
- `.nojekyll` — GitHub Pages sirve el archivo tal cual, sin pasarlo por Jekyll.

El código del proyecto está en [`main`](https://github.com/waka-code/StockHex).
