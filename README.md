# 🚌 MiBondi · Buses de Montevideo en tiempo real

Aplicación web **ASP.NET Core MVC (C#)** que muestra sobre un mapa, **en tiempo real**, la
posición de los ómnibus del Sistema de Transporte Metropolitano (STM) de Montevideo, las
**paradas** por las que pasa cada línea y el **sentido** (ida/vuelta) de cada coche.

![.NET](https://img.shields.io/badge/.NET-10-512BD4) ![Leaflet](https://img.shields.io/badge/Leaflet-1.9.4-199900)

🌐 **En vivo:** https://www.MiBusCorbo.somee.com/

## ✨ Características

- 🗺️ **Mapa con 3 capas base**: OpenStreetMap (por defecto), Cartografía oficial de Montevideo
  (GeoServer WMS) y Satélite (Esri World Imagery), conmutables desde el selector de capas.
- ⏱️ **Auto-actualización cada 2 s** con un **contador circular tipo reloj** que muestra cuándo
  ocurre el próximo refresco.
- 🔎 **Buscador tipo combobox**: al escribir aparece un desplegable con las líneas que coinciden
  (por número o por destino). Búsqueda *case-insensitive* y sin acentos.
- 🔤 **Búsqueda por texto/destino**: ej. `149 ADUANA` filtra (del lado del cliente) los buses de
  la 149 cuyo destino se parece más a "ADUANA". El API solo filtra por número; el texto se
  resuelve visualmente por similitud.
- 🧭 **Sentido (ida/vuelta)**: cada destino distinto recibe un color; los buses se pintan según su
  sentido y se muestra una **leyenda**. La etiqueta de cada bus indica `→ DESTINO`.
- 🚏 **Paradas de la línea**: al buscar una línea se dibujan las paradas por las que pasa (overlay
  apagable con checkbox), con calle y esquina en el popup.
- 🛣️ **Recorrido por sentido**: traza la **ruta** que va a tomar cada bus (polilínea siguiendo las
  paradas en orden), **coloreada igual que el bus** según su sentido. Es un overlay apagable con
  checkbox ("Recorrido (sentido)"). Solo se dibujan las rutas de las variantes con buses activos.
- 🔵 **Marcadores informativos**: círculo con el número de línea + **badge de velocidad** (🔴
  detenido / 🟠 lento / 🟢 en movimiento).
- 🔗 **Enlaces directos**: `…/#155` o `…/#149 ADUANA` precargan la búsqueda.
- 📱 **Responsive** (iPhone 12 mini / 14): controles apilados, `100dvh`, *safe-area*, anti-zoom iOS.
- 🧹 **Sin publicidad**: oculta el banner/branding que somee inyecta en el plan gratuito.
- 🚀 **Deploy automático** a somee por FTP en cada push a `main` (GitHub Actions).

## 🚀 Cómo correrlo

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/lcorbo121/MiBondi.git
cd MiBondi
dotnet run
```

Abrí la URL que imprime la consola (por ej. `http://localhost:5147`), escribí una línea o
destino y elegí una sugerencia.

## 🏗️ Arquitectura (MVC)

| Capa | Archivo | Rol |
|------|---------|-----|
| **Model** | `Models/Bus.cs` | `Bus`, `LineaInfo`, `Parada`, `Recorrido` + DTOs de los GeoJSON |
| **Service** | `Services/StmService.cs` | Cliente HTTP de la STM y del GeoServer (`IHttpClientFactory`) |
| **Controller** | `Controllers/HomeController.cs` | Sirve la vista del mapa |
| **Controller (API)** | `Controllers/BusController.cs` | Endpoints JSON que consume el frontend |
| **View** | `Views/Home/Index.cshtml` | Mapa Leaflet, combobox, capas, colores y leyenda |

---

# 🔌 Endpoints

## A) API interna (la que expone esta app, en `BusController`)

El frontend **solo** habla con estos endpoints; el `StmService` se encarga de llamar a las
fuentes externas (así se evita CORS y se ocultan las URLs de origen).

### `GET /api/bus/posiciones?lineas=155,103`
- **Qué hace:** devuelve la posición en vivo de los ómnibus. Sin `lineas` devuelve todos.
- **Obtengo:** `{ ok, count, buses: [ { id, linea, sublinea, destino, tipoLinea, empresa, codigoBus, velocidad, lat, lng } ] }`
- **Cómo lo manejo:** el mapa lo consulta cada 2 s. Renderiza un marcador por bus, lo **colorea
  según `destino`** (sentido), arma la etiqueta `→ destino` y el badge de velocidad. Si se buscó
  texto (ej. `ADUANA`), filtra los buses por similitud del lado del cliente.

### `GET /api/bus/lineas`
- **Qué hace:** lista las líneas que están circulando (DataProvider del autocompletado).
- **Obtengo:** `{ ok, count, lineas: [ { linea, texto } ] }` (texto = sublínea representativa).
- **Cómo lo manejo:** se carga una vez al inicio. Alimenta el **combobox** (sugerencias al
  escribir) y sirve para resolver lo tipeado → código de línea exacto.

### `GET /api/bus/paradas?lineas=155,103`
- **Qué hace:** devuelve las paradas por las que pasa(n) la(s) línea(s).
- **Obtengo:** `{ ok, count, paradas: [ { cod, linea, calle, esquina, lat, lng } ] }`
- **Cómo lo manejo:** se dibujan como `circleMarker` al buscar una línea (se **cachea por
  línea**, no se recarga cada 2 s). Popup con `calle y esquina`. Es un overlay apagable.

### `GET /api/bus/recorridos?variantes=4145,8863`
- **Qué hace:** devuelve la **traza (ruta)** de cada variante: sus paradas en orden.
- **Obtengo:** `{ ok, count, recorridos: [ { variante, paradas: [ { cod, calle, esquina, lat, lng } ] } ] }`
  (las paradas vienen ordenadas por su `ordinal` en el recorrido).
- **Cómo lo manejo:** el frontend toma las **variantes que tienen buses activos** (cada bus trae
  su `variante` y `destino`), pide sus recorridos (se **cachea por set de variantes**) y dibuja una
  **polilínea por variante coloreada según el sentido** (mismo color que el bus). Overlay apagable.

## B) Fuentes externas (consumidas por `StmService`) — **no requieren API key**

### 1. Posiciones en tiempo real — STM
```
POST https://www.montevideo.gub.uy/buses/rest/stm-online
Content-Type: application/json
Body  {}                        -> todos los ómnibus
Body  {"lineas":["155","103"]}  -> solo esas líneas
```
- **Detalle:** con `GET` responde **405**; necesita `POST` JSON. Devuelve un **GeoJSON
  FeatureCollection**; cada feature trae en `properties`: `codigoEmpresa`, `codigoBus`, `linea`,
  `sublinea`, `tipoLineaDesc`, `destinoDesc`, `velocidad`, y en `geometry` un `Point` con
  `coordinates: [lng, lat]` (EPSG:4326).
- **Cómo lo manejo:** `StmService.GetBusesAsync` hace el POST, deserializa el GeoJSON a `Bus`
  (invirtiendo lng/lat). `GetLineasAsync` reutiliza la misma respuesta agrupando por `linea`
  para construir el DataProvider del autocompletado.

### 2. Variantes / recorridos — STM (recurso de apoyo)
```
GET https://www.montevideo.gub.uy/buses/rest/variantes
```
- **Detalle:** array de ~2150 variantes con `{ varianteCodigo, linea, lineaCodigo, origen,
  destino, sublinea, especial }`. Mapea **línea → variantes** con su `origen`/`destino`.
- **Cómo lo manejo:** se descubrió explorando endpoints hermanos del de posiciones. Confirma que
  el `variante` que trae cada bus coincide con el `cod_variante` del GeoServer y aporta el
  origen/destino de cada sentido. *En la app el sentido (ida/vuelta) se detecta directamente con
  el campo `destino` de `stm-online`*, por lo que este endpoint queda documentado como recurso
  disponible para futuras mejoras.

### 3. Paradas por línea — GeoServer (WFS)
```
GET https://geoserver.montevideo.gub.uy/geoserver/wfs
    ?service=WFS&version=2.0.0&request=GetFeature
    &typeNames=imm:v_uptu_paradas_con_horarios
    &outputFormat=application/json&srsName=EPSG:4326
    &cql_filter=desc_linea IN ('155','103')
```
- **Detalle:** GeoJSON de paradas. `properties`: `cod_ubic_parada`, `desc_linea`, `cod_variante`,
  `ordinal` (orden en el recorrido), `calle`, `esquina`; `geometry` = `Point [lng, lat]`.
- **Cómo lo manejo:** `StmService.GetParadasAsync` arma el `CQL_FILTER` (sanitizando las líneas a
  alfanumérico para evitar inyección), hace el GET y **deduplica por `cod_ubic_parada`** (una
  línea recorre varias variantes/sentidos y repite paradas).

### 4. Recorrido por variante — GeoServer (WFS)
```
GET https://geoserver.montevideo.gub.uy/geoserver/wfs
    ?service=WFS&version=2.0.0&request=GetFeature
    &typeNames=imm:Paradas_variantes_all
    &outputFormat=application/json&srsName=EPSG:4326
    &cql_filter=cod_variante IN (4145,8863)
```
- **Detalle:** mismas paradas pero **por variante** y con **cobertura completa** de variantes
  (`v_uptu_paradas_con_horarios` solo trae las que tienen horarios). `properties`: `cod_variante`,
  `ordinal`, `cod_ubic_parada`; `geometry` = `Point [lng, lat]`.
- **Cómo lo manejo:** `StmService.GetRecorridosAsync` filtra por `cod_variante`, **agrupa por
  variante y ordena por `ordinal`** para armar la secuencia de puntos de cada ruta. El frontend la
  dibuja como polilínea coloreada por sentido. Se usa esta capa (y no la de horarios) porque tiene
  todas las variantes.

### 5. Capas de mapa (tiles, directo desde Leaflet en el navegador)
| Capa | URL |
|------|-----|
| OpenStreetMap (default) | `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png` |
| Satélite (Esri World Imagery) | `https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}` |
| Cartografía Montevideo (WMS) | `https://geoserver.montevideo.gub.uy/geoserver/wms` · capa `stm_carto_basica` (`L.tileLayer.wms`) |

> 💡 Las capas/atributos del GeoServer se descubrieron con
> `…/geoserver/wfs?service=WFS&request=GetCapabilities` (buscando layers de *paradas/recorridos*).

## ⚙️ Configuración (`appsettings.json`)

```jsonc
"Stm":     { "Endpoint": "https://www.montevideo.gub.uy/buses/rest/stm-online" },
"Paradas": {
  "WfsUrl": "https://geoserver.montevideo.gub.uy/geoserver/wfs",
  "Layer":  "imm:v_uptu_paradas_con_horarios",   // paradas (con calle/esquina)
  "LayerVariantes": "imm:Paradas_variantes_all"  // recorrido por variante (cobertura completa)
},
"Mapa": {
  "WmsUrl": "https://geoserver.montevideo.gub.uy/geoserver/wms",
  "WmsLayer": "stm_carto_basica",
  "CentroLat": -34.8721, "CentroLng": -56.1819, "Zoom": 12,
  "RefrescoSegundos": 2
}
```

## 🚀 Deploy automático (GitHub Actions → somee)

`.github/workflows/deploy.yml` se ejecuta en cada push a `main`: publica *self-contained*
(multi-archivo, requisito de IIS), sube un `app_offline.htm` para **liberar los locks** de IIS,
sube todo por FTP y lo elimina para reiniciar la app. Credenciales en *secrets*
(`SOMEE_FTP_USERNAME`, `SOMEE_FTP_PASSWORD`).

## 🛠️ Stack

ASP.NET Core MVC · C# / .NET 10 · Leaflet · GeoJSON · WFS/WMS (GeoServer) · GitHub Actions.
