# Black Box Reborn

Mod de R.E.P.O. (BepInEx 5) que registra cada nivel como la caja negra de un avión: quién extrae, quién
rompe, cuánto queda en el mapa. Lo enseña en un monitor dentro del camión, en un panel con **F6** y en el
HUD. Sucesor del BlackBox de RoemisTeam (roto desde que REPOLib cambió su API), escrito desde cero: no lleva
código ni assets del original, que no tiene licencia.

El README para jugadores (en inglés) está en [thunderstore/README.md](thunderstore/README.md).

## Cómo funciona

- **Solo cliente.** El juego ya sincroniza con todos los clientes los golpes (`BreakRPC`), quién agarra
  cada objeto (`playerGrabbing`), las destrucciones y la salud. Cada jugador calcula sus estadísticas en
  local: no hay objetos en red, no depende de REPOLib y funciona aunque el host no lo tenga.
- **Extracciones.** El juego borra los objetos por tres caminos (`Object.Destroy` en un jugador,
  `PhotonNetwork.Destroy` para los creados en red y la RPC `DestroyPhysGrabObjectRPC` para los de escena),
  así que no se engancha el borrado: `Tracker.Observe` guarda cada fotograma lo último que vio de cada objeto
  y, cuando desaparece, decide si se rompió, se lo tragó una caja de botín o se extrajo.
- **Mérito de extracción.** Se reparte según el tiempo que cada jugador llevó el objeto (empujar el carrito
  cuenta la mitad). Las cajas de botín (`ItemValuableBox`) acumulan el mérito de su contenido.
- **Culpa de los golpes.** Se reparte entre quienes lo sujetan; si nadie lo sujeta, el último que lo soltó
  hace menos de 3 s (lo que recuerda el juego), el último que lo tocó hace menos de 10 s (registro propio)
  o el jugador a menos de 1,5 m (chocarse, darle con otro objeto). Lo demás va a «Entorno». Un golpe sin
  culpable deja una línea en el registro para poder revisar el caso.
- **Destrucciones sin golpe** (`DestroyCauses`). Los HurtCollider (ataques y trampas) destruyen por defecto
  cualquier objeto que tocan, y el juego borra lo que cae por debajo de y = −50. Se marca qué HurtCollider
  está activo parcheando el `MoveNext` de su corrutina `ColliderCheck`, y se apunta la causa en
  `DestroyObject` (solo el host la ve; los clientes solo detectan caídas). Lo que se pierde en los primeros
  30 s sin nadie cerca es cosa del mapa al generarse: se descuenta del botín en vez de contar como pérdida.
- **Interfaz.** uGUI + TextMeshPro construidos por código (sin asset bundles), con LiberationSans y Teko,
  las dos fuentes que trae el juego. El juego renderiza a 752×423 y escala: por eso el monitor tiene su
  propio diseño (`MonitorViews`) con letra de 30 px o más, distinto del panel (`Views`).

## Estructura

```
src/Plugin.cs          entrada BepInEx, crea el Controller
src/Controller.cs      bucle: rastreador, teclado, monitor
src/Settings.cs        configuración (BepInEx/config/vicxelcode.BlackBoxReborn.cfg)
src/Loc.cs             textos en inglés y español
src/Stats/             modelo (nivel, jugador, partida) y Tracker
src/Patches/           ganchos Harmony (solo leen; un fallo se registra y el juego sigue)
src/UI/                panel F6, monitor del camión, HUD
src/Dev/DevTools.cs    solo en Debug: control remoto para pruebas
thunderstore/          manifest, README e icono del paquete
```

## Desarrollo

Requisitos: .NET SDK (8 o superior) y R.E.P.O. instalado. Las DLL del juego se leen de la carpeta de
Steam; otra ruta con `REPO_DIR=...` o `-p:RepoDir=...`. Assembly-CSharp se publiciza al compilar.

- `bash scripts/dev-run.sh` compila en Debug, lo copia a un perfil limpio (`../../.e2e/bbr-profile`, solo
  BepInEx + este mod) y lanza el juego con Doorstop apuntando a él. Antes, la ventana al escritorio
  «Claude» (`scripts/claude-desktop.ps1 -Names REPO`, ver CLAUDE.md del banco).
- `bash scripts/package.sh` compila en Release y construye el zip de Thunderstore en `dist/` con `tcli`
  (`dotnet tool install -g tcli`) a partir de `thunderstore.toml`, fuente única de nombre, versión,
  dependencias y categorías. La versión va en `thunderstore.toml` y en `Plugin.Version`; el script
  comprueba que coinciden.
- `bash scripts/publish.sh` lo construye y lo sube a Thunderstore (equipo `vicxelcode`, comunidad `repo`).
  Necesita el token de la cuenta de servicio del equipo en `../../.keys/thunderstore.env` como
  `TCLI_AUTH_TOKEN=...`; nunca se imprime.

### Probar sin tocar el juego (DevTools, solo Debug)

Se escriben órdenes, una por línea, en `BepInEx/bbr-dev/cmd.txt` del perfil; el mod deja allí
`status.txt` (estado cada segundo), las capturas y `dump.txt`.

| Orden | Qué hace |
|---|---|
| `start` | Desde el menú, nueva partida de un jugador (crea un archivo de guardado) |
| `level` | Avanza como al completar el nivel (nivel → tienda → sala → nivel) |
| `break n` / `extract n` | Golpea / extrae n objetos al azar por las rutas reales del juego |
| `hold n s` + `breakheld [heavy]` / `extractheld` | Igual, pero sujetados por el jugador local |
| `hurt n` · `activate` | Daño al jugador · activa el primer punto de extracción |
| `force <nivel>` · `force off` | Fija el siguiente nivel (p. ej. `force Wizard`), vía `RunManager.debugLevel` |
| `uidump` · `menu n` | Vuelca lienzos, textos y materiales de la interfaz del juego · abre una página de menú |
| `panel` · `tab n` · `sel n` · `lang es\|en` | Maneja el panel |
| `snap nombre monitor [dist]` · `snap nombre x y z rx ry rz` | Render a PNG con una cámara propia (relativo al camión) |
| `res w h` | Resolución de `snap` (752 423 = la del juego) |
| `shot nombre` · `dump` · `wait s` · `quit` | Captura de pantalla · jerarquía del camión y cámaras · pausa · salir |

## Limitaciones conocidas

- Las armas (granadas, escopeta) y los enemigos que rompen objetos se apuntan a «Desconocido»: el juego
  solo sabe quién causó el golpe en el host, no en cada cliente.
- Las estadísticas viven en memoria: se pierden al salir del juego.
- La bolsa de excedente (surplus) no suma al botín generado ni al extraído: devuelve valor que ya se
  contó al extraer.

No afiliado a semiwork ni a RoemisTeam.
