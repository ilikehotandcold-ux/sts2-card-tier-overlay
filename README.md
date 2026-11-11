# Card Tier Overlay for Slay the Spire 2

Mod para **Slay the Spire 2** que muestra el tier de la [tier list de cartas de Mobalytics](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards) (**S / A / B / C / D**) en la esquina de cada carta que te ofrecen:

- recompensa de carta tras un combate
- pantallas de "elige una carta"
- tienda del mercader

| Tier | Color    |
|------|----------|
| S    | rojo     |
| A    | naranja  |
| B    | amarillo |
| C    | verde    |
| D    | azul     |

Incluye las tier lists de Ironclad, Silent, Regent, Necrobinder, Defect y las cartas de multijugador (424 cartas). Las cartas sin tier (básicas, incoloras, maldiciones…) no llevan insignia. En combate no se muestra nada.

*English: shows the Mobalytics card tier as a badge on cards in card rewards, choose-a-card screens and the shop. Download the zip from Releases, run `Instalar.bat`, launch the game.*

## Instalar

1. Descarga `StsTierOverlay-vX.Y.Z.zip` desde [Releases](https://github.com/ilikehotandcold-ux/sts2-card-tier-overlay/releases/latest) y descomprímelo entero.
2. Cierra el juego y haz doble clic en **`Instalar.bat`**.
   - Busca el juego en tus bibliotecas de Steam.
   - Si no lo encuentra, te pide que arrastres la carpeta que contiene `SlayTheSpire2.exe`.
   - Si descomprimes el zip dentro o al lado de la carpeta del juego, también lo detecta.
3. Abre el juego como siempre: Steam, `SlayTheSpire2.exe` o un `.bat`. Si pregunta si quieres cargar mods, acepta.

Para quitarlo: **`Desinstalar.bat`**, o desactívalo en el menú *Mods* del juego.

Instalación manual: copia la carpeta `StsTierOverlay` del zip en `Slay the Spire 2\mods\`.

## Cómo funciona

Es un mod normal del cargador de mods que trae el juego (`mods/<id>/mod_manifest.json` + DLL). Da igual cómo se abra el juego: lo carga el propio `SlayTheSpire2.exe`.

- **Detección de cartas:** cada carta (`NCard`) que entra en pantalla se comprueba. Si alguno de sus contenedores tiene un tipo cuyo nombre contiene uno de los patrones de `_screens` (por defecto `CardReward`, `ChooseACard`, `Merchant`), recibe la insignia. Cuando la carta sale de esa pantalla, la insignia se borra; el juego recicla las cartas, así que no llega a tu mano.
- **Emparejar cartas:** se compara el nombre de la clase de la carta (`PommelStrike`) con el de Mobalytics (`pommel-strike`), sin mayúsculas ni guiones. Funciona con el juego en cualquier idioma.

### Preparado para futuras versiones

| Si después de una actualización… | Qué pasa | Cómo se arregla |
|---|---|---|
| Mobalytics cambia la tier list | nada se rompe | actualiza `data/tiers.json` en el repo; a todos les llega al abrir el juego |
| salen cartas nuevas | no llevan insignia | igual: añádelas a `data/tiers.json` |
| el juego renombra una pantalla | esa pantalla se queda sin insignias | cambia `_screens` en `data/tiers.json`; se distribuye solo, sin recompilar |
| cambia el sistema de guardado | el parche del perfil falla y el juego vuelve a su comportamiento normal (perfil `modded`) | recompilar |

- No se engancha a métodos internos de las pantallas. Los únicos parches de Harmony son los del perfil, y cada uno se aplica por separado: si uno falla, se registra y lo demás sigue funcionando.
- Al arrancar, el mod descarga `data/tiers.json` de este repo (`raw.githubusercontent.com`, máximo 8 s) y lo guarda como `tiers.txt`. Sin conexión usa la última copia guardada o, si no hay, los datos incluidos en la DLL.
- Escribe un diagnóstico en `mods\StsTierOverlay\estado.txt`: de dónde salen los datos, qué componentes van bien o fallan, y qué nombres de la tier list no existen en tu versión del juego.

### Tu progreso

Cuando hay mods cargados, el juego usa un perfil `modded/` separado y vacío (sin Cronografía ni desbloqueos). Este mod declara `"affects_gameplay": false` y, **solo si todos los mods cargados son así**, mantiene tu perfil normal. Si cargas otro mod que sí cambie el juego, se respeta el comportamiento original.

## Actualizar la tier list

Mobalytics tiene protección anti-bots, así que la actualización se hace a mano desde tu navegador (unos segundos):

1. Abre la [tier list de Mobalytics](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards) y espera a que cargue.
2. Pulsa F12, ve a *Console*, pega el contenido de [`tools/mobalytics-extract.js`](tools/mobalytics-extract.js) y pulsa Enter. El JSON queda copiado en el portapapeles.
3. Pégalo sobre [`data/tiers.json`](data/tiers.json), haz commit y push. Todos los que tengan el mod lo reciben la próxima vez que abran el juego.

Formato:

```json
{
  "_screens": ["CardReward", "ChooseACard", "Merchant"],
  "Ironclad": { "S": ["offering", "dominate"], "A": ["feed"] }
}
```

Para usar tus propios tiers solo en tu PC, edita `mods\StsTierOverlay\tiers.txt` y crea un archivo vacío `no_actualizar.txt` en la misma carpeta para que no se sobrescriba.

## Compilar

Requisitos: .NET SDK 8 o superior y el juego instalado. No hace falta el SDK de .NET 9: se compila contra las librerías de runtime que trae el propio juego.

```powershell
.\build.ps1                                   # busca el juego en Steam o lo pregunta
.\build.ps1 -GameDir "C:\Games\Slay the Spire 2"
```

Genera `out/StsTierOverlay-vX.Y.Z.zip`, listo para compartir. Para publicarlo: `gh release create vX.Y.Z out/StsTierOverlay-vX.Y.Z.zip`.

Estructura:

```
src/        código del mod (TierOverlay.cs) y mod_manifest.json
data/       tiers.json: tier list + patrones de pantallas (lo que descarga el mod)
installer/  Instalar.bat, Desinstalar.bat, instalar.ps1, LEEME.txt
tools/      script para extraer la tier list de Mobalytics
```

## Créditos

- Tier list: [Mobalytics](https://mobalytics.gg/slay-the-spire-2/tier-lists/cards). Este proyecto no está afiliado a Mobalytics ni a Mega Crit.
- Código bajo licencia [MIT](LICENSE).
