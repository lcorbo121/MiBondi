# 🚌 MiBondi · Buses de Montevideo en tiempo real

Aplicación web **ASP.NET Core MVC (C#)** que muestra sobre un mapa, **en tiempo real**, la
posición de los ómnibus del Sistema de Transporte Metropolitano (STM) de Montevideo.

![.NET](https://img.shields.io/badge/.NET-10-512BD4) ![Leaflet](https://img.shields.io/badge/Leaflet-1.9.4-199900)

## ✨ Características

- 🗺️ **Mapa en vivo** con la cartografía oficial de Montevideo (GeoServer WMS de la IM) y
  OpenStreetMap como alternativa.
- ⏱️ **Auto-actualización cada 2 s** con un **contador circular tipo reloj** que muestra
  cuándo se produce el próximo refresco.
- 🔎 **Filtro por línea** con dos modos:
  - **`<select>` (DataProvider)** poblado con todas las líneas en circulación (número + texto).
  - **Búsqueda de texto** *case-insensitive* (TOUPPERCASE) que resuelve a la coincidencia más
    cercana: por número (`155`, `CE1`) o por texto (`terminal`, `pocitos`…).
- 🔵 **Marcadores informativos**: círculo azul con el número de línea, etiqueta con el texto de
  la línea al costado y **badge circular de velocidad** (con código de color: 🔴 detenido /
  🟠 lento / 🟢 en movimiento) por fuera y arriba del círculo.
- 🧭 Por defecto el mapa **arranca vacío** hasta que elegís/filtrás una línea.
- 🧩 **Fix de redimensionado**: el mapa se reacomoda solo (`invalidateSize` + `ResizeObserver`)
  para no romperse al agrandar la ventana.

## 🚀 Cómo correrlo

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/lcorbo121/MiBondi.git
cd MiBondi
dotnet run
```

Abrí en el navegador la URL que imprime la consola (por ej. `http://localhost:5147`).
Elegí una línea en el desplegable o escribila en el buscador y tocá **Seguir**.

## 🏗️ Arquitectura (MVC)

| Capa | Archivo | Rol |
|------|---------|-----|
| **Model** | `Models/Bus.cs` | Modelo de dominio (`Bus`, `LineaInfo`) + DTOs del GeoJSON de la STM |
| **Service** | `Services/StmService.cs` | Cliente HTTP de la API de la STM (inyectado con `IHttpClientFactory`) |
| **Controller** | `Controllers/HomeController.cs` | Sirve la vista del mapa |
| **Controller (API)** | `Controllers/BusController.cs` | Endpoints JSON que consume el frontend |
| **View** | `Views/Home/Index.cshtml` | Mapa Leaflet, filtro, contador y marcadores |

### Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/bus/posiciones?lineas=155,103` | Posiciones en vivo (todas o filtradas por línea) |
| `GET` | `/api/bus/lineas` | DataProvider: líneas en circulación (número + texto) |

## 🔌 Fuentes de datos

- **Posiciones en tiempo real** (no requiere API key):
  `POST https://www.montevideo.gub.uy/buses/rest/stm-online`
  - Body `{}` → todos los ómnibus
  - Body `{"lineas":["155","103"]}` → filtra por línea
  - Responde **GeoJSON** (`coordinates: [lng, lat]`, EPSG:4326).
- **Cartografía base**: capa WMS `stm_carto_basica` del GeoServer de la Intendencia de Montevideo.

## ⚙️ Configuración (`appsettings.json`)

```jsonc
"Stm":  { "Endpoint": "https://www.montevideo.gub.uy/buses/rest/stm-online" },
"Mapa": {
  "WmsUrl": "https://geoserver.montevideo.gub.uy/geoserver/wms",
  "WmsLayer": "stm_carto_basica",
  "CentroLat": -34.8721, "CentroLng": -56.1819, "Zoom": 12,
  "RefrescoSegundos": 2
}
```

## 🛠️ Stack

ASP.NET Core MVC · C# / .NET 10 · Leaflet · GeoJSON · WMS (GeoServer).
